using DarkNights.Samples.LanCoop.Core;
using GameCore.Objects.NetworkStates;

namespace DarkNights.Samples.LanCoop.Runtime
{
    /// <summary>由 ObjectDefinition 装配的会话行为；只把权威世界投影交给 StateSynchronizer，无经济计算。</summary>
    public sealed class CampBehaviour : StatefulBehaviour<CampState>
    {
        protected override void Initialize() { }
        public void Publish(CampReplica replica)
        {
            using (var mutation = MutateState())
            {
                var state = mutation.Value;
                state.Epoch = replica.Epoch;
                state.Revision = replica.Revision;
                state.Coins = replica.Coins;
                state.Purchases = replica.Purchases;
                state.Occupant = replica.Occupant;
                state.Paused = replica.Paused;
                state.SimulationTicks = replica.SimulationTicks;
            }
        }
    }
}
