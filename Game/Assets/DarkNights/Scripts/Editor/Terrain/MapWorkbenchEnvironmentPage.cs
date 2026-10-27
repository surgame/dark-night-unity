using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>环境常用参数与真实素材层；高级逐层参数保持原序列化结构，不用虚构的层数滑杆替代素材。</summary>
    internal sealed class MapWorkbenchEnvironmentPage : IDisposable
    {
        private readonly SerializedObject data;
        internal MapWorkbenchEnvironmentPage(MapWorkbenchFields form)
        {
            var owner = form.Owner;
            form.Title("环境", "当前配置的天空、远景和植被；使用顶部保存一起保存。");
            if (owner.Style == null) { form.Help("尚无岩壁表现配置。"); return; }
            var working = owner.Styles.Draft(owner.Style);
            form.Basic("天空与远景");
            form.StyleField(owner.Style, "SurfaceEnvironment.Enabled", "显示环境");
            form.StyleField(owner.Style, "SurfaceEnvironment.Clouds", "云层");
            var layers = working.SurfaceEnvironment.Layers;
            data = new SerializedObject(working);
            for (int i = 0; i < layers.Length; i++)
                if (layers[i] != null)
                    form.StyleField(owner.Style, "SurfaceEnvironment.Layers.Array.data[" + i + "].Enabled",
                        (i + 1) + " · " + (layers[i].Texture == null ? "缺少素材" : layers[i].Texture.name));
            form.Note("可逐层启用；素材、裁片、像素密度、视差及颜色位于高级配置。");
            form.Button("布置树木与石块", () =>
            { owner.State.Extra = "scenery"; owner.SelectPage("extras"); });
            form.Advanced("天空与远景");
            form.StyleField(owner.Style, "SurfaceEnvironment.CloudHeight", "云层高度");
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) continue;
                string path = "SurfaceEnvironment.Layers.Array.data[" + i + "]";
                string title = layers[i].Texture == null ? "缺少素材" : layers[i].Texture.name;
                var advanced = form.Fold(title + " · 素材与精确参数");
                foreach (string name in new[] { "Texture", "Pixels", "PixelScale", "BottomCells",
                    "HorizontalParallax", "VerticalParallax", "Tint" })
                {
                    var property = data.FindProperty(path + "." + name);
                    var field = new PropertyField(property); field.BindProperty(property); advanced.Add(field);
                    field.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
                    { owner.Preview.RequestEnvironment(); owner.Refresh(); });
                }
            }
            form.Note("可逐层启用；山林只显示原裁片范围，下方留空。云层独立。");
            form.Button("岩壁与地下背景高级设置", () => owner.SelectPage("appearance"));
        }
        public void Dispose() => data?.Dispose();
    }
}
