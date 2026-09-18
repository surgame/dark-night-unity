using System;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 负责矿床、钻机和主角部署之间的一个性事务；真实钻机仍由 ObjectSession 创建并由 YYGC 索引、保存及投影。
    /// 本类不拥有独立状态，所有变更都通过宿主会话的 MutationBatch 写入对象 Behaviour。
    /// </summary>
    internal sealed class ObjectSessionMineralDrill
    {
        private readonly ObjectSession session;

        internal ObjectSessionMineralDrill(ObjectSession session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        internal bool Start(int depositId, int drillId) => session.Mutations.Run(() =>
        {
            MineralDepositBehaviour deposit = session.Index.Find<MineralDepositBehaviour>(depositId);
            WorksiteBehaviour drill = session.Index.Find<WorksiteBehaviour>(drillId);
            return MineralDrillBusiness.Start(deposit, drill);
        });

        internal bool Stop(int depositId, int drillId) => session.Mutations.Run(() =>
        {
            MineralDepositBehaviour deposit = session.Index.Find<MineralDepositBehaviour>(depositId);
            WorksiteBehaviour drill = session.Index.Find<WorksiteBehaviour>(drillId);
            return MineralDrillBusiness.Stop(deposit, drill);
        });

        internal int Deploy(int actorId, int depositId) => session.Mutations.Run(() =>
        {
            ActorBehaviour actor = session.Index.Find<ActorBehaviour>(actorId);
            MineralDepositBehaviour deposit = session.Index.Find<MineralDepositBehaviour>(depositId);
            ActorState actorState = actor?.Read();
            HeroControlDefinition hero = session.Catalog.Balance.HeroControl;
            float targetHeight = session.Terrain == null || deposit == null ? 0 :
                PlayableTerrain.OriginY - (deposit.Y + .5f) * PlayableTerrain.CellPixels;
            if (deposit == null || actorState == null || actor.Enemy || actor.Hp <= 0 || !actorState.ManualControl || hero == null ||
                actorState.SelectedItem != 1 || Math.Abs(actor.X - (deposit?.X ?? 0)) > hero.WorkReach ||
                Math.Abs(actorState.Height - targetHeight) > hero.WorkReach || actorState.DrillCharges <= 0 ||
                deposit.Remaining <= 0 || deposit.DrillId != 0) return 0;
            ObjectDefinition definition = session.Resources.FindOptional(WorksiteBehaviour.MineralDrillRule);
            if (definition == null) throw new InvalidOperationException("Mineral drill definition is not prepared.");
            int variant = deposit.ResourceId == "gold" ? 2 : 1;
            WorksiteBehaviour drill = (WorksiteBehaviour)session.Create(definition, deposit.X,
                "mineral-drill." + actorId + "." + deposit.Id, true, variant, "", null);
            if (!MineralDrillBusiness.Start(deposit, drill)) throw new InvalidOperationException("Mineral drill could not attach.");
            actor.Edit().DrillCharges--;
            return drill.Id;
        });

        internal void Tick(MineralDepositBehaviour deposit, double seconds)
        {
            WorksiteBehaviour drill = session.Index.Find<WorksiteBehaviour>(deposit.DrillId);
            MineralDrillBusiness.Tick(deposit, drill, seconds);
        }
    }
}
