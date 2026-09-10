using System;
using GameCore.Objects.Behaviours.Interfaces;

namespace YYGC.IdentityValidation
{
    /// <summary>正式 Player 中用于区分共用 Prefab 的两个定义配置。</summary>
    [Serializable]
    public sealed class IdentityProbeConfig : IConfigData
    {
        public string Name { get; set; }
        public int Marker;
    }
}
