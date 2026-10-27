using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 常驻画布的八步结果查看器；只读取同次不可变生成结果，阶段选择与作者参数完全分离。
    /// 新候选直接带快照，历史结果缺少快照时需明确重新生成；纹理随候选替换或窗口释放。
    /// </summary>
    internal sealed class MapWorkbenchFlowPage : IDisposable
    {
        private readonly MapWorkbenchWindow owner;
        private readonly VisualElement root;
        private readonly Button[] buttons = new Button[8];
        private readonly Image image;
        private readonly Label detail;
        private TerrainGenerationResult observed;
        private Texture2D texture;
        private int drawn = -1;
        private int selected => owner.State.ProductionStage;
        private bool changes => owner.State.HighlightStageChanges;

        internal MapWorkbenchFlowPage(MapWorkbenchWindow owner, VisualElement root)
        {
            this.owner = owner; this.root = root;
            image = root.Q<Image>("productionImage"); image.scaleMode = ScaleMode.ScaleToFit;
            detail = root.Q<Label>("productionDetail");
            string[] names = { "1 地表", "2 计划", "3 洞室", "4 道路", "5 入口", "6 矿巢", "7 格形", "8 完整地图" };
            for (int i = 0; i < 8; i++)
            {
                int stage = i;
                buttons[i] = new Button(() => Select(stage)) { name = "productionStage" + i, text = names[i] };
                buttons[i].AddToClassList("map-flow-step"); root.Q("productionStages").Add(buttons[i]);
            }
            var highlight = root.Q<Toggle>("highlightStageChanges"); highlight.SetValueWithoutNotify(changes);
            highlight.RegisterValueChangedCallback(e =>
            { owner.State.HighlightStageChanges = e.newValue; if (selected < 7 && observed != null) Draw(); });
            root.Q<Button>("captureStages").clicked += () => owner.Execute(() =>
            {
                owner.Generation.CaptureStages = true; owner.Commands.Generate();
                owner.Notice = "按本次种子记录真实步骤；不会抽种子，不自动保存地图。";
            });
            Refresh();
        }

        internal void Select(int stage)
        {
            owner.State.ProductionStage = Mathf.Clamp(stage, 0, 7);
            owner.State.Pick = 0; owner.Preview.UsePan(); owner.Preview.Compare = false;
            UpdateImage(); owner.Refresh();
        }

        internal void Refresh()
        {
            var result = owner.State.ViewedMap == null ? owner.Generation.Result : null;
            if (!ReferenceEquals(observed, result) || drawn != selected)
            {
                observed = result;
                if (selected < 7 && (result == null || !result.Steps[selected].HasSnapshot)) owner.State.ProductionStage = 7;
                UpdateImage();
            }
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].EnableInClassList("map-selected", selected == i);
                buttons[i].SetEnabled(i == 7 || result != null && result.Steps[i].HasSnapshot);
                buttons[i].tooltip = result == null ? "历史地图未保存制作步骤；实际保存结果保持。" : result.Steps[i].Name;
            }
            root.Q<Button>("captureStages").EnableInClassList("map-hidden", result?.Steps[0].HasSnapshot == true);
            root.Q<Button>("captureStages").SetEnabled(owner.State.ViewedMap == null && !owner.Preview.InputPending && !owner.Preview.Map.HasChanges);
            root.Q<Label>("moduleImpact").text = Impact();
            bool intermediate = selected < 7 && result?.Steps[selected].HasSnapshot == true;
            root.Q("canvasTools").SetEnabled(!intermediate);
            root.Q("mapModeSwitch").SetEnabled(!intermediate);
            root.Q<Button>("compare").SetEnabled(!intermediate && owner.Preview.HasPrevious);
            root.Q<Toggle>("highlightStageChanges").SetEnabled(intermediate);
            root.Q<Label>("canvasInteraction").text = intermediate ? "阶段白模 · 只读查看 · 选择第8步返回编辑" : "拖拽平移 · 滚轮缩放";
            if (result == null) detail.text = "历史地图未保存制作步骤及计划；实际保存结果保持，不补算。";
            else if (!intermediate) detail.text = "第8步 · 完整地图 · 材料变格 " + result.Steps[7].ChangedCells +
                " · 坡形变格 " + result.Steps[7].ChangedShapes + " · 本次种子 " + result.Terrain.Seed +
                (result.Steps[0].HasSnapshot ? "" : "；只有步骤摘要，未保存中间格子。");
        }

        private string Impact()
        {
            string page = owner.State.Page == "extras" ? owner.State.Extra : owner.State.Page;
            switch (page)
            {
                case "recipe": return "算法／实际种子：影响完整生成；只改选择策略不重抽当前地图。";
                case "surface": return "地表：从①更新自然地表及受围岩影响的内容；②地下计划保持。";
                case "rooms": return "洞室：②计划及后续步骤更新；①地表保持。";
                case "roads": return "道路类型：②起更新；只调宽度从④起更新，②③洞室计划保持。";
                case "entrance": return "入口：⑤起更新；②地下计划保持。围岩变化可能影响⑥内容。";
                case "minerals": return "矿物：⑥内容与后续格形更新；局部重排保持其他区域、洞形与侧巢。";
                case "nest": return "侧巢：⑥可能挖空巢口；①～⑤空间保持，随后⑦统一修形。";
                case "collapse": return "塌方：⑧验证静态站点；冲突保留草稿，不自动改地形。";
                case "environment": case "appearance": case "scenery": return "表现：只刷新美术或锚点；①～⑧地形、矿区与种子保持。";
                case "release": return "保存：记录配方与完整结果；重开和用于新局沿用保存结果，不重新抽图。";
                default: return "实验独立于生产地图；临时笔触不保存，存在笔触时暂停生产操作。";
            }
        }

        private void UpdateImage()
        {
            drawn = selected;
            bool show = selected < 7 && observed?.Steps[selected].HasSnapshot == true;
            image.EnableInClassList("map-hidden", !show);
            if (show) Draw();
            else { image.image = null; if (texture != null) UnityEngine.Object.DestroyImmediate(texture); texture = null; }
        }

        private void Draw()
        {
            var result = owner.Generation.Result;
            var step = result.Steps[selected];
            detail.text = "本步材料变化 " + step.ChangedCells + " 格 · 坡形变化 " + step.ChangedShapes +
                " 格 · 矿格 " + step.MineralCells + " · 侧巢 " + step.Nests +
                (step.Right < 0 ? "" : "\n改形范围 U " + step.Left + "～" + step.Right + " / 行 " + step.Top + "～" + step.Bottom);
            if (!step.HasSnapshot)
            {
                detail.text += "\n只有步骤摘要；点击记录按钮可明确按当前规则重生成并查看中间白模。";
                return;
            }
            var cells = step.CopyMaterials();
            var before = selected == 0 ? null : result.Steps[selected - 1].CopyMaterials();
            var geometry = step.CopyShapes();
            const int scale = 4, width = 320 * scale, height = 192 * scale;
            var pixels = new Color32[width * height];
            var sky = new Color32(52, 65, 70, 255);
            for (int y = 0; y < 192; y++) for (int x = 0; x < 320; x++)
            {
                int i = y * 320 + x;
                Color32 color = cells[i] == 0 ? new Color32(52, 65, 70, 255) :
                    cells[i] == 8 ? new Color32(172, 156, 184, 255) : new Color32(222, 225, 215, 255);
                if (changes && cells[i] != (before == null ? 0 : before[i])) color = new Color32(77, 180, 202, 255);
                bool removed = changes && cells[i] == 0 && before != null && before[i] != 0;
                for (int dy = 0; dy < scale; dy++) for (int dx = 0; dx < scale; dx++)
                {
                    bool inside = cells[i] != 0 && (geometry == null || TerrainShapeGeometry.Contains(
                        (TerrainCellShape)geometry[i], (dx + .5f) / scale, 1 - (dy + .5f) / scale));
                    pixels[(height - 1 - (y * scale + dy)) * width + x * scale + dx] = inside || removed ? color : sky;
                }
            }
            void Cell(int x, int y, Color32 color)
            {
                if (x < 0 || x >= 320 || y < 0 || y >= 192) return;
                for (int dy = 0; dy < scale; dy++) for (int dx = 0; dx < scale; dx++)
                    pixels[(height - 1 - (y * scale + dy)) * width + x * scale + dx] = color;
            }
            void Line(int ax, int ay, int bx, int by)
            {
                int count = Math.Max(1, Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay)));
                for (int n = 0; n <= count; n++)
                    Cell((int)Math.Round(ax + (bx - ax) * n / (double)count),
                        (int)Math.Round(ay + (by - ay) * n / (double)count), new Color32(118, 204, 165, 255));
            }
            if (selected == 1 && result.Plan != null)
            {
                foreach (var edge in result.Plan.Connections)
                {
                    var a = result.Plan.Rooms[edge.From]; var b = result.Plan.Rooms[edge.To];
                    Line(a.X, a.Y, edge.BendX, edge.BendY); Line(edge.BendX, edge.BendY, b.X, b.Y);
                }
                foreach (var room in result.Plan.Rooms)
                {
                    Line(room.Left, room.Top, room.Left + room.Width, room.Top);
                    Line(room.Left, room.Top, room.Left, room.Top + room.Height);
                    Line(room.Left + room.Width, room.Top, room.Left + room.Width, room.Top + room.Height);
                    Line(room.Left, room.Top + room.Height, room.Left + room.Width, room.Top + room.Height);
                }
            }
            if (selected >= 5)
                foreach (var deposit in result.Terrain.Deposits) foreach (var mineral in deposit.Cells)
                    Cell(mineral.U, -mineral.V, deposit.Rarity == "rare" ? new Color32(239, 190, 77, 255) : new Color32(104, 183, 208, 255));
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels32(pixels); texture.Apply();
            image.image = texture;
        }

        public void Dispose()
        {
            if (image != null) image.image = null;
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
