using System;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地图工作台独占的中文字体及粗体页签；复用本机字体，不写入或修改作者字体资源。
    /// 字体图集随当前UITK视图创建、释放，窗口重建和域重载不累积动态材质或纹理。
    /// </summary>
    internal sealed class MapWorkbenchTypography : IDisposable
    {
        private FontAsset body;
        private FontAsset bold;
        private VisualElement root;

        internal void Apply(VisualElement target)
        {
            root = target;
            string[] installed = Font.GetOSInstalledFontNames();
            foreach (string family in new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans SC", "Noto Sans CJK SC" })
            {
                if (Array.IndexOf(installed, family) < 0) continue;
                body = FontAsset.CreateFontAsset(family, "Regular", 48);
                if (body == null) continue;
                bold = FontAsset.CreateFontAsset(family, "Bold", 48);
                Retain();
                root.style.unityFontDefinition = FontDefinition.FromSDFFont(body);
                if (bold != null)
                    root.Query<Button>(className: "map-module").ForEach(button =>
                        button.style.unityFontDefinition = FontDefinition.FromSDFFont(bold));
                return;
            }
        }

        internal void Retain()
        {
            Retain(body); Retain(bold);
        }

        private static void Retain(FontAsset asset)
        {
            if (asset == null) return;
            asset.hideFlags = HideFlags.HideAndDontSave;
            if (asset.material != null) asset.material.hideFlags = HideFlags.HideAndDontSave;
            foreach (var texture in asset.atlasTextures)
                if (texture != null) texture.hideFlags = HideFlags.HideAndDontSave;
        }

        public void Dispose()
        {
            if (root != null)
            {
                root.style.unityFontDefinition = StyleKeyword.Null;
                root.Query<Button>(className: "map-module").ForEach(button =>
                    button.style.unityFontDefinition = StyleKeyword.Null);
            }
            Release(body); Release(bold);
            body = bold = null; root = null;
        }

        private static void Release(FontAsset asset)
        {
            if (asset == null) return;
            foreach (var texture in asset.atlasTextures)
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            if (asset.material != null) UnityEngine.Object.DestroyImmediate(asset.material);
            UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}
