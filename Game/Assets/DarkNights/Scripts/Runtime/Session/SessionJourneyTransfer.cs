using DarkNights.Core.ViewData;
using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.State;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;

namespace DarkNights.Runtime.Session
{
    /// <summary>服务端航程的地图提交和连接就绪屏障；只持有候选资源及可信连接引用，持久状态仍归 YYGC 航程能力。</summary>
    internal sealed class SessionJourneyTransfer
    {
        private readonly ObjectSession world;
        private readonly List<SessionConnection> participants = new List<SessionConnection>();
        private long deadline;
        private bool waiting, timeoutReported;

        internal SessionJourneyTransfer(ObjectSession world) { this.world = world; }

        internal bool Pump(long tick, SessionConnection[] connections, Action beginEpoch)
        {
            var flow = world.Flow;
            if (flow?.Enabled != true) return false;
            int before = world.Journey.Capture().Revision;
            flow.Pump();
            if (!world.Paused)
            {
                var data = flow.TakeArrivalCandidate();
                if (data != null)
                {
                    participants.Clear();
                    participants.AddRange(connections.Where(c => c?.Ready == true));
                    if (Install(data))
                    {
                        beginEpoch();
                        waiting = true; timeoutReported = false;
                        deadline = tick + (long)Math.Ceiling(flow.ArrivalTimeoutSeconds * 60);
                        return true;
                    }
                }
            }
            if (waiting && flow.Phase != JourneyPhase.ArrivalSync) Reset();
            if (waiting)
            {
                participants.RemoveAll(c => !ReferenceEquals(connections[c.PlayerSlot], c));
                bool expired = tick >= deadline;
                bool ready = connections[0]?.Ready == true && participants.All(c => c.Ready || expired && !c.IsHost);
                if (expired && !timeoutReported)
                {
                    timeoutReported = true;
                    world.Mutations.Run(() =>
                    {
                        var pilot = world.Index.Find<ActorBehaviour>(world.Expedition.Ship.Read().PilotId);
                        if (pilot != null && participants.Any(c => !c.Ready && c.PlayerSlot == pilot.Read().ControllerSlot))
                            world.Ship.ReleasePilot(pilot.Id);
                        world.Notify("到达同步等待超时；未就绪成员继续等待同步，飞船保持安全悬停。", true);
                        return true;
                    });
                }
                if (ready && !world.Paused)
                {
                    world.Mutations.Run(() => { flow.ReleaseDescent(); return true; });
                    Reset();
                }
            }
            return before != world.Journey.Capture().Revision;
        }

        private bool Install(PlayableTerrain data)
        {
            TerrainMapAuthority candidate = null;
            bool committed = false;
            try
            {
                candidate = world.Terrain.Prepare(data);
                var previous = world.Terrain.Map;
                world.Mutations.Run(() =>
                {
                    foreach (var mineral in world.Index.MineralDeposits.ToArray()) world.Lifecycle.Retire(mineral);
                    var definition = world.Resources.Find(MineralDepositRuleConfig.Rule);
                    foreach (var deposit in data.Deposits)
                        world.Create(definition, (deposit.X + .5f) * PlayableTerrain.CellPixels,
                            "terrain.deposit." + deposit.Id, true, 0, "", null, false, 0, deposit);
                    world.Mutations.OnRollback(world.Terrain.Swap(candidate, data));
                    world.Flow.ArrivalCommitted();
                    foreach (var actor in world.Index.Actors)
                    {
                        var state = actor.Edit();
                        state.ControlLease = checked(state.ControlLease + 1);
                        HeroControlBehaviour.ResetInput(state);
                    }
                    return true;
                });
                committed = true;
                try { previous?.Dispose(); }
                catch (Exception cleanupError) { UnityEngine.Debug.LogException(cleanupError); }
                return true;
            }
            catch (Exception error)
            {
                if (committed) throw;
                world.Mutations.Run(() => { world.Flow.ArrivalFailed(error.Message); return true; });
                return false;
            }
            finally { if (!committed) candidate?.Dispose(); }
        }

        internal void Reset() { participants.Clear(); waiting = timeoutReported = false; deadline = 0; }
    }
}
