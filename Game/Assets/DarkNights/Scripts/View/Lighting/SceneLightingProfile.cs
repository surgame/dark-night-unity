using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DarkNights.View.Lighting
{
    /// <summary>关卡相机的可保存照明配置；统一拥有后端资源和场景参数，不从首个光源推断，不含单灯或角色状态。</summary>
    [CreateAssetMenu(menuName = "Dark Nights/Lighting/场景光照配置", fileName = "SceneLightingProfile")]
    public sealed class SceneLightingProfile : ScriptableObject
    {
        public ComputeShader LightingShader;
        public Light2D UrpLightTemplate;
        public ShadowCaster2D UrpShadowTemplate;
        public ExplorationLightSettings Settings = new ExplorationLightSettings();
        public LightProfile DevicePreset;
        public LightEffect DefaultEffectTemplate;

        public void Validate()
        {
            if (LightingShader == null || UrpLightTemplate == null || UrpShadowTemplate == null ||
                Settings == null || DevicePreset == null || DefaultEffectTemplate == null)
                throw new InvalidOperationException("场景光照配置缺少渲染资源、参数或设备光预设：" + name);
            Settings.Validate(); DevicePreset.Freeze(); DefaultEffectTemplate.ValidateStructure();
        }
    }
}
