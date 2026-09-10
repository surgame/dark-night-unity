using System.Collections.Generic;
using System.Threading.Tasks;
using DarkNights.Samples.LanCoop.Core;
using FishNet.Connection;
using GameCore.NetworkCommands;
using VitalRouter;

namespace DarkNights.Samples.LanCoop.Runtime
{
    /// <summary>单场会话的服务端适配；连接代次及频率限制留在 Runtime，纯规则调用栈可直接断点跟踪。</summary>
    public sealed class SampleAuthority
    {
        private readonly CampSession world = new CampSession();
        private readonly Dictionary<NetworkConnection, int> players = new Dictionary<NetworkConnection, int>();
        private readonly Dictionary<NetworkConnection, SampleEndpoint> endpoints = new Dictionary<NetworkConnection, SampleEndpoint>();
        private readonly Dictionary<NetworkConnection, int> budgets = new Dictionary<NetworkConnection, int>();
        private int nextPlayer;
        private float budgetTime, accumulator;
        private readonly CampBehaviour behaviour;

        public SampleAuthority(CampBehaviour behaviour) { this.behaviour = behaviour; Publish(); }
        public void Add(NetworkConnection connection, SampleEndpoint endpoint)
        {
            players.Add(connection, ++nextPlayer);
            endpoints.Add(connection, endpoint);
        }
        public void Remove(NetworkConnection connection)
        {
            if (players.TryGetValue(connection, out int player)) world.RemovePlayer(player);
            players.Remove(connection);
            endpoints.Remove(connection);
            budgets.Remove(connection);
            Publish();
        }
        public ValueTask Handle(CampCommand command, PublishContext publication)
        {
            if (!NetworkCommandContext.TryGet(publication, out var context) || !context.IsServerExecution ||
                !players.TryGetValue(context.SenderConnection, out int player)) return default;
            var connection = context.SenderConnection;
            budgets.TryGetValue(connection, out int count);
            budgets[connection] = count + 1;
            // IsHostInput 由 Gateway 的本地主持入口构造，不能由客户端请求填写。
            string result = count >= 60 ? "RateLimited" : world.Apply(player, context.IsHostInput,
                command.RequestSequence, command.Protocol, command.Epoch, command.Operation, command.EntityId);
            Publish();
            var snapshot = world.Snapshot();
            endpoints[connection].Reply(connection, command.RequestSequence, result, snapshot.Epoch, snapshot.Revision);
            return default;
        }
        public void Advance(float unscaledDelta)
        {
            budgetTime += unscaledDelta;
            if (budgetTime >= 1) { budgetTime = 0; budgets.Clear(); }
            accumulator += unscaledDelta;
            int steps = 0;
            while (accumulator >= 1f / 60f && steps < 120)
            {
                accumulator -= 1f / 60f;
                world.Step();
                steps++;
            }
            // 10 Hz 完整投影。暂停仍运行网络；背压时保留模拟积压，不跳步。
            if (steps > 0 && world.Snapshot().SimulationTicks % 6 < steps) Publish();
        }
        private void Publish() => behaviour.Publish(world.Snapshot());
    }
}
