using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 退出日常制图主流程的兼容与实验页；保留星球航程、Modifier及本机试参，不复制另一份作者模型。
    /// 仅原有复杂兼容绘制器及实验回放保留IMGUI，生产来源、矿物、额外设计和冻结出口均为原生UITK。
    /// </summary>
    internal sealed class TerrainWorkbenchAdvancedPage : IDisposable
    {
        private readonly TerrainWorkbenchFields form;
        private TerrainPlanetControls planets;
        private TerrainModifierConfigDrawer modifiers;
        internal TerrainWorkbenchAdvancedPage(TerrainWorkbenchFields form)
        {
            this.form = form; var owner = form.Owner;
            form.Title("高级与兼容", "这些能力仍保留，但不混入地图制作主流程；退出主流程不等于删除资源。");
            string[] ids = { "compat", "planet", "paint", "study" };
            var picker = new DropdownField("工具范围", new List<string>
            { "兼容与生成步骤", "星球、泊位与航程", "预览拆填实验", "本机矿巢实验" },
                Math.Max(0, Array.IndexOf(ids, owner.State.AdvancedPage)));
            picker.RegisterValueChangedCallback(change =>
            {
                owner.State.AdvancedPage = ids[picker.index];
                owner.State.ShowStudy = ids[picker.index] == "study";
                owner.State.ShowJourney = false;
                owner.Preview.UsePan(); owner.RebuildPage();
            });
            form.Root.Add(picker);
            if (owner.State.AdvancedPage == "planet") BuildPlanets();
            else if (owner.State.AdvancedPage == "paint") BuildPaint();
            else if (owner.State.AdvancedPage == "study") BuildStudy();
            else BuildCompatibility();
        }

        private void BuildCompatibility()
        {
            var owner = form.Owner;
            form.Heading("原生成步骤");
            if (owner.Generation.RuleMode && owner.Draft.Config.CopyModifiers().All(m => m is EntranceWalkwayModifierConfig))
                form.Help("新规则使用模板入口，旧入口步道不参与本模式；其参数隐藏但原配置保留。");
            else
            {
                modifiers = new TerrainModifierConfigDrawer(owner.Draft);
                form.Root.Add(new IMGUIContainer(() =>
                {
                    using (new EditorGUI.DisabledScope(owner.Preview.Map.HasChanges))
                        modifiers.Draw(owner.DraftChanged);
                }));
            }
            if (!owner.Generation.RuleMode)
            {
                form.Heading("旧来源的完整联合配方");
                form.Note("仅兼容旧作者输入；参数归本机预设，最终版本记录真实配方。新规则不消费它们。");
                form.Root.Add(new IMGUIContainer(() =>
                {
                    var settings = TerrainVeinStudySettings.instance;
                    using (new EditorGUI.DisabledScope(owner.Preview.Map.HasChanges))
                    {
                        EditorGUI.BeginChangeCheck();
                        int count = EditorGUILayout.IntSlider("新矿脉上限", settings.JointVeinCount, 1, 16);
                        bool nests = EditorGUILayout.Toggle("包含侧巢", settings.JointNests);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(settings, "修改旧联合配方");
                            settings.JointVeinCount = count; settings.JointNests = nests;
                            EditorUtility.SetDirty(settings); owner.DraftChanged();
                        }
                    }
                }));
                form.Button("保存本机配方预设", () => TerrainVeinStudySettings.instance.SavePreset());
            }
            form.Button("更新完整候选", owner.Commands.Generate);
            form.Button("检查与使用 →", () => owner.SelectPage("release"));
        }

        private void BuildPlanets()
        {
            var owner = form.Owner;
            planets = new TerrainPlanetControls(owner.Generation);
            form.Note("地图与泊位仍共用原作者草稿；修改后候选过期。航程不再占用日常地图导航。");
            form.Root.Add(new IMGUIContainer(() =>
            {
                using (new EditorGUI.DisabledScope(owner.Preview.Map.HasChanges))
                {
                    planets.DrawPicker(() => { owner.DraftChanged(); owner.RebuildPage(); });
                    planets.DrawCatalog(owner.DraftChanged);
                    planets.DrawLanding(owner.DraftChanged);
                    planets.DrawJourney(owner.DraftChanged, owner.Preview.PlayJourney);
                }
            }));
            form.Field("Config.Navigation", "星图与导航（完整原配置）");
            form.Button("返回地图画布", () => owner.Preview.ToggleJourney(false));
            form.Button("保存地图与航程草稿", owner.Commands.SaveDraft);
        }

        private void BuildPaint()
        {
            var owner = form.Owner;
            form.Help("临时拆填只用于预览，不保存、不导出，也不是持久地图编辑。存在笔触时暂停来源与生产参数编辑。");
            form.Button("观察／平移", owner.Preview.UsePan);
            form.Button("临时拆格", () => owner.Preview.Canvas.ActiveTool = TerrainStylePreviewTool.Dig);
            var material = new DropdownField("临时填充材料", new List<string>
            { "壤土", "板岩", "玄武岩", "铜矿", "铁矿", "金矿", "苔岩" }, Mathf.Clamp(owner.State.FillMaterial - 1, 0, 6));
            material.RegisterValueChangedCallback(change => owner.State.FillMaterial = (byte)(material.index + 1));
            form.Root.Add(material);
            form.Button("临时填格", () => owner.Preview.Canvas.ActiveTool = TerrainStylePreviewTool.Fill);
            form.Button("撤销临时笔触", () => { owner.Preview.Map.Undo(); owner.Preview.RefreshMap(); });
            form.Button("重做临时笔触", () => { owner.Preview.Map.Redo(); owner.Preview.RefreshMap(); });
            form.Button("取消全部临时拆填", owner.Preview.CancelPaint, primary: true);
            form.Note("撤销笔触只改变预览；生产作者参数仍使用原生Undo。");
        }

        private void BuildStudy()
        {
            var owner = form.Owner; owner.State.ShowStudy = true;
            form.Help("本机局部制作实验，非当前地图的生产输入。隐藏旧的冻结／装配按钮，出口统一到检查与使用。");
            form.Root.Add(new IMGUIContainer(() => owner.Preview.Study.DrawControls(owner.Generation, false)));
            form.Button("保存本机实验预设", () => TerrainVeinStudySettings.instance.SavePreset());
            form.Button("返回生产地图", () => owner.SelectPage("source"));
        }

        public void Dispose() { planets?.Dispose(); modifiers?.Dispose(); }
    }
}
