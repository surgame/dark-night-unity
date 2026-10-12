using System;
using System.IO;
using DarkNights.View.Lighting;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>验收后的指定 Pinewatch 资产显式迁移；只切换返光模式，保留作者参数和 GUID，普通导入／启动不运行迁移。</summary>
    public static class WallDiffuseMigration
    {
        [MenuItem("Dark Nights/Tools/启用灰松谷连续墙面漫反射")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("退出 Play 后迁移指定场景配置。");
            var asset = AssetDatabase.LoadAssetAtPath<SceneLightingProfile>(LightProfileMigration.ScenePath);
            if (asset == null) throw new InvalidOperationException("灰松谷光照配置不存在。");
            if (EditorUtility.IsDirty(asset)) throw new InvalidOperationException("场景光照配置有未保存的作者修改。");
            asset.Validate();
            if (asset.Settings.BounceMode == LightBounceMode.Continuous) return;
            string backup = Path.GetFullPath("../artifacts/wall-diffuse-20261012/migration");
            Directory.CreateDirectory(backup);
            string target = Path.Combine(backup, "PinewatchLighting-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
            File.WriteAllText(target, JsonUtility.ToJson(asset, true));
            Undo.RecordObject(asset, "启用连续墙面漫反射"); asset.Settings.BounceMode = LightBounceMode.Continuous;
            asset.Validate(); EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
            LightingEditorRefresh.Request();
        }
    }
}
