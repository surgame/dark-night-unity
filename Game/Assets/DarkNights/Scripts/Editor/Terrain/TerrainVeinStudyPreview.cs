using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// Tuner矿巢制作页的唯一预览所有者；读取当前完整地图生成冻结局部候选，控制有限时间回放。
    /// 草稿失效时保留旧候选并明确标记，重建成功才替换；不启动会话、Play、AI或玩家存储。
    /// </summary>
    internal sealed class TerrainVeinStudyPreview : IDisposable
    {
        private readonly TerrainVeinStudyCanvas canvas = new TerrainVeinStudyCanvas();
        private TerrainVeinStudyLayout layout;
        private string identity, failedIdentity, message = "开启后从当前地图选择近壁样点。";
        private double previous;
        private float time;
        private int decision;
        private bool playing;
        private static readonly string[] Shapes = { "厚团矿体", "分叉主脉", "邻接矿团", "错层厚带" };
        internal bool Enabled => TerrainVeinStudySettings.instance.Enabled;
        internal TerrainVeinStudyLayout Layout => layout;
        internal string Message => message;
        private string CurrentIdentity(TerrainGenerationPreview model)
        {
            var value = TerrainVeinStudySettings.instance;
            return model.Identity + "|" + value.Shape + "|" + value.Capacity + "|" + value.HasNest +
                "|" + value.SiteIndex + "|" + value.Variation;
        }

        internal void DrawControls(TerrainGenerationPreview model, bool productionActions = true)
        {
            var settings = TerrainVeinStudySettings.instance;
            using (var serialized = new SerializedObject(settings))
            {
                serialized.Update();
                Field(serialized, "Enabled", "启用矿巢制作预览");
                if (productionActions)
                { Field(serialized, "JointVeinCount", "完整地图矿脉上限"); Field(serialized, "JointNests", "完整地图包含侧巢"); }
                using (new EditorGUI.DisabledScope(!Enabled))
                {
                    var shape = serialized.FindProperty("Shape");
                    shape.enumValueIndex = EditorGUILayout.Popup("矿脉形态", shape.enumValueIndex, Shapes);
                    Field(serialized, "Capacity", "总储量（试参）"); Field(serialized, "HasNest", "包含巢穴");
                    Field(serialized, "SiteIndex", "近壁样点编号"); Field(serialized, "Variation", "形态变化编号");
                    Field(serialized, "XRay", "透视前景墙"); Field(serialized, "Automatic", "参数变化自动预览");
                }
                if (serialized.ApplyModifiedProperties()) { failedIdentity = null; playing = false; }
            }
            EditorGUILayout.HelpBox("当前地图格级制作剖面。前景负责通行，背景矿独立；本页参数只保存为本机工具预设。", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Enabled || model.LastCandidate == null || model.LastCandidateIdentity != model.Identity))
                if (GUILayout.Button("生成／更新矿巢候选")) Build(model);
            if (GUILayout.Button("保存本机工具预设")) settings.SavePreset();
            if (productionActions)
            {
                if (GUILayout.Button("预览完整联合候选")) { model.Generate(); }
                using (new EditorGUI.DisabledScope(model.HasChanges))
                    if (GUILayout.Button("生成新联合地图并用于新局"))
                    {
                        try { var map = model.ExportJoint();
                            message = "新局地图已冻结：" + AssetDatabase.GetAssetPath(map) + "；现有游戏和旧地图保持。"; }
                        catch (Exception error) { message = "未装配：" + error.Message; }
                    }
            }
            EditorGUILayout.LabelField(settings.HasChanges ? "● 工具预设尚未保存" : "工具预设已保存", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
            if (!Enabled || layout == null) return;
            if (identity != CurrentIdentity(model)) EditorGUILayout.HelpBox("参数已变化；当前仍显示上一候选。", MessageType.Warning);
            EditorGUILayout.LabelField("样点 " + settings.SiteIndex + " / " + layout.SiteCount +
                " · 世界格 " + layout.WorldX + ", " + layout.WorldY, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(layout.MineralCells + " 矿格 · " + layout.TotalCapacity +
                " 总储量 · " + layout.Deposits.Count + " 矿床分片", EditorStyles.wordWrappedMiniLabel);
            DrawPlayback();
        }

        private static void Field(SerializedObject serialized, string path, string label) =>
            EditorGUILayout.PropertyField(serialized.FindProperty(path), new GUIContent(label));

        private void DrawPlayback()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("独立动作：拆墙 → 采矿 → 破巢／后退", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(time >= 14 || time >= 6 && decision == 0))
                if (GUILayout.Button(playing ? "暂停" : "播放")) { playing = !playing; previous = EditorApplication.timeSinceStartup; }
            if (GUILayout.Button("重播")) { time = 0; decision = 0; playing = false; }
            EditorGUILayout.EndHorizontal();
            float next = EditorGUILayout.Slider("时间（秒）", time, 0, 14);
            if (next != time) { playing = false; time = decision == 0 ? Mathf.Min(6, next) : next; }
            if (time >= 6 && decision == 0)
            {
                if (GUILayout.Button("继续挖墙，打开后方空间")) { decision = 1; playing = true; previous = EditorApplication.timeSinceStartup; }
                if (GUILayout.Button("保留薄墙，带矿后退")) { decision = 2; playing = true; previous = EditorApplication.timeSinceStartup; }
            }
            var frame = new TerrainVeinStudyFrame(layout, time, decision);
            EditorGUILayout.LabelField("拆墙 " + frame.RemovedWalls + " 格 · 已采 " + frame.Harvested +
                " · 余矿 " + (layout.TotalCapacity - frame.Harvested), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(frame.Phase, EditorStyles.wordWrappedLabel);
        }

        internal void Tick(TerrainGenerationPreview model, bool visible)
        {
            if (!Enabled || !visible) { playing = false; return; }
            string current = CurrentIdentity(model);
            if (TerrainVeinStudySettings.instance.Automatic && current != identity && current != failedIdentity &&
                model.LastCandidate != null && model.LastCandidateIdentity == model.Identity) Build(model);
            if (!playing) return;
            double now = EditorApplication.timeSinceStartup;
            time += (float)Math.Min(.1, now - previous); previous = now;
            if (time >= 6 && decision == 0) { time = 6; playing = false; }
            if (time >= 14) { time = 14; playing = false; }
        }

        private void Build(TerrainGenerationPreview model)
        {
            var settings = TerrainVeinStudySettings.instance;
            try
            {
                var candidate = TerrainVeinStudyGenerator.Generate(model.LastCandidate, settings.Shape,
                    settings.Capacity, settings.HasNest, settings.SiteIndex, settings.Variation);
                layout = candidate; identity = CurrentIdentity(model); failedIdentity = null;
                time = 0; decision = 0; playing = false;
                message = "候选已生成；作者地图／原矿层保持，巢腔仅在本制作候选中预留。";
            }
            catch (Exception error) { failedIdentity = CurrentIdentity(model); message = "未生成：" + error.Message; }
        }

        internal void Draw(Rect area)
        {
            if (layout == null) { GUI.Label(area, message, EditorStyles.centeredGreyMiniLabel); return; }
            canvas.Draw(area, layout, new TerrainVeinStudyFrame(layout, time, decision), TerrainVeinStudySettings.instance.XRay);
        }

        public void Dispose() { playing = false; canvas.Dispose(); }
    }
}
