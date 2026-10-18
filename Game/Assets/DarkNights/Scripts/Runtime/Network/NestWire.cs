using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>巢穴冻结投影的有界传输记录；接收立即通过Core构造器验证，客户端不从时钟推进业务。</summary>
    [MemoryPackable]
    public partial class NestWire
    {
        public int Id { get; set; }
        public int U { get; set; }
        public int Row { get; set; }
        public int Phase { get; set; }
        public double Clock { get; set; }
        public float X { get; set; }
        public float Height { get; set; }
        public int Hp { get; set; }
        public static NestWire From(NestViewData n) => new NestWire { Id = n.Id, U = n.U, Row = n.Row,
            Phase = n.Phase, Clock = n.Clock, X = n.X, Height = n.Height, Hp = n.Hp };
        public NestViewData Freeze() => new NestViewData(Id, U, Row, Phase, Clock, X, Height, Hp);
    }
}
