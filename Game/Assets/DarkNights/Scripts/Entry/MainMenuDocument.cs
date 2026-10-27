using System;
using Cysharp.Threading.Tasks;
using GameCore.Objects.Runner.DI;
using GameCore.UI;
using Runtime.Utils;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>
    /// 主菜单文档与YYGC UIManager的应用级租约，复用现有Addressables、DI及独立运行时PanelSettings。
    /// 原TitleFont的Georgia视觉保持，中文仅用本地展示字体；释放先卸载面板，再释放字体和文档，不改作者字体。
    /// </summary>
    internal sealed class MainMenuDocument : IDisposable
    {
        private readonly UIManager manager = new UIManager();
        private GameObject owner;
        private PanelSettings settings;
        private FontAsset font, title;
        private bool loading, disposed, managerReady;
        internal MainMenuPanel Panel { get; private set; }

        internal async UniTask Initialize(Transform parent)
        {
            loading = true;
            try
            {
                var source = await AssetProvider.LoadAssetAsync<PanelSettings>("dark_nights.ui.main_menu_settings");
                if (disposed || parent == null) throw new OperationCanceledException("主菜单宿主已释放。");
                if (source == null || source.themeStyleSheet == null)
                    throw new InvalidOperationException("主菜单缺少原生PanelSettings或主题，请在工作台安装主菜单配置。");
                settings = UnityEngine.Object.Instantiate(source);
                owner = new GameObject("MainMenuUITK");
                owner.transform.SetParent(parent, false);
                var document = owner.AddComponent<UIDocument>();
                document.panelSettings = settings;
                document.rootVisualElement.pickingMode = PickingMode.Ignore;
                manager.Initialize(document.rootVisualElement, DIContainer.Root);
                managerReady = true;
                Panel = await manager.OpenAsync<MainMenuPanel>();
                if (disposed || parent == null) throw new OperationCanceledException("主菜单宿主已释放。");
                font = FontAsset.CreateFontAsset("Microsoft YaHei", "Regular", 48);
                title = FontAsset.CreateFontAsset("Georgia", "Bold", 87);
                if (font == null || title == null) throw new InvalidOperationException("主菜单中文或原Georgia标题字体不可用。");
                Panel.Root.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
                Panel.Root.Q("title").style.unityFontDefinition = FontDefinition.FromSDFFont(title);
            }
            catch { disposed = true; throw; }
            finally { loading = false; if (disposed) ReleaseDocument(); }
        }

        public void Dispose()
        {
            disposed = true;
            if (!loading) ReleaseDocument();
        }

        private void ReleaseDocument()
        {
            if (managerReady) { manager.Dispose(); managerReady = false; Panel = null; }
            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (settings != null) UnityEngine.Object.Destroy(settings);
            Release(font); Release(title); font = title = null;
            owner = null; settings = null;
        }

        private static void Release(FontAsset asset)
        {
            if (asset == null) return;
            foreach (var texture in asset.atlasTextures) if (texture != null) UnityEngine.Object.Destroy(texture);
            if (asset.material != null) UnityEngine.Object.Destroy(asset.material);
            UnityEngine.Object.Destroy(asset);
        }
    }
}
