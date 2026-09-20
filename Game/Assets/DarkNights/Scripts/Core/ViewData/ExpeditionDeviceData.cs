using System;
using System.Collections.Generic;

namespace DarkNights.Core.ViewData
{
    /// <summary>远征的冻结数据合同；仅供投影和保存，构造时复制集合，不拥有权威状态。</summary>
    public sealed class ExpeditionDeviceData
    {
        public int Id { get; }
        public float Height { get; }
        public int Iron { get; }
        public int Gold { get; }
        public int Stage { get; }
        public int ParentId { get; }
        public float TargetX { get; }
        public float TargetHeight { get; }
        public bool Powered { get; }
        public ExpeditionDeviceData(int id, float height, int iron, int gold, int stage, int parentId, float targetX, float targetHeight, bool powered)
        {
            Id = id;
            Height = height;
            Iron = iron;
            Gold = gold;
            Stage = stage;
            ParentId = parentId;
            TargetX = targetX;
            TargetHeight = targetHeight;
            Powered = powered;
        }
    }
}
