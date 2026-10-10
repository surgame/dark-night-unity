using System;
using GameCore.Objects.Behaviours.Interfaces;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkNights.Runtime.Objects
{
    /// <summary>照明道具的 Definition 配置；声明配发规则、可选光照预设及制作覆盖，无预设时使用内置基线，不拥有运行开关、挂点或装备状态。</summary>
    [Serializable]
    public sealed class FlashlightToolConfig : IConfigData
    {
        public string Name => "照明道具能力";
        [HideInInspector] public bool Starter = true;
        [HideInInspector] public AssetReference Profile = new AssetReference("");
        [LightConfiguration] public LightOverrides Overrides = new LightOverrides();
    }
}
