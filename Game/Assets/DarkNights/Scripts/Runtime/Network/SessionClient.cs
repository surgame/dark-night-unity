using System;
using System.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Session;
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
        private long connection, sequence, readySequence;
        private double nextReadyAt;
        public WorldReplica Replica { get; } = new WorldReplica();
        public bool Ready { get; private set; }
        public int PlayerSlot { get; private set; } = -1;
        public string Status { get; private set; } = "未连接";
        public event Action<CommandFeedback> Feedback;
        public event Action<SessionViewData> Updated;
        public event Action<Exception> Failed;

        public SessionClient(ProjectionCodec codec) { this.codec = codec; }

        public void Begin()
        {
            Dispose();
            connection = Replica.BeginConnection();
            sequence = readySequence = 0;
            nextReadyAt = 0;
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
                    var frame = codec.Decode(state.ProjectionPayload);
                    int previousEpoch = Replica.Current?.Epoch ?? 0;
                    if (!Replica.Apply(captured, frame)) return;
                    if (frame.Epoch != previousEpoch) { Ready = false; nextReadyAt = 0; }
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
        }

        public async ValueTask Advance(double now)
        {
            var frame = Replica.Current;
            if (Ready || endpoint == null || frame == null || frame.Loading || now < nextReadyAt) return;
            nextReadyAt = now + 1;
            readySequence = ++sequence;
            await NetworkCommandGateway.Instance.ProcessLocalCommandAsync(new SetReadyCommand
            {
                SenderObjectId = endpoint.ObjectId, Protocol = SessionAuthority.ProtocolVersion,
                Epoch = frame.Epoch, RequestSequence = readySequence, Ready = true,
                AppliedRevision = frame.Revision, AppliedPublication = frame.Publication
            });
        }

        public async ValueTask<long> Send(SessionOperation operation, int[] actors = null, int target = 0,
            float x = 0, string kind = "", int value = 0)
        {
            var frame = Replica.Current;
            if (!Ready || endpoint == null || frame == null) throw new InvalidOperationException("会话尚未就绪。");
            long request = ++sequence;
            await NetworkCommandGateway.Instance.ProcessLocalCommandAsync(new SessionCommand
            {
                SenderObjectId = endpoint.ObjectId, Protocol = SessionAuthority.ProtocolVersion,
                Epoch = frame.Epoch, PolicyRevision = frame.PolicyRevision, RequestSequence = request,
                Operation = operation, ActorIds = actors == null ? Array.Empty<int>() : (int[])actors.Clone(),
                TargetId = target, X = x, Kind = kind, Value = value
            });
            return request;
        }

        public void Receive(PlayerEndpoint source, CommandFeedback feedback)
        {
            if (source != endpoint || Replica.Current == null || feedback.Epoch != Replica.Current.Epoch) return;
            if (feedback.ReadyReply)
            {
                if (feedback.Sequence != readySequence) return;
                Ready = feedback.Code == "Ready";
                if (Ready) { PlayerSlot = feedback.PlayerSlot; Status = "已就绪"; }
            }
            Feedback?.Invoke(feedback);
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
