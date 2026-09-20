using System.Linq;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>远征冻结合同的有界网络字段；接收后立即复制，不能参与玩法计算。</summary>
    [MemoryPackable]
    public partial class ExpeditionDeviceWire
    {
        public int Id { get; set; }
        public float Height { get; set; }
        public int Iron { get; set; }
        public int Gold { get; set; }
        public int Stage { get; set; }
        public int ParentId { get; set; }
        public float TargetX { get; set; }
        public float TargetHeight { get; set; }
        public bool Powered { get; set; }
        public static ExpeditionDeviceWire From(ExpeditionDeviceData v) => v == null ? null : new ExpeditionDeviceWire
        {
            Id = v.Id,
            Height = v.Height,
            Iron = v.Iron,
            Gold = v.Gold,
            Stage = v.Stage,
            ParentId = v.ParentId,
            TargetX = v.TargetX,
            TargetHeight = v.TargetHeight,
            Powered = v.Powered,
        };
        public ExpeditionDeviceData Freeze() => new ExpeditionDeviceData(Id, Height, Iron, Gold, Stage, ParentId, TargetX, TargetHeight, Powered);
    }
}
