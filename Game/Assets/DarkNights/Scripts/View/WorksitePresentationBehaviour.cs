using System;
using DarkNights.Core.ViewData;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 工位本地表现解释资源存量、变体和农田关联，并以本工位 ID 提交现有采集派工意图。
    /// 存量、占用和生产进度只读；耗尽仅改变外观，不结算采集或接管农场建筑的显示。
    /// </summary>
    public sealed class WorksitePresentationBehaviour : EntityPresentationBehaviour
    {
        private WorksiteView WorksiteVisual => Visual as WorksiteView ??
            throw new InvalidOperationException("Worksite presentation is missing WorksiteView.");
        public WorksiteViewData Current { get; private set; }
        public bool IsDepleted => IsAvailable && Current.Amount == 0;
        public override bool IsAvailable => IsBound && Current != null;

        public void Bind(int id, int epoch, string kind, Action<InputIntent> submit) => BindEntity(id, epoch, kind, submit);

        public bool Present(WorksiteViewData site, int epoch, Color ambient)
        {
            if (site == null || !Accept(site.Id, epoch, site.Kind)) return false;
            Current = site;
            Position(site.X, ambient);
            bool natural = site.FarmId == 0;
            WorksiteVisual.SetVisibility(natural && !IsDepleted, site.Variant, natural && IsDepleted);
            WorksiteVisual.TintSurface(Color.white);
            return true;
        }

        public bool AssignWorkers(int[] actors) => Submit(new InputIntent("Orders", actors, Id, Current?.X ?? 0));
        protected override bool SupportsView(EntityView value) => value is WorksiteView;
        protected override void ClearState() { Current = null; }
    }
}
