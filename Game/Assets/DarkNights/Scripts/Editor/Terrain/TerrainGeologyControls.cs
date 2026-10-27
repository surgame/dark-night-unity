using System;
using System.Linq;
using DarkNights.Runtime.Objects;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>既有地图草稿中的一点塌方作者面板；字段、选点与原生Undo写同一ExpeditionFlowDraft，显式保存及整图冻结沿用现有入口。</summary>
    internal sealed class TerrainGeologyControls
    {
        private int selection;
        private string message = "先指定人工点，预览无冲突后保存并冻结同一候选。";
        private static readonly string[] Selections = { "只查看", "源岩左下格", "落地基准格", "支撑格", "触发区左下格", "退避区左下格", "允许改形左下格" };
        internal bool Picking => selection != 0;
        internal bool ShowOverlay { get; private set; } = true;
        internal void Draw(TerrainGenerationPreview model, Action changed)
        {
            using (var serialized = new SerializedObject(model.Draft))
            {
                serialized.Update();
                void Field(string field, string label, string tip = "") => EditorGUILayout.PropertyField(
                    serialized.FindProperty("Config.Geology." + field), new GUIContent(label, tip), true);
                Field("Enabled", "启用此塌方点", "关闭后不附加站点，也不改动输入地图。");
                Field("SiteId", "稳定SiteId"); Field("Definition", "效果Definition", "引用现有Geology事件定义；本图坐标不会写回样板定义。");
                using (new EditorGUI.DisabledScope(model.Draft.Config.Geology?.Definition == null))
                    if (GUILayout.Button("读取Definition参数为本图草稿"))
                    {
                        Undo.RecordObject(model.Draft, "读取塌方配方");
                        var definition = model.Draft.Config.Geology.Definition;
                        model.Draft.Config.Geology.Configuration = ExpeditionFlowDraft.Clone(definition.SharedConfigs.OfType<GeologyEventConfig>().Single());
                        serialized.Update(); changed();
                    }
                Field("Configuration.EnterRegion", "角色进入触发区", "关闭时使用支撑格被真正挖空触发。");
                Field("Configuration.SupportU", "支撑U"); Field("Configuration.SupportV", "支撑V");
                Field("Configuration.RegionU", "触发区U"); Field("Configuration.RegionV", "触发区V");
                Field("Configuration.RegionWidth", "触发区宽（格）"); Field("Configuration.RegionHeight", "触发区高（格）");
                Field("Configuration.SourceU", "源岩左下U"); Field("Configuration.SourceV", "源岩左下V");
                Field("Configuration.Width", "源岩宽（格）"); Field("Configuration.SourceRows", "源岩高（格）");
                Field("Configuration.LandingU", "落地基准U"); Field("Configuration.LandingV", "落地基准V");
                Field("Configuration.RubbleWidth", "允许散落宽（格）");
                Field("AllowedU", "允许改形U"); Field("AllowedV", "允许改形V");
                Field("AllowedWidth", "允许改形宽（格）"); Field("AllowedHeight", "允许改形高（格）");
                Field("RetreatU", "退避区U"); Field("RetreatV", "退避区V");
                Field("RetreatWidth", "退避区宽（格）"); Field("RetreatHeight", "退避区高（格）");
                Field("Configuration.WarningSeconds", "前兆（秒）"); Field("Configuration.Gravity", "重力（像素/秒²）");
                Field("Configuration.FragmentDelay", "碎岩错峰（秒）"); Field("Configuration.OccupancyWaitSeconds", "占位等待（秒）");
                Field("Association", "洞室／矿脉／巢位关联", "可选关联备注，使用目标稳定身份；首片不据此自动布点。");
                if (serialized.ApplyModifiedProperties()) { changed(); }
            }
            EditorGUILayout.HelpBox("逻辑格U向右、V向上，地下V为负。源岩／落点仅支持完整slate与平地；源岩U须等于落地U。", MessageType.Info);
            ShowOverlay = EditorGUILayout.Toggle("显示塌方设计叠层", ShowOverlay);
            selection = EditorGUILayout.Popup("画布左键选点", selection, Selections);
            if (GUILayout.Button("更新完整联合候选"))
            {
                changed();
            }
            EditorGUILayout.HelpBox(model.GeologyConflict.Length == 0 ? "候选检查：" +
                (model.CandidateFresh ? "通过（静态空间）" : "待更新完整联合候选") :
                "候选冲突：" + model.GeologyConflict, model.GeologyConflict.Length == 0 ? MessageType.Info : MessageType.Error);
            EditorGUILayout.HelpBox("源岩＝蓝，落地包络＝棕，进入区＝橙，退避区＝绿，允许改形＝灰；均为编辑叠层。", MessageType.None);
            if (GUILayout.Button("保存地图与航程草稿"))
            {
                try { model.Apply(); message = "地图草稿已保存；下一步冻结当前完整候选。"; }
                catch (Exception error) { message = error.Message; }
            }
            if (GUILayout.Button("生成新联合地图并用于新局"))
            {
                try { message = "已冻结：" + AssetDatabase.GetAssetPath(model.ExportJoint()); }
                catch (Exception error) { message = error.Message; }
            }
            EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
        }

        internal void Pick(TerrainGenerationPreview model, Vector2Int cell, Action changed)
        {
            if (!Picking) return;
            Undo.RecordObject(model.Draft, "设置人工塌方点坐标");
            var point = model.Draft.Config.Geology; var config = point.Configuration;
            int u = cell.x, v = -cell.y;
            if (selection == 1) { config.SourceU = u; config.SourceV = v; }
            else if (selection == 2) { config.LandingU = u; config.LandingV = v; }
            else if (selection == 3) { config.SupportU = u; config.SupportV = v; }
            else if (selection == 4) { config.RegionU = u; config.RegionV = v; }
            else if (selection == 5) { point.RetreatU = u; point.RetreatV = v; }
            else { point.AllowedU = u; point.AllowedV = v; }
            EditorUtility.SetDirty(model.Draft); changed();
        }
    }
}
