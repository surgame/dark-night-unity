using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.ViewData;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 矿床的只读表现适配；容量和手采阶段来自投影，视觉只负责位置、稀有度变体和枯竭标记。
    /// 不提交工人订单，也不修改地形格，矿床行为由 YYGC MineralDepositBehaviour 唯一拥有。
    /// </summary>
    public sealed class MineralDepositPresentationBehaviour : EntityPresentationBehaviour
    {
        private WorksiteView DepositVisual => Visual as WorksiteView ??
            throw new InvalidOperationException("Mineral deposit presentation is missing WorksiteView.");

        public WorksiteViewData Current { get; private set; }
        public override bool IsAvailable => IsBound && Current != null;

        public void Bind(int id, int epoch, string kind, Action<InputIntent> submit) => BindEntity(id, epoch, kind, submit);

        public bool Present(WorksiteViewData deposit, int epoch, Color ambient)
        {
            if (deposit == null || !deposit.IsMineralDeposit || !Accept(deposit.Id, epoch, deposit.Kind)) return false;
            Current = deposit;
            float height = PlayableTerrain.OriginY - (deposit.Y + .5f) * PlayableTerrain.CellPixels;
            Position(deposit.X, ambient, height);
            DepositVisual.SetVisibility(deposit.Amount > 0, Variant(deposit.Rarity), deposit.Amount == 0);
            DepositVisual.TintSurface(Color.white);
            return true;
        }

        protected override bool SupportsView(EntityView value) => value is WorksiteView;
        protected override void ClearState() { Current = null; }

        private static int Variant(string rarity)
        {
            switch (rarity)
            {
                case "rare": return 1;
                case "epic": return 2;
                case "legendary": return 3;
                default: return 0;
            }
        }
    }
}
