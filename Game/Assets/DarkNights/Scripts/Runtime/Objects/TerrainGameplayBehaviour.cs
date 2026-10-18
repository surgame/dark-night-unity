using System;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Terrain;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>正式地形配置与单虫巢穴的权威能力；地形、矿量归SessionTerrain。真实破壳后推进前兆、追近和清理，不结算角色伤害或新增奖励。</summary>
    [RequireConfig(typeof(TerrainProfileConfig))]
    public sealed partial class TerrainGameplayBehaviour : SessionStateBehaviour<TerrainGameplayState>
    {
        [Inject] private TerrainProfileConfig config;
        public FrozenTerrainRules Rules { get; private set; }

        public void InitializeSession(ObjectSession session)
        {
            if (config == null || session == null) throw new InvalidOperationException("缺少网格业务装配配置。");
            Rules = session.Terrain?.Rules;
            if (Rules != null && Rules.Fingerprint != config.Freeze(session.Terrain.Definition).Fingerprint)
                throw new InvalidOperationException("网格与 ObjectDefinition 配置身份不一致。");
            PrepareState(new TerrainGameplayState { Nests = Initial(session.Terrain?.InitialNests, session.Terrain?.InitialNestProgress) });
        }
        internal static NestCrawlerState[] Initial(System.Collections.Generic.IReadOnlyList<TerrainNest> nests,
            System.Collections.Generic.IReadOnlyList<TerrainNestProgress> progress)
        {
            if (nests == null) return Array.Empty<NestCrawlerState>();
            return nests.Select(n =>
            {
                var saved = progress?.FirstOrDefault(p => p.Id == n.Id);
                return saved == null ? new NestCrawlerState { Id = n.Id, X = n.SpawnX, Height = n.SpawnHeight, Hp = n.HitPoints } :
                    new NestCrawlerState { Id = n.Id, Phase = saved.Phase, Clock = saved.Clock, X = saved.X, Height = saved.Height,
                        VerticalSpeed = saved.VerticalSpeed, Hp = saved.Hp };
            }).ToArray();
        }
        internal TerrainNestProgress[] CaptureProgress() => Current.Nests.Select(n => new TerrainNestProgress(
            n.Id, n.Phase, n.Clock, n.X, n.Height, n.VerticalSpeed, n.Hp)).ToArray();
        internal NestViewData[] CaptureView() => Current.Nests.Select(n =>
        {
            var layout = Session.Terrain.InitialNests.Single(v => v.Id == n.Id);
            return new NestViewData(n.Id, layout.U, layout.Row, n.Phase, n.Clock, n.X, n.Height, n.Hp);
        }).ToArray();

        internal void Tick(double delta)
        {
            if (Current.Nests.Length == 0 || delta <= 0) return;
            if (delta > .25 || double.IsNaN(delta)) throw new ArgumentOutOfRangeException(nameof(delta));
            var states = Edit().Nests;
            for (int i = 0; i < states.Length; i++)
            {
                ref var state = ref states[i];
                int nestId = state.Id;
                var nest = Session.Terrain.InitialNests.Single(n => n.Id == nestId);
                if (state.Phase == 3) continue;
                if (state.Phase == 0)
                {
                    bool lower = Empty(nest.U, nest.Row), upper = Empty(nest.U, nest.Row - 1);
                    if (!lower && !upper) continue;
                    var other = new CellCoord(nest.U, -(lower ? nest.Row - 1 : nest.Row));
                    if (!lower || !upper) Session.Terrain.Map.StageDamage(other, int.MaxValue, out _);
                    state.Phase = 1; state.Clock = 0;
                    Session.Notify("岩缝里传来窸窣声，巢穴已经被惊动。", true);
                    continue;
                }
                state.Clock = Math.Min(1000000, state.Clock + delta);
                if (state.Phase == 1)
                {
                    if (state.Clock < nest.WarningSeconds || !Empty(nest.U, nest.Row) || !Empty(nest.U, nest.Row - 1)) continue;
                    state.Phase = 2; state.Clock = 0;
                }
                NestCrawlerMotion.Tick(Session, nest, ref state, delta);
            }
        }
        private bool Empty(int u, int row) => Session.Terrain.Map.Read(new CellCoord(u, -row)).TryGetCell(out var cell) && cell.IsEmpty;

        /// <summary>已有权威武器入口调用；休眠巢不接受清理，命中不引发角色伤害或奖励。</summary>
        internal bool ContainsPoint(float x, float height, float radius) => HitIndex(x, height, radius) >= 0;
        private int HitIndex(float x, float height, float radius) => Array.FindIndex(Current.Nests, n => n.Phase == 2 && n.Hp > 0 &&
            Math.Abs(n.X - x) <= 6 + radius && height >= n.Height - radius && height <= n.Height + 12 + radius);
        internal bool Hit(float x, float height, float radius, int damage)
        {
            if (Current.Nests.Length == 0 || damage <= 0) return false;
            int index = HitIndex(x, height, radius);
            if (index < 0) return false;
            ref var state = ref Edit().Nests[index];
            state.Hp = Math.Max(0, state.Hp - damage);
            if (state.Hp == 0) { state.Phase = 3; state.Clock = 0; Session.Notify("幼虫已清理，这处巢穴不会再次出虫。"); }
            return true;
        }
        internal bool Strike(float x, float height, float dx, float dy, float reach, int damage)
        {
            for (float d = 0; d <= reach; d += 1)
            {
                float nx = x + dx * d, nh = height + dy * d;
                if (BallisticMotion.Solid(Session, nx, nh, 0)) return false;
                if (Hit(nx, nh, 0, damage)) return true;
            }
            return false;
        }
        public override void OnDespawn() { Rules = null; base.OnDespawn(); }
    }
}
