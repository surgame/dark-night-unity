using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AnyRules.Next.FishNet;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Diagnostics;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using GameCore.NetworkCommands;
using R3;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// Host 和远端共用的输入／只读副本入口；同步复制池化状态，Ready 确认前不开放业务操作。
    /// 连接、订阅与待确认序号归本实例所有，退出清理后旧端点回调不能再次写入副本。
    /// </summary>
    public sealed class SessionClient : IDisposable
    {
        private readonly ProjectionCodec codec;
        private PlayerEndpoint endpoint;
        private WorldSessionBehaviour observed;
        private IDisposable subscription;
        private long connection, sequence, readySequence, inputSequence;
        private double nextReadyAt;
        private string recoveryToken = "";
        public WorldReplica Replica { get; } = new WorldReplica();
        public long ConnectionGeneration => connection;
        public bool Ready { get; private set; }
        public Func<int, bool> MapReady { get; set; }
        public Func<string> MapIdentity { get; set; }
        public bool HadReady { get; private set; }
        public bool RequestDefaultHero { get; set; } = true;
        public int LastPayloadBytes { get; private set; }
        public int PlayerSlot { get; private set; } = -1;
        public string Status { get; private set; } = "未连接";
        public event Action<CommandFeedback> Feedback;
        public event Action<TerrainActionResult> TerrainFeedback;
        public event Action<SessionViewData> Updated;
        public event Action<Exception> Failed;
        public Action<SessionViewData> PrepareProjection { get; set; }
        public ClientMeasurements Measurements { get; private set; }

        public SessionClient(ProjectionCodec codec) { this.codec = codec; }

        public void EnableMeasurements() => Measurements = Measurements ?? new ClientMeasurements();

        public void ClearRecovery() => recoveryToken = "";
        public void GrantRecovery(PlayerEndpoint source, string token)
        {
            if (source == endpoint && token != null && token.Length <= 64) recoveryToken = token;
        }

        public void Begin()
        {
            Dispose();
            connection = Replica.BeginConnection();
            sequence = readySequence = inputSequence = 0;
            nextReadyAt = 0;
            HadReady = false;
            Status = "等待完整快照";
        }

        public void Attach(PlayerEndpoint value)
        {
            if (endpoint == value) return;
            if (endpoint != null) throw new InvalidOperationException("Duplicate owned endpoint.");
            endpoint = value;
        }

        public void Detach(PlayerEndpoint value)
        {
            if (endpoint != value) return;
            endpoint = null;
            Ready = false;
        }

        public void Observe(WorldSessionBehaviour behaviour)
        {
            if (observed == behaviour) return;
            subscription?.Dispose();
            observed = behaviour;
            long captured = connection;
            subscription = behaviour.ReactiveState.Where(s => s?.ProjectionPayload != null).Subscribe(state =>
            {
                try
                {
                    if (state.Protocol != SessionAuthority.ProtocolVersion) throw new FormatException("Session protocol mismatch.");
                    long started = Measurements == null ? 0 : Stopwatch.GetTimestamp();
                    var frame = codec.Decode(state.ProjectionPayload);
                    Measurements?.DecodeMilliseconds.Add((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
                    int previousEpoch = Replica.Current?.Epoch ?? 0;
                    if (!Replica.CanApply(captured, frame)) return;
                    if (Measurements != null) started = Stopwatch.GetTimestamp();
                    PrepareProjection?.Invoke(frame);
                    Measurements?.ObjectsMilliseconds.Add((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
                    if (Measurements != null) started = Stopwatch.GetTimestamp();
                    bool applied = Replica.Apply(captured, frame);
                    Measurements?.ApplyMilliseconds.Add((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
                    if (!applied) return;
                    LastPayloadBytes = state.ProjectionPayload.Length;
                    if (frame.Epoch != previousEpoch) { Ready = false; readySequence = 0; nextReadyAt = 0; }
                    Updated?.Invoke(frame);
                }
                catch (Exception error) { Status = error.Message; Failed?.Invoke(error); }
            });
        }

        public void Unobserve(WorldSessionBehaviour behaviour)
        {
            if (observed != behaviour) return;
            subscription?.Dispose();
            subscription = null;
            observed = null;
            Ready = false;
            // 当前世界对象已消失，旧副本和迟到资源结果立即失效；旧对象的 Detach 由上方引用比较拒绝。
            connection = Replica.BeginConnection();
            readySequence = 0;
            nextReadyAt = 0;
        }

        public async ValueTask Advance(double now)
        {
            var frame = Replica.Current;
            if (Ready || endpoint == null || frame == null || frame.Loading || now < nextReadyAt || MapReady?.Invoke(frame.Epoch) == false) return;
            nextReadyAt = now + 1;
            // 同一 epoch 的握手重试复用序号，避免持续重试使较早的成功回执永远失效。
            if (readySequence == 0) readySequence = ++sequence;
            await NetworkCommandGateway.Instance.ProcessLocalCommandAsync(new SetReadyCommand
            {
                SenderObjectId = endpoint.ObjectId, Protocol = SessionAuthority.ProtocolVersion,
                Epoch = frame.Epoch, RequestSequence = readySequence, Ready = true,
                RequestHero = RequestDefaultHero, AppliedRevision = frame.Revision,
                AppliedPublication = frame.Publication, RecoveryToken = recoveryToken, MapIdentity = MapIdentity?.Invoke() ?? ""
            });
        }

        public async ValueTask<long> Send(SessionOperation operation, int[] actors = null, int target = 0,
            float x = 0, string kind = "", int value = 0, int controlLease = 0)
        {
            var frame = Replica.Current;
            if (!Ready || endpoint == null || frame == null) throw new InvalidOperationException("会话尚未就绪。");
            long request = ++sequence;
            await NetworkCommandGateway.Instance.ProcessLocalCommandAsync(new SessionCommand
            {
                SenderObjectId = endpoint.ObjectId, Protocol = SessionAuthority.ProtocolVersion,
                Epoch = frame.Epoch, PolicyRevision = frame.PolicyRevision, RequestSequence = request,
                Operation = operation, ActorIds = actors == null ? Array.Empty<int>() : (int[])actors.Clone(),
                TargetId = target, X = x, Kind = kind, Value = value, ControlLease = controlLease
            });
            return request;
        }

        public ValueTask SendInput(int actor, int lease, int horizontal, bool jumpHeld, bool useHeld,
            bool jumpPressed = false, bool dropPressed = false)
        {
            var frame = Replica.Current;
            if (!Ready || endpoint == null || frame == null) return default;
            return NetworkCommandGateway.Instance.ProcessLocalCommandAsync(new HeroInputCommand
            {
                SenderObjectId = endpoint.ObjectId, Protocol = SessionAuthority.ProtocolVersion,
                Epoch = frame.Epoch, PolicyRevision = frame.PolicyRevision, ActorId = actor, ControlLease = lease,
                InputSequence = ++inputSequence, ObservedTick = frame.ServerTick, Horizontal = horizontal,
                JumpHeld = jumpHeld, UseHeld = useHeld, JumpPressed = jumpPressed, DropPressed = dropPressed
            });
        }

        public ValueTask SendTerrain(int u, int v, ulong expectedRevision, string requestId = null)
        {
            if (!Ready || endpoint == null) throw new InvalidOperationException("会话尚未就绪。");
            string request = string.IsNullOrWhiteSpace(requestId) ? "terrain-" + (++sequence) : requestId;
            return NetworkCommandGateway.Instance.ProcessLocalCommandAsync(new TerrainEditCommand
            {
                SenderObjectId = endpoint.ObjectId, RequestId = request, ExpectedRevision = expectedRevision, U = u, V = v
            });
        }

        public ValueTask SendFrozen(SessionRequest request)
        {
            if (!Ready || endpoint == null || request == null) throw new InvalidOperationException("会话尚未就绪或请求为空。");
            sequence = Math.Max(sequence, request.Sequence);
            return NetworkCommandGateway.Instance.ProcessLocalCommandAsync(new SessionCommand
            {
                SenderObjectId = endpoint.ObjectId, Protocol = request.Protocol, Epoch = request.Epoch,
                PolicyRevision = request.PolicyRevision, RequestSequence = request.Sequence, Operation = request.Operation,
                ActorIds = System.Linq.Enumerable.ToArray(request.ActorIds), TargetId = request.TargetId,
                X = request.X, Kind = request.Kind, Value = request.Value, ControlLease = request.ControlLease
            });
        }

        /// <summary>提交已经冻结的输入包；重发与协议探针仍经过正式 Gateway 和可信连接验证。</summary>
        public ValueTask SendInputFrozen(HeroInputRequest request)
        {
            if (!Ready || endpoint == null) throw new InvalidOperationException("会话尚未就绪。");
            inputSequence = Math.Max(inputSequence, request.Sequence);
            return NetworkCommandGateway.Instance.ProcessLocalCommandAsync(new HeroInputCommand
            {
                SenderObjectId = endpoint.ObjectId, Protocol = request.Protocol, Epoch = request.Epoch,
                PolicyRevision = request.PolicyRevision, ActorId = request.ActorId, ControlLease = request.ControlLease,
                InputSequence = request.Sequence, ObservedTick = request.ObservedTick, Horizontal = request.Horizontal,
                JumpHeld = request.JumpHeld, UseHeld = request.UseHeld, JumpPressed = request.JumpPressed, DropPressed = request.DropPressed
            });
        }

        public void Receive(PlayerEndpoint source, CommandFeedback feedback)
        {
            if (source != endpoint || Replica.Current == null || feedback.Epoch != Replica.Current.Epoch) return;
            if (feedback.ReadyReply)
            {
                if (feedback.Sequence != readySequence || Ready) return;
                Ready = feedback.Code == "Ready";
                if (Ready) { HadReady = true; PlayerSlot = feedback.PlayerSlot; Status = "已就绪"; }
            }
            Feedback?.Invoke(feedback);
        }

        internal void ReceiveTerrain(PlayerEndpoint source, TerrainActionResult result)
        {
            if (source != endpoint || result.RequestId == null) return;
            TerrainFeedback?.Invoke(result);
        }

        public void Dispose()
        {
            subscription?.Dispose();
            subscription = null;
            endpoint = null;
            observed = null;
            Replica.EndConnection(connection);
            Ready = false;
            PlayerSlot = -1;
            Status = "未连接";
        }
    }
}
