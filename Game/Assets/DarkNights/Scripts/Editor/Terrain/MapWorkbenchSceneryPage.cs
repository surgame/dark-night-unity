using System;
using System.Linq;
using DarkNights.View.Expedition;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>逐图装饰的UITK任务页；布局草稿独立保存，地形摘要不匹配则拒绝沿用旧锚点，不写共享生成输入。</summary>
    internal static class MapWorkbenchSceneryPage
    {
        internal static void Build(MapWorkbenchFields form)
        {
            var owner = form.Owner;
            var map = owner.Preview.Displayed;
            if (map?.Background == null || owner.Style == null)
            { form.Help("先加载完整地图，才能制作地表装饰。"); return; }
            var style = owner.Styles.Draft(owner.Style);
            var settings = style.SurfaceEnvironment;
            var source = SurfaceSceneryLayout.Find(settings, map.Background);
            form.Basic("本图植被与环境装饰");
            form.Note("布置树木与石块；独立草皮已停用，不包含采木、倒树、拾取或碰撞玩法。");
            CameraControls(form);
            if (source == null)
            {
                form.Help("当前地形尚未布置装饰。");
                int retained = settings.Layouts.Count(x => x != null);
                if (retained > 0) form.Help("保留了 " + retained + " 份其他地形的装饰布局及人工锚点，本图不应用；保存会保留这些记录，创建本图布局不会覆盖它们。",
                    HelpBoxMessageType.Warning);
                form.Button("为当前地图创建装饰布局", () =>
                {
                    owner.Commands.Guard();
                    var layout = SurfaceSceneryAuthoring.Create(map, owner.Generation.Selected.Freeze(), style, owner.Styles);
                    Undo.RecordObject(style, "注册本图装饰布局");
                    settings.Layouts = settings.Layouts.Concat(new[] { layout }).ToArray();
                    owner.Preview.RequestStyle(); owner.RebuildPage();
                    owner.Notice = "已创建本图装饰；使用顶部保存一起保存到配置。";
                });
                form.Advanced("植被与装饰");
                form.Note("创建本图布局后可编辑树木间距、石块间距和逐项锚点；其他地图的布局继续保留。");
                return;
            }
            var draft = owner.Styles.Draft(source);
            int active = draft.Placements.Count(p => p != null && !SurfaceSceneryLayout.IsGroundTrim(p));
            form.Note("布局：" + AssetDatabase.GetAssetPath(source) + "\n锚点（不含已停用草皮）：" + active +
                " · 地形摘要：" + source.TerrainKey.Substring(0, 12));
            form.StyleField(source, "Density", "重建密度");
            form.Advanced("植被与装饰");
            form.StyleField(source, "TreeSpacing", "树木间距（格）");
            form.StyleField(source, "StoneSpacing", "石块间距（格）");
            form.Note("密度与间距仅在显式重建时生效；调参不会自动替换人工锚点。");
            form.Button("按本图重建装饰草稿…", () =>
            {
                owner.Commands.Guard();
                if (!EditorUtility.DisplayDialog("重建装饰草稿",
                    "将替换当前布局草稿的全部锚点。原资产在保存前不改变，可Undo。", "重建草稿", "返回")) return;
                var next = SurfaceSceneryAuthoring.Build(map, owner.Generation.Selected.Freeze(), draft, style, owner.Styles);
                Undo.RecordObject(draft, "重建本图装饰"); draft.Placements = next;
                draft.Recipe = SurfaceSceneryLayout.DefaultRecipe;
                owner.Preview.RequestStyle(); owner.RebuildPage(); owner.Refresh();
            });
            form.StyleField(source, "Placements", "逐项编辑锚点／素材／开关");
            form.Note("装饰调整随顶部保存一起写入当前配置；旧草皮条目保留，但不再显示。");
        }

        internal static void CameraControls(MapWorkbenchFields form)
        {
            form.Note("在同一画布拖拽平移、滚轮缩放；画布上方的四角图标可适配画布。");
        }
    }
}
