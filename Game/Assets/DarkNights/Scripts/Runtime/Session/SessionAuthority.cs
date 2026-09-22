using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DarkNights.Core.Logic.State;
using DarkNights.Core.Save;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Save;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 服务端串行访问本局 YYGC 对象能力，处理有界请求、权限、去重及固定 60 Hz 调度。
    /// 连接签发和 Ready 由可信服务端适配调用，不能直接暴露为 RPC；Host 业务也只走 Submit。
    /// 所有操作限创建线程，异步存储只能携带冻结快照或加载票据，完成后回到该线程提交。
    /// </summary>
    public sealed class SessionAuthority : IDisposable
    {
        public const int ProtocolVersion = 13;
        public const int MaximumPendingPerPlayer = 16;
        public const int ResultWindow = 64;
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private readonly SessionConnection[] connections = new SessionConnection[4];
        private readonly int[] generations = new int[4];
        private readonly Queue<SessionCommandEntry> pending = new Queue<SessionCommandEntry>();
        private readonly SessionProjector projector = new SessionProjector();
        private readonly ObjectSession world;
        private readonly SessionHeroControl heroes;
        private SessionEventJournal events;
        private SessionReceipt loadTicket;
        private bool started;
        public int Epoch { get; private set; } = 1;
        public int Revision { get; private set; }
        public int PolicyRevision { get; private set; }
        public long ServerTick { get; private set; }
        public CampControlMode ControlMode { get; private set; } = CampControlMode.SharedCamp;
        public bool Loading => loadTicket != null;
        public bool Closed { get; private set; }
        public int PendingCount => pending.Count;
        public int PlayerCount => connections.Count(c => c != null);
        public int ReadyCount => connections.Count(c => c != null && c.Ready);
        public SessionStorageRequest StorageRequest { get; private set; }
        public SessionAuthority(ObjectSession simulation)
        {
            world = simulation ?? throw new ArgumentNullException(nameof(simulation));
            heroes = new SessionHeroControl(world);
            events = new SessionEventJournal(world.Feedback, () => ServerTick);
            world.Activate();
        }
        // 服务端握手/恢复凭据验证完成后才调用；slot 0 是服务端配置的房主，不来自请求字段。
        public SessionConnection Connect(int slot)
        {
            CheckThread();
            if (Closed) throw new ObjectDisposedException(nameof(SessionAuthority));
            if (slot < 0 || slot >= connections.Length) throw new ArgumentOutOfRangeException(nameof(slot));
            if (Loading) throw new InvalidOperationException("Cannot replace connections while loading.");
            var connection = new SessionConnection(slot, checked(generations[slot] + 1), Revision);
            generations[slot] = connection.Generation;
            heroes.Release(slot);
            connections[slot]?.ResetWorld();
            connections[slot] = connection;
            return connection;
        }
        // Host 的网络观察端可单独停止；正式离房仍由会话所有者释放整个 Authority。
        public void Disconnect(SessionConnection connection, bool closeHostedSession = true)
        {
            CheckThread();
            if (!Active(connection)) return;
            if (connection.IsHost && closeHostedSession) { Dispose(); return; }
            heroes.Release(connection.PlayerSlot);
            connection.ResetWorld();
            connections[connection.PlayerSlot] = null;
        }
        // 适配层须先验证内容握手与实际应用的完整投影；此处只守护 epoch/revision 和当前连接。
        public bool AcknowledgeReady(SessionConnection connection, int epoch, int appliedRevision, bool assignDefaultHero = false)
        {
            CheckThread();
            if (!Active(connection) || Loading || epoch != Epoch ||
                appliedRevision < connection.BaselineRevision || appliedRevision > Revision) return false;
            connection.Ready = true;
            connection.DefaultHeroRequested = assignDefaultHero;
            if (connection.IsHost) started = true;
            if (assignDefaultHero && CanControlHero(connection) && heroes.AssignDefault(connection) > 0)
                Revision = checked(Revision + 1);
            return true;
        }
        public SessionReceipt Submit(SessionConnection connection, SessionRequest request)
        {
            CheckThread();
            SessionResultCode gate = ValidateEnvelope(connection, request);
            if (gate != SessionResultCode.Applied) return Receipt(connection, request, gate);
            if (connection.History.TryGetValue(request.Sequence, out var cached))
                return cached.Request.SameIntent(request) ? cached.Receipt : Receipt(connection, request, SessionResultCode.SequenceConflict);
            if (request.Sequence <= connection.HighestSequence) return Receipt(connection, request, SessionResultCode.SequenceExpired);
            if (Loading) return Receipt(connection, request, SessionResultCode.Loading);
            if (!connection.Ready) return Receipt(connection, request, SessionResultCode.NotReady);
            if (connection.PendingCount >= MaximumPendingPerPlayer || pending.Count >= MaximumPendingPerPlayer * connections.Length)
                return Receipt(connection, request, SessionResultCode.QueueFull);
            var entry = new SessionCommandEntry(connection, request, Receipt(connection, request, SessionResultCode.Pending));
            connection.HighestSequence = request.Sequence;
            connection.PendingCount++;
            connection.History.Add(request.Sequence, entry);
            pending.Enqueue(entry);
            return entry.Receipt;
        }

        public bool SubmitInput(SessionConnection connection, HeroInputRequest input)
        {
            CheckThread();
            return Active(connection) && connection.Ready && !Loading &&
                input.Protocol == ProtocolVersion && input.Epoch == Epoch && input.PolicyRevision == PolicyRevision &&
                (connection.IsHost || ControlMode == CampControlMode.SharedCamp) && heroes.Receive(connection, input, ServerTick);
        }

        // 每个服务端调度点调用一次；外层累积真实时间并保留积压，不传入任意网络 delta。
        public IReadOnlyList<SessionReceipt> Tick()
        {
            CheckThread();
            if (Closed) return Array.Empty<SessionReceipt>();
            ServerTick = checked(ServerTick + 1);
            var results = new List<SessionReceipt>(pending.Count);
            while (pending.Count != 0)
            {
                var entry = pending.Dequeue();
                if (entry.Connection.PendingCount > 0) entry.Connection.PendingCount--;
                entry.Receipt = Execute(entry.Connection, entry.Request);
                if (Active(entry.Connection)) entry.Connection.RememberCompleted(entry.Request.Sequence);
                results.Add(entry.Receipt);
            }
            heroes.Expire(ServerTick);
            if (started && !Loading)
            {
                int nextRevision = checked(Revision + 1);
                world.Advance(1.0 / 60);
                Revision = nextRevision;
            }
            return results.AsReadOnly();
        }

        private SessionReceipt Execute(SessionConnection connection, SessionRequest request)
        {
            SessionResultCode gate = ValidateEnvelope(connection, request);
            if (gate == SessionResultCode.Applied && Loading) gate = SessionResultCode.Loading;
            if (gate == SessionResultCode.Applied && !connection.Ready) gate = SessionResultCode.NotReady;
            if (gate == SessionResultCode.Applied && request.PolicyRevision != PolicyRevision) gate = SessionResultCode.PolicyChanged;
            if (gate == SessionResultCode.Applied && !connection.IsHost &&
                (ControlMode == CampControlMode.HostOnly || SessionOperations.HostRequired(request.Operation)))
                gate = SessionResultCode.PermissionDenied;
            if (gate == SessionResultCode.Applied && !world.ValidRequest(request)) gate = SessionResultCode.InvalidRequest;
            bool storage = request.Operation == SessionOperation.Save || request.Operation == SessionOperation.BeginLoad || request.Operation == SessionOperation.Restart;
            if (gate == SessionResultCode.Applied && storage && StorageRequest != null) gate = SessionResultCode.Loading;
            if (gate != SessionResultCode.Applied) return Receipt(connection, request, gate);
            int nextRevision = checked(Revision + 1);
            int affected = 1;
            int entityId = 0;
            if (request.Operation == SessionOperation.SetControlMode)
            {
                if (ControlMode != (CampControlMode)request.Value)
                {
                    PolicyRevision = checked(PolicyRevision + 1);
                    ControlMode = (CampControlMode)request.Value;
                    heroes.InvalidateInputs();
                    if (ControlMode == CampControlMode.HostOnly)
                        for (int slot = 1; slot < connections.Length; slot++) heroes.Release(slot);
                    else
                        foreach (var current in connections)
                            if (current?.Ready == true && current.DefaultHeroRequested) heroes.AssignDefault(current);
                }
            }
            else if (SessionHeroControl.IsOperation(request.Operation))
            {
                if (request.Operation == SessionOperation.ClaimHero) connection.DefaultHeroRequested = true;
                else if (request.Operation == SessionOperation.ReleaseHero) connection.DefaultHeroRequested = false;
                affected = heroes.Apply(connection, request);
                if (affected < 0) return Receipt(connection, request, SessionResultCode.PermissionDenied);
                if (affected > 0 && request.ActorIds.Count > 0) entityId = request.ActorIds[0];
            }
            else if (!storage)
            {
                try
                {
                    affected = world.Apply(request, out entityId);
                    if (request.Operation == SessionOperation.SetPaused) heroes.InvalidateInputs();
                }
                catch (SessionOperationException error) { return Receipt(connection, request, error.Code); }
            }
            Revision = nextRevision;
            var result = Receipt(connection, request, affected > 0 ? SessionResultCode.Applied : SessionResultCode.NoEffect, affected, entityId);
            if (storage)
            {
                if (request.Operation != SessionOperation.Save) loadTicket = result;
                StorageRequest = new SessionStorageRequest(request.Operation, request.Value, result,
                    request.Operation == SessionOperation.Save ? CaptureWorld() : null);
            }
            return result;
        }

        private SessionResultCode ValidateEnvelope(SessionConnection connection, SessionRequest request)
        {
            if (Closed) return SessionResultCode.SessionClosed;
            if (!Active(connection)) return SessionResultCode.InvalidConnection;
            if (!SessionOperations.ValidShape(request)) return SessionResultCode.InvalidRequest;
            if (request.Protocol != ProtocolVersion) return SessionResultCode.ProtocolMismatch;
            if (request.Epoch != Epoch) return SessionResultCode.EpochChanged;
            return SessionResultCode.Applied;
        }

        // 仅供权威存储和验证读取；包含 RNG 的恢复快照禁止发送到客户端或交给 View。
        public SessionSnapshot CaptureWorld()
        {
            CheckThread();
            if (Closed) throw new ObjectDisposedException(nameof(SessionAuthority));
            return world.CaptureWorld();
        }

        public SessionViewData CaptureProjection()
        {
            CheckThread();
            if (Closed) throw new ObjectDisposedException(nameof(SessionAuthority));
            return projector.Capture(this, world, events.Freeze(), events.FreezeRemnants());
        }

        // ticket 必须是本实例 BeginLoad 执行得到的同一个回执对象，不能用网络 DTO 重建。
        public void CompleteLoad(SessionReceipt ticket, string json)
        {
            CheckLoadTicket(ticket);
            try
            {
                int nextEpoch = checked(Epoch + 1);
                if (json == null) world.Restart();
                else world.Restore(json);
                events.Dispose();
                events = new SessionEventJournal(world.Feedback, () => ServerTick);
                projector.Clear();
                Epoch = nextEpoch;
                Revision = 0;
                started = false;
                pending.Clear();
                foreach (var connection in connections) connection?.ResetWorld(true);
                world.Loaded(json == null);
            }
            finally { loadTicket = null; StorageRequest = null; }
        }

        public void CompleteRestart(SessionReceipt ticket) => CompleteLoad(ticket, null);

        public void ReleaseStorage(SessionStorageRequest request)
        {
            CheckThread();
            if (!ReferenceEquals(StorageRequest, request)) return;
            StorageRequest = null;
        }

        public void CancelLoad(SessionReceipt ticket)
        {
            CheckLoadTicket(ticket);
            loadTicket = null;
            StorageRequest = null;
        }

        private void CheckLoadTicket(SessionReceipt ticket)
        {
            CheckThread();
            if (Closed || ticket == null || !ReferenceEquals(ticket, loadTicket))
                throw new InvalidOperationException("Load ticket is no longer active.");
        }

        private bool Active(SessionConnection connection) => !Closed && connection != null &&
            ReferenceEquals(connections[connection.PlayerSlot], connection);
        private bool CanControlHero(SessionConnection connection) => connection.IsHost || ControlMode == CampControlMode.SharedCamp;
        private SessionReceipt Receipt(SessionConnection connection, SessionRequest request, SessionResultCode code,
            int affected = 0, int entityId = 0) => new SessionReceipt(connection, request, Epoch, Revision,
                PolicyRevision, ServerTick, code, affected, entityId);

        internal void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new InvalidOperationException("Session operations must run on the owning thread.");
        }

        public void Dispose()
        {
            CheckThread();
            Closed = true;
            StorageRequest = null;
            events.Dispose();
            world.Dispose();
            pending.Clear();
            loadTicket = null;
            projector.Clear();
            foreach (var connection in connections) connection?.ResetWorld();
            Array.Clear(connections, 0, connections.Length);
        }
    }
}
