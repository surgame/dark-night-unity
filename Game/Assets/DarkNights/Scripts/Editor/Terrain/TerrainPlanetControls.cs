using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DarkNights.Runtime.Objects;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 同一航程草稿的原生属性编辑和星球目录；SerializedProperty 提供真实输入、Undo 与中文标签。
    /// 普通编辑只修改草稿，持久保存继续检查完整作者基线，不补齐另一份配置来源。
    /// </summary>
    public sealed class TerrainPlanetControls : IDisposable
    {
        private readonly TerrainGenerationPreview model;
        private readonly SerializedObject serialized;
        private readonly TerrainModifierConfigDrawer modifiers;

        public TerrainPlanetControls(TerrainGenerationPreview model)
        {
            this.model = model;
            serialized = new SerializedObject(model.Draft);
            modifiers = new TerrainModifierConfigDrawer(model.Draft);
        }

        public void DrawPicker(Action changed)
        {
            var rows = model.Draft.Config.Planets;
            if (rows == null || rows.Count == 0) { GUILayout.Label("没有星球，请在目录中新增。"); return; }
            int selected = Math.Max(0, rows.FindIndex(p => p != null && p.Id == model.PlanetId));
            int next = EditorGUILayout.Popup(selected, rows.Select(p => p == null ? "缺失配置" :
                p.DisplayName + (p.Enabled ? "" : "（停用）")).ToArray(), GUILayout.MinWidth(120));
            if (next != selected && rows[next] != null) { model.PlanetId = rows[next].Id; changed(); }
        }

        public void DrawMap(Action changed)
        {
            DrawFields(() =>
            {
                Field("Config.CaveMap.Seed", "预览／复现种子");
                var amplitude = serialized.FindProperty("Config.CaveMap.Amplitude");
                amplitude.doubleValue = EditorGUILayout.Slider("地表起伏", (float)amplitude.doubleValue, .3f, 1.6f);
            }, changed);
            modifiers.Draw(changed);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(model.Selected?.Seed)))
                if (GUILayout.Button("使用固定种子")) Edit(() => model.Draft.Config.CaveMap.Seed = model.Selected.Seed, changed);
            if (GUILayout.Button("新预览种子")) Edit(() => model.Draft.Config.CaveMap.Seed = Guid.NewGuid().ToString("N"), changed);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("将预览种子设为此星球固定种子") && model.Selected != null)
                Edit(() => model.Selected.Seed = model.Draft.Config.CaveMap.Seed, changed);
            EditorGUILayout.LabelField("正式开局：" + (string.IsNullOrEmpty(model.Selected?.Seed) ? "随机种子" :
                "固定 " + model.Selected.Seed), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("预览使用上方复现种子；已有地图和存档保持原格子。", EditorStyles.wordWrappedMiniLabel);
        }

        public void DrawLanding(Action changed)
        {
            DrawFields(() =>
            {
                PlanetField("DisplayName", "星球名称"); PlanetField("Description", "说明");
                PlanetField("Enabled", "启用此星球"); PlanetField("Seed", "固定种子（空为随机）");
                EditorGUILayout.Space(); EditorGUILayout.LabelField("地表与降落", EditorStyles.boldLabel);
                PlanetField("DockColumn", "泊位中心列（格）"); PlanetField("DockRow", "地表行（格）");
                PlanetField("LandingWidth", "泊位宽度（格）"); PlanetField("ArrivalHeight", "到达高度（逻辑像素）");
                PlanetField("HorizontalRange", "水平范围（逻辑像素）"); PlanetField("MaximumLift", "最大升高（逻辑像素）");
                EditorGUILayout.Space(); PlanetField("Id", "稳定 ID");
            }, changed);
        }

        public void DrawJourney(Action changed, Action play)
        {
            DrawFields(() =>
            {
                PlanetField("TransitionKind", "过场方式"); PlanetField("TransitSeconds", "最短过场（秒）");
                PlanetField("StarCount", "星点数量"); PlanetField("StarSpeed", "星点速度");
                ColorField("SpaceColorHex", "太空颜色"); ColorField("SkyColorHex", "天空颜色");
                EditorGUILayout.Space(); EditorGUILayout.LabelField("流程设置", EditorStyles.boldLabel);
                Field("Config.Enabled", "启用太空到星球流程");
                Field("Config.PreparationTimeoutSeconds", "准备超时（秒）"); Field("Config.ArrivalTimeoutSeconds", "到达超时（秒）");
            }, changed);
            if (GUILayout.Button("预览过场示意")) play();
        }

        public void DrawCatalog(Action changed)
        {
            EditorGUILayout.LabelField("星球目录", EditorStyles.boldLabel); DrawPicker(changed);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("新增")) { Add(false); changed(); }
            using (new EditorGUI.DisabledScope(model.Selected == null)) if (GUILayout.Button("复制")) { Add(true); changed(); }
            using (new EditorGUI.DisabledScope(model.Draft.Config.Planets.Count <= 1)) if (GUILayout.Button("移除")) { Remove(); changed(); }
            EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("上移")) { Move(-1); changed(); }
            if (GUILayout.Button("下移")) { Move(1); changed(); }
            EditorGUILayout.EndHorizontal();
        }

        public void Add(bool duplicate)
        {
            var rows = model.Draft.Config.Planets;
            if (rows.Count >= 32) throw new InvalidOperationException("星球最多 32 项。");
            Undo.RecordObject(model.Draft, duplicate ? "复制星球" : "新增星球");
            var planet = duplicate ? ExpeditionFlowDraft.Clone(model.Selected) : new PlanetPreset { DisplayName = "新星球" };
            int suffix = 1;
            while (rows.Any(p => p != null && p.Id == "planet-" + suffix)) suffix++;
            planet.Id = "planet-" + suffix; if (duplicate) planet.DisplayName += " 副本";
            rows.Add(planet); model.PlanetId = planet.Id;
            EditorUtility.SetDirty(model.Draft); serialized.Update();
        }

        public void Remove()
        {
            if (model.Draft.Config.Planets.Count <= 1) throw new InvalidOperationException("至少保留一颗星球。");
            Edit(() => { model.Draft.Config.Planets.Remove(model.Selected);
                model.PlanetId = model.Draft.Config.Planets.FirstOrDefault(p => p != null)?.Id; }, () => { });
        }

        public void Move(int direction)
        {
            var rows = model.Draft.Config.Planets;
            int current = rows.IndexOf(model.Selected), next = current + direction;
            if (current < 0 || next < 0 || next >= rows.Count) return;
            Edit(() => { var item = rows[current]; rows.RemoveAt(current); rows.Insert(next, item); }, () => { });
        }

        private void DrawFields(Action draw, Action changed)
        {
            int selected = model.Draft.Config.Planets.IndexOf(model.Selected);
            serialized.Update(); EditorGUI.BeginChangeCheck(); draw(); bool edited = EditorGUI.EndChangeCheck();
            bool applied = serialized.ApplyModifiedProperties();
            if (selected >= 0 && selected < model.Draft.Config.Planets.Count)
                model.PlanetId = model.Draft.Config.Planets[selected]?.Id;
            if (applied || edited) changed();
        }

        private void Field(string path, string label) => EditorGUILayout.PropertyField(serialized.FindProperty(path), new GUIContent(label), true);

        private SerializedProperty PlanetProperty(string name)
        {
            int index = model.Draft.Config.Planets.IndexOf(model.Selected);
            return index < 0 ? null : serialized.FindProperty("Config.Planets").GetArrayElementAtIndex(index).FindPropertyRelative(name);
        }

        private void PlanetField(string name, string label)
        {
            var property = PlanetProperty(name); if (property == null) return;
            if (name == "TransitionKind")
            {
                string[] values = { "star-shift", "fade", "none" };
                EditorGUI.BeginChangeCheck();
                int next = EditorGUILayout.Popup(label, Math.Max(0, Array.IndexOf(values, property.stringValue)),
                    new[] { "星点穿行", "淡出", "直接到达" });
                if (EditorGUI.EndChangeCheck()) property.stringValue = values[next];
            }
            else EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }

        private void ColorField(string name, string label)
        {
            var property = PlanetProperty(name); if (property == null) return;
            if (!ColorUtility.TryParseHtmlString(property.stringValue, out var color)) color = Color.black;
            EditorGUI.BeginChangeCheck(); color = EditorGUILayout.ColorField(label, color);
            if (EditorGUI.EndChangeCheck()) property.stringValue = "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        private void Edit(Action edit, Action changed)
        {
            Undo.RecordObject(model.Draft, "修改星球工作台草稿"); edit();
            EditorUtility.SetDirty(model.Draft); serialized.Update(); changed();
        }

        public void Dispose() { modifiers.Dispose(); serialized.Dispose(); }
    }
}
