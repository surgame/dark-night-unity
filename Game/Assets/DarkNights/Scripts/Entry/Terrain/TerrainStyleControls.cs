using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.View.Terrain;
using UnityEngine;

namespace DarkNights.Entry.Terrain
{
    /// <summary>岩壁与背景分页；每次只展开一个资产或背景层，编辑原资产的独立草稿，不在 OnGUI 写盘。</summary>
    public sealed class TerrainStyleControls
    {
        private int rockPage, backgroundPage;
        private string expandedPicker;
        private readonly Dictionary<string, int> expandedModifier = new Dictionary<string, int>();
        public void Draw(TerrainDebugPanel panel, bool background)
        {
            var draft = panel.Bootstrap.StyleDraft;
            if (draft?.Style == null) { GUILayout.Label("旧版地图没有岩层样式，请打开新版工作台。"); return; }
            GUILayout.Label("样式草稿 · " + draft.Source.name);
            if (!background)
            {
                rockPage = GUILayout.Toolbar(rockPage, new[] { "外轮廓", "前景造型", "刷新" });
                if (rockPage == 0) TerrainFieldControls.Draw(draft, draft.Source, panel.StyleChanged,
                    name => name.StartsWith("Outline") || name == "StoneSize");
                else if (rockPage == 1)
                {
                    if (draft.Style.ImmediateForeground) GUILayout.Label("局部刷新支持空栈或单个圆簇。使用其他组合前请在“刷新”页关闭局部前景刷新。");
                    Modifiers(panel, draft.Style.Modifiers, value => draft.Style.Modifiers = value, "前景");
                }
                else TerrainFieldControls.Draw(draft, draft.Source, panel.StyleChanged,
                    name => name == "ProceduralRock" || name == "ImmediateForeground" || name == "InteractiveBakeBudgetMs");
            }
            else
            {
                backgroundPage = GUILayout.Toolbar(backgroundPage, new[] { "总览", "近层", "中层", "深层" });
                var layer = draft.Background;
                if (layer == null) { GUILayout.Label("当前样式未配置背景。"); return; }
                if (backgroundPage == 0)
                {
                    TerrainFieldControls.Draw(draft, draft.Style.Background, panel.StyleChanged);
                    if (Pick("点缀生成器", layer.Generator, out CaveBackgroundGeneratorAsset generator))
                    { layer.Generator = generator; panel.StyleChanged(); }
                    TerrainFieldControls.Draw(draft, layer.Generator, panel.StyleChanged, name => name == "Seed");
                }
                else
                {
                    string prefix = backgroundPage == 1 ? "Near" : backgroundPage == 2 ? "Middle" : "Deep";
                    TerrainFieldControls.Draw(draft, layer.Generator, panel.StyleChanged, name => name.StartsWith(prefix));
                    var modifiers = backgroundPage == 1 ? layer.NearModifiers : backgroundPage == 2 ? layer.MiddleModifiers : layer.DeepModifiers;
                    Modifiers(panel, modifiers, value =>
                    {
                        if (backgroundPage == 1) layer.NearModifiers = value;
                        else if (backgroundPage == 2) layer.MiddleModifiers = value;
                        else layer.DeepModifiers = value;
                    }, prefix);
                }
            }
        }
        private void Modifiers(TerrainDebugPanel panel, CaveModifierAsset[] sources, Action<CaveModifierAsset[]> assign, string key)
        {
            sources = sources ?? Array.Empty<CaveModifierAsset>();
            GUILayout.Label("造型栈 · " + sources.Length + "/8");
            if (!expandedModifier.TryGetValue(key, out int expanded)) expanded = 0;
            if (sources.Length == 0) GUILayout.Label("没有造型层，保持原轮廓。");
            for (int i = 0; i < sources.Length; i++)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button((expanded == i ? "▾ " : "▸ ") + (i + 1) + ". " + (sources[i] == null ? "空" : sources[i].name)))
                { expanded = expanded == i ? -1 : i; expandedModifier[key] = expanded; }
                if (GUILayout.Button("移除", GUILayout.Width(52)))
                { assign(sources.Where((_, n) => n != i).ToArray()); panel.StyleChanged(); GUIUtility.ExitGUI(); }
                GUILayout.EndHorizontal();
                if (expanded == i)
                {
                    if (Pick(key + "造型" + i, sources[i], out CaveModifierAsset selected))
                    { var copy = (CaveModifierAsset[])sources.Clone(); copy[i] = selected; assign(copy); panel.StyleChanged(); }
                    TerrainFieldControls.Draw(panel.Bootstrap.StyleDraft, sources[i], panel.StyleChanged);
                }
                GUILayout.EndVertical();
            }
            if (sources.Length < 8 && Pick(key + "添加造型", null, out CaveModifierAsset added) && added != null)
            { assign(sources.Concat(new[] { added }).ToArray()); panel.StyleChanged(); }
        }
        public bool Pick<T>(string label, T current, out T result) where T : ScriptableObject
        {
            result = current;
            if (GUILayout.Button(label + "：" + (current == null ? "选择…" : current.name)))
                expandedPicker = expandedPicker == label ? null : label;
            if (expandedPicker != label) return false;
            var choices = TerrainWorkbenchAssets.Query?.Invoke(typeof(T)) ?? Resources.FindObjectsOfTypeAll<T>();
            GUILayout.BeginVertical(GUI.skin.box);
            bool changed = false;
            if (GUILayout.Button("无")) { result = null; changed = true; }
            foreach (var asset in choices)
                if (asset is T candidate && candidate.hideFlags == HideFlags.None && GUILayout.Button(candidate.name))
                { result = candidate; changed = true; }
            GUILayout.EndVertical();
            if (changed) expandedPicker = null;
            return changed;
        }
    }
}
