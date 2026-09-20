using System;
using DarkNights.Core.ViewData;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 建筑本地表现解释冻结施工阶段、受击和训练队列，生成修缮及派工的显式目标意图。
    /// 不推进进度、训练或扣款；原生外观只接收采样时间、可见性及颜色，解绑后没有活建筑副本。
    /// </summary>
    public sealed class BuildingPresentationBehaviour : EntityPresentationBehaviour
    {
        private BuildingView BuildingVisual => Visual as BuildingView ??
            throw new InvalidOperationException("Building presentation is missing BuildingView.");
        public BuildingViewData Current { get; private set; }
        public bool IsConstructing => IsAvailable && Current.Progress < 1;
        public int TrainingCount => Current?.Training.Count ?? 0;
        public override bool IsAvailable => IsBound && Current != null;

        public void Bind(int id, int epoch, string kind, Action<InputIntent> submit) => BindEntity(id, epoch, kind, submit);

        public bool Present(BuildingViewData building, int epoch, Color ambient, ExpeditionDeviceData device = null, int moduleMask = 0)
        {
            if (building == null || !Accept(building.Id, epoch, building.Kind)) return false;
            Current = building;
            Position(building.X, ambient, device?.Height ?? 0);
            if (BuildingVisual.HasPoseClips) BuildingVisual.SamplePose("construction", Math.Min(building.Progress, 0.999999));
            BuildingVisual.SetVisibility(building.Progress >= 1 || BuildingVisual.FadeConstruction,
                building.Progress < 1 && !BuildingVisual.FadeConstruction, false);
            Color tint = building.HitFlash > 0 ? new Color(1.4f, 1.15f, 1.1f) : Color.white;
            if (BuildingVisual.FadeConstruction && building.Progress < 1) tint.a = 0.4f + (float)building.Progress * 0.6f;
            if (device != null)
            {
                BuildingVisual.PresentExpedition(moduleMask);
                if (device.Stage is 0 or 6) BuildingVisual.SetVisibility(false, false, false);
                if (!device.Powered && building.Kind != "ship") tint *= .55f;
            }
            BuildingVisual.TintSurface(tint);
            return true;
        }

        public bool Repair() => Submit(new InputIntent("Repair", Array.Empty<int>(), Id));
        public bool AssignWorkers(int[] actors) => Submit(new InputIntent("Orders", actors, Id, Current?.X ?? 0));
        protected override bool SupportsView(EntityView value) => value is BuildingView;
        protected override void ClearState() { Current = null; }
    }
}
