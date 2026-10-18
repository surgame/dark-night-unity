using System;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Terrain;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>单次落石／塌方的YYGC状态所有者；支撑消失或可信角色进入后不可取消，按模拟秒推进。占位只等待或安全退休，不伤人、不重试触发，也不拥有另一张地图。</summary>
    [RequireConfig(typeof(GeologyEventConfig))]
    public sealed partial class GeologyEventBehaviour : SessionStateBehaviour<GeologyEventState>
    {
        [Inject] private GeologyEventConfig config;
        private GeologyEventConfig frozen;
        public GeologyEventConfig Configuration => frozen?.Freeze();
        public GeologyEventViewData Capture()
        {
            if (Current == null) return default;
            uint soil = 0;
            if (Current.Phase == 4 && frozen != null && Session.Terrain?.Map != null)
            {
                var map = Session.Terrain.Map; uint material = map.Tiles.ByKey(frozen.Material);
                for (int i = 0; i < frozen.RubbleCount; i++)
                    if (map.Read(frozen.Landing(i)).TryGetCell(out var cell) && cell.TileId == material &&
                        TerrainShapeGeometry.Decode(cell.Flags) == frozen.PileShape(i)) soil |= 1u << i;
            }
            return new GeologyEventViewData(Current.Phase, Current.Clock, Current.Reason, soil);
        }
        /// <summary>前兆及落石期间声明源岩仍归此事件；本地样板矿镐不能提前挖掉它解除危险，异常外部地形改变仍按安全退休处理。</summary>
        internal bool ReservesSource(CellCoord cell) => frozen != null && Current != null && Current.Phase >= 1 && Current.Phase <= 3 &&
            cell.V >= frozen.SourceV && cell.V < frozen.SourceV + frozen.SourceRows &&
            cell.U >= frozen.SourceU && cell.U < frozen.SourceU + frozen.Width;

        public void Prepare(GeologyEventConfig configuration = null, TerrainGeologyProgress progress = null)
        {
            frozen = (configuration ?? config)?.Freeze() ?? throw new InvalidOperationException("地质事件缺少配置。");
            var map = Session.Terrain?.Map ?? throw new InvalidOperationException("地质事件缺少活动前景地图。");
            if (progress == null && !frozen.EnterRegion && (!map.Descriptor.Bounds.Contains(frozen.Support) ||
                !map.Read(frozen.Support).TryGetCell(out var support) || support.IsEmpty || !map.Rules.CanDamage(support.TileId)))
                throw new InvalidOperationException("支撑触发需要真实可挖岩格。");
            if (!map.Descriptor.Bounds.Contains(new CellCoord(frozen.RegionU, frozen.RegionV)) ||
                !map.Descriptor.Bounds.Contains(new CellCoord(frozen.RegionU + frozen.RegionWidth - 1, frozen.RegionV + frozen.RegionHeight - 1)))
                throw new InvalidOperationException("地质事件触发区域越界。");
            if (progress == null)
            {
                string reason = GeologyLanding.Check(Session, frozen, true);
                if (reason.Length != 0) throw new InvalidOperationException(reason);
            }
            else GeologyRestoreValidation.Validate(Session, frozen, progress);
            PrepareState(new GeologyEventState { WorldId = map.World.WorldId.ToString(), MapEpoch = map.World.Epoch,
                Phase = progress?.Phase ?? 0, Clock = progress?.Clock ?? 0, Reason = progress?.Reason ?? "" });
        }

        public void Tick(double delta)
        {
            Session.Mutations.RequireWriting();
            if (double.IsNaN(delta) || double.IsInfinity(delta) || delta < 0 || delta > .25)
                throw new ArgumentOutOfRangeException(nameof(delta));
            if (delta == 0 || Session.Paused || Current == null || Current.Phase >= 5) return;
            var map = Session.Terrain?.Map;
            if (map == null || Current.WorldId != map.World.WorldId.ToString() || Current.MapEpoch != map.World.Epoch)
            { Retire("地图已退休"); return; }
            if (Current.Phase == 0)
            {
                bool triggered = frozen.EnterRegion ? Session.Index.Actors.Any(InRegion) :
                    map.Read(frozen.Support).TryGetCell(out var support) && support.IsEmpty;
                if (!triggered) return;
                SetPhase(1); Session.Notify("岩体失稳：离开落点，落地后用矿镐清障。", true); return;
            }
            if (Current.Phase == 4)
            {
                if (Enumerable.Range(0, frozen.RubbleCount).All(i => map.Read(frozen.Landing(i)).TryGetCell(out var cell) && cell.IsEmpty))
                { SetPhase(5); Session.Notify("瓦砾已清除，通道恢复。"); }
                return;
            }
            var state = Edit(); state.Clock += delta;
            if (state.Phase == 1)
            {
                if (state.Clock >= frozen.WarningSeconds)
                {
                    string preparation = GeologyLanding.Check(Session, frozen, false);
                    if (preparation.Length != 0) { Retire(preparation); return; }
                    GeologyLanding.Detach(Session, frozen); SetPhase(2);
                }
                return;
            }
            if (state.Phase == 2 && state.Clock < frozen.FallSeconds) return;
            string reason = GeologyLanding.Check(Session, frozen, true, detached: true);
            if (reason == "等待角色离开落点")
            {
                if (state.Phase == 2) { SetPhase(3); Edit().Reason = reason; }
                else if (state.Clock >= frozen.OccupancyWaitSeconds) Retire("占位等待超时，事件安全退休");
                return;
            }
            if (reason.Length != 0) { Retire(reason); return; }
            GeologyLanding.Stage(Session, frozen); SetPhase(4);
            Session.Notify("塌落土石形成堵路土堆；矿镐可清理，事件不会再次触发。");
        }

        private bool InRegion(ActorBehaviour actor)
        {
            var state = actor.Read();
            if (!state.ManualControl || state.Hp <= 0 || state.Boarded) return false;
            return frozen.Region.Contains(new CellCoord((int)Math.Floor(state.X / PlayableTerrain.CellPixels + .5f),
                (int)Math.Floor((state.Height + 4 - PlayableTerrain.OriginY) / PlayableTerrain.CellPixels + .5f)));
        }

        private void SetPhase(int phase) { var state = Edit(); state.Phase = phase; state.Clock = 0; state.Reason = ""; }
        private void Retire(string reason) { SetPhase(6); Edit().Reason = reason; Session.Notify("地质事件停止：" + reason, true); }
        public override void OnDespawn() { frozen = null; base.OnDespawn(); }
    }
}
