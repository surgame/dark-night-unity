using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 退出日常制图主流程的兼容与实验页；保留星球航程及本机试参，附加塑形入口已撤下，不复制另一份作者模型。
    /// 仅原有复杂兼容绘制器及实验回放保留IMGUI，生产来源、矿物、额外设计和冻结出口均为原生UITK。
    /// </summary>
    internal sealed class MapWorkbenchAdvancedPage : IDisposable
    {
        private readonly MapWorkbenchFields form;
        private TerrainPlanetControls planets;
        internal MapWorkbenchAdvancedPage(MapWorkbenchFields form)
        {
            this.form = form; var owner = form.Owner;
            string[] ids = { "planet", "paint", "study" };
            if (Array.IndexOf(ids, owner.State.AdvancedPage) < 0) owner.State.AdvancedPage = ids[0];
            form.Title(owner.State.AdvancedPage == "paint" ? "临时拆填实验" :
                owner.State.AdvancedPage == "study" ? "矿巢实验" : "保留星球参数",
                "实验只修改本机预览；星球与船位参数暂存保留，当前新局不装配飞船或推进航程。");
            if (owner.State.AdvancedPage == "planet") BuildPlanets();
            else if (owner.State.AdvancedPage == "paint") BuildPaint();
            else if (owner.State.AdvancedPage == "study") BuildStudy();
        }

        private void BuildPlanets()
        {
            var owner = form.Owner;
            planets = new TerrainPlanetControls(owner.Generation);
            form.Basic("星球与航程");
            form.Note("当前新局不使用飞船和泊位。以下既有配置保留供旧档及后续设计，不改变地形。");
            form.Root.Add(new IMGUIContainer(() =>
            {
                using (new EditorGUI.DisabledScope(owner.Preview.Map.HasChanges))
                {
                    planets.DrawPicker(() => { owner.DraftChanged(); owner.RebuildPage(); });
                }
            }));
            form.Advanced("星球、泊位与航程");
            form.Root.Add(new IMGUIContainer(() =>
            {
                using (new EditorGUI.DisabledScope(owner.Preview.Map.HasChanges))
                {
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
            form.Basic("临时拆填实验");
            form.Help("临时拆填只用于预览，不保存、不导出，也不是持久地图编辑。存在笔触时暂停来源与生产参数编辑。");
            form.Button("观察／平移", owner.Preview.UsePan);
            form.Button("临时拆格", () => owner.Preview.Canvas.ActiveTool = TerrainStylePreviewTool.Dig);
            var material = new DropdownField("临时填充材料", new List<string>
            { "壤土", "板岩", "玄武岩", "铜矿", "铁矿", "金矿", "苔岩" }, Mathf.Clamp(owner.State.FillMaterial - 1, 0, 6));
            material.RegisterValueChangedCallback(change => owner.State.FillMaterial = (byte)(material.index + 1));
            form.Root.Add(material);
            form.Button("临时填格", () => owner.Preview.Canvas.ActiveTool = TerrainStylePreviewTool.Fill);
            form.Advanced("临时笔触历史");
            form.Button("撤销临时笔触", () => { owner.Preview.Map.Undo(); owner.Preview.RefreshMap(); });
            form.Button("重做临时笔触", () => { owner.Preview.Map.Redo(); owner.Preview.RefreshMap(); });
            form.Button("取消全部临时拆填", owner.Preview.CancelPaint, primary: true);
            form.Note("撤销笔触只改变预览；生产作者参数仍使用原生Undo。");
        }

        private void BuildStudy()
        {
            var owner = form.Owner; owner.State.ShowStudy = true;
            form.Basic("本机矿巢实验");
            form.Help("本机局部制作实验，不作为当前地图的保存内容。保存当前地图请使用顶部文件入口。");
            form.Button("返回地图", () => owner.SelectPage("space"));
            form.Advanced("矿形、侧巢与回放");
            form.Root.Add(new IMGUIContainer(() => owner.Preview.Study.DrawControls(owner.Generation, false)));
            form.Button("保存本机实验预设", () => TerrainVeinStudySettings.instance.SavePreset());
            form.Button("返回地图", () => owner.SelectPage("space"));
        }

        public void Dispose() { planets?.Dispose(); }
    }
}
