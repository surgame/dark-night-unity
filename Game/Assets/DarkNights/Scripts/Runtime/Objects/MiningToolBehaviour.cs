using DarkNights.Core.Config;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>由工具 Definition 装配的主动采集能力；只持有冻结配置，动作仍归角色，目标耐久仍归地图或矿床。</summary>
    [RequireConfig(typeof(MiningToolConfig))]
    public sealed partial class MiningToolBehaviour : PooledBehaviour
    {
        [Inject] private MiningToolConfig config;
        public MiningToolRules Rules { get; private set; }
        protected override void OnSpawn()
        {
            base.OnSpawn();
            var world = _context.Session?.Container.Resolve<ObjectSession>();
            Rules = world == null ? config.Freeze() : world.Resources.Equipment.Mining(_context.Owner.Definition.Guid.ToString());
            if (Rules == null) throw new System.InvalidOperationException("工具不在本会话冻结目录中。");
        }
        public override void OnDespawn()
        {
            Rules = null;
            base.OnDespawn();
        }
        internal bool Hit(ActorBehaviour actor, DarkNights.Core.ViewData.HeroMiningTarget target, float aim) =>
            HeroMining.TryMine(actor, target, aim, Rules);
    }
}
