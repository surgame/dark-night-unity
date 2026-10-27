using System;
using AnyRules.Next;
using AnyRules.Next.Unity;

namespace DarkNights.View.Terrain
{
    /// <summary>表现宿主当前版本的只读进度快照；冻结地形、墙体、矿层及背景分页计数，不推进调度或相机回执。</summary>
    public readonly struct TerrainPreviewProgress
    {
        public TerrainPageProgress Terrain { get; }
        public TerrainPageProgress Rock { get; }
        public TerrainPageProgress Minerals { get; }
        public TerrainPageProgress Background { get; }
        public string Message { get; }

        internal TerrainPreviewProgress(Exception error, ARDMapController controller, TerrainReplicaRegion region,
            TerrainReplicaSource replica, TerrainInputBatchQueue queue, CaveVisualSource cave, MineralLayerPresentation embedded,
            MineralReplicaPresentation minerals, GridBounds visible, bool loading, bool baseline, bool useReplica, bool ready)
        {
            bool hasInput = !loading && !baseline && !queue.HasPending && region?.Loading != true &&
                replica?.WaitingForBaseline != true && replica?.ProjectionRefreshPending != true;
            Terrain = hasInput ? TerrainPageProgress.Capture(controller, visible) : default;
            Rock = hasInput ? cave?.RockProgress ?? new TerrainPageProgress(0, 0) : default;
            Minerals = useReplica ? minerals?.PageProgress ?? default : embedded?.PageProgress ?? default;
            Background = cave?.BackgroundProgress ?? default;
            Message = Describe(error, controller, region, replica, queue, cave, minerals, loading, baseline, useReplica, ready);
        }

        private static string Describe(Exception error, ARDMapController controller, TerrainReplicaRegion region,
            TerrainReplicaSource replica, TerrainInputBatchQueue queue, CaveVisualSource cave,
            MineralReplicaPresentation minerals, bool loading, bool baseline, bool useReplica, bool ready)
        {
            if (error != null) return "错误：" + error.Message;
            if (controller == null || loading) return "初始地图装载";
            if (region?.Loading == true || replica?.WaitingForBaseline == true) return "等待局部区域完整基线";
            if (baseline) return "等待完整基线";
            if (queue.HasPending || replica?.ProjectionRefreshPending == true) return "待安装变化格";
            if (controller.HasPendingPresentationWork) return "Dual Grid 规则/资源处理中";
            if (!(cave?.BackgroundReady ?? true)) return "岩壁/背景烘焙中（" + (cave?.RefreshPath ?? "DualGrid") + "）";
            if (useReplica && minerals?.Ready != true) return "矿层：" + minerals?.WaitReason;
            return ready ? "已绘制" : "等待相机完成回执";
        }
    }
}
