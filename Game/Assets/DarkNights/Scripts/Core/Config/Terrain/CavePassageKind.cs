using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>隐藏拓扑边的通行状态；仅是生成时的地质分类，不构成客户端爆破授权。</summary>
    public enum CavePassageKind { Open, LooseFill, ThinRock, DeepRock }
}
