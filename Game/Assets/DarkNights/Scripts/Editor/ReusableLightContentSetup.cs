using System;
using DarkNights.View.Lighting;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>共用光效的显式制作与结构核验入口；既有作者资源不被重新初始化，持久配置迁移由专用有限批次执行。</summary>
    public static class ReusableLightContentSetup
    {
        public const string EffectPath = "Assets/DarkNights/Res/Shared/Lighting/LightEffect.prefab";
        public const string HeadPath = "Assets/DarkNights/Res/Shared/Lighting/HeadMountedLightEffect.prefab";
        [MenuItem("Dark Nights/Tools/安装可复用照明与手电道具")]
        public static void Install() => Lighting.LightProfileMigration.Apply();
        public static void Validate()
        {
            foreach (string path in new[] { EffectPath, HeadPath })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var effect = prefab != null ? prefab.GetComponent<LightEffect>() : null;
                if (effect == null) throw new InvalidOperationException("缺少共用光效模板：" + path);
                effect.ValidateStructure();
            }
            FlashlightContentSetup.Validate(); Lighting.LightProfileMigration.Validate();
        }
    }
}
