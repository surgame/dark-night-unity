using DarkNights.Core.Config.Terrain;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>地形编辑回执；只反馈短事务结果，不把地图状态复制到第二个业务所有者。</summary>
    public readonly struct TerrainActionResult
    {
        public string RequestId { get; }
        public TerrainEditAction Action { get; }
        public ulong CommitId { get; }
        public bool Accepted { get; }
        public string Reason { get; }
        public TerrainActionResult(string requestId, TerrainEditAction action, ulong commitId, bool accepted, string reason)
        { RequestId = requestId; Action = action; CommitId = commitId; Accepted = accepted; Reason = reason ?? ""; }
    }
}
