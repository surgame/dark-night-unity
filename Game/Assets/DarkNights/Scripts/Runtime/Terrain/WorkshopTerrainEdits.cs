using System;
using System.Collections.Generic;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>离线工作台笔触历史；格子只在 YYGC 权威地图内写入，历史保存前后差异，外部破坏会使冲突撤销失败。</summary>
    public sealed class WorkshopTerrainEdits
    {
        private readonly TerrainMapAuthority map;
        private readonly TerrainBlueprint initial;
        private readonly List<WorkshopTerrainStroke> history = new List<WorkshopTerrainStroke>();
        private readonly Dictionary<CellCoord, GridCell> baseline = new Dictionary<CellCoord, GridCell>();
        private readonly Dictionary<CellCoord, GridCell> owned = new Dictionary<CellCoord, GridCell>();
        private WorkshopTerrainStroke stroke;
        private int cursor;
        private static readonly string[] Keys = { "", "loam", "slate", "basalt", "copper", "iron", "gold", "moss" };
        public bool CanUndo => cursor > 0 || stroke?.After.Count > 0;
        public bool CanRedo => cursor < history.Count;
        public int ChangedCells
        {
            get { int count = 0; foreach (var pair in baseline) if (!map.Read(pair.Key).Cell.Equals(pair.Value)) count++; return count; }
        }
        public WorkshopTerrainEdits(TerrainMapAuthority map, TerrainBlueprint initial) { this.map = map; this.initial = initial; }
        public void BeginStroke() { if (stroke == null) stroke = new WorkshopTerrainStroke(); }
        public bool Paint(int x, int row, bool fill, byte material)
            => PaintLine(x, row, x, row, fill, material);
        public bool PaintLine(int x, int row, int endX, int endRow, bool fill, byte material)
        {
            if (material < 1 || material > 7 || Math.Abs(endX - x) > 320 || Math.Abs(endRow - row) > 192) return false;
            var before = new Dictionary<CellCoord, GridCell>(); var after = new Dictionary<CellCoord, GridCell>();
            var value = fill ? new GridCell(map.Tiles.ByKey(Keys[material]), 0, 0) : default;
            int dx = Math.Abs(endX - x), dy = Math.Abs(endRow - row), sx = x < endX ? 1 : -1, sy = row < endRow ? 1 : -1;
            int error = dx - dy;
            while (true)
            {
                var p = new CellCoord(x, -row);
                if (x > 0 && row > 0 && x < initial.Width - 1 && row < initial.Height - 1 &&
                    !initial.IsProtected(x, row) && initial.MaterialAt(x, row) != 8 && map.Read(p).TryGetCell(out var old) && !old.Equals(value))
                { before[p] = old; after[p] = value; }
                if (x == endX && row == endRow) break;
                int twice = error * 2;
                if (twice > -dy) { error -= dy; x += sx; }
                if (twice < dx) { error += dx; row += sy; }
            }
            if (after.Count == 0) return false;
            BeginStroke();
            map.ApplyWorkshopCells(after);
            foreach (var pair in before)
            {
                if (!stroke.Before.ContainsKey(pair.Key)) stroke.Before.Add(pair.Key, pair.Value);
                if (!baseline.ContainsKey(pair.Key)) baseline.Add(pair.Key, pair.Value);
                stroke.After[pair.Key] = after[pair.Key]; owned[pair.Key] = after[pair.Key];
            }
            return true;
        }
        public void EndStroke()
        {
            if (stroke == null) return;
            if (stroke.After.Count > 0)
            {
                if (cursor < history.Count) history.RemoveRange(cursor, history.Count - cursor);
                history.Add(stroke); cursor = history.Count;
                if (history.Count > 128) { history.RemoveAt(0); cursor--; }
            }
            stroke = null;
        }
        public void Undo()
        {
            EndStroke(); if (!CanUndo) return;
            var entry = history[cursor - 1]; Apply(entry.After, entry.Before); cursor--;
        }
        public void Redo()
        {
            EndStroke(); if (!CanRedo) return;
            var entry = history[cursor]; Apply(entry.Before, entry.After); cursor++;
        }
        private void Apply(Dictionary<CellCoord, GridCell> expected, Dictionary<CellCoord, GridCell> values)
        {
            foreach (var pair in expected)
                if (!map.Read(pair.Key).Cell.Equals(pair.Value))
                    throw new InvalidOperationException("笔触涉及的格子已被其他操作改变；请重置地图后继续。");
            map.ApplyWorkshopCells(values);
            foreach (var pair in values) owned[pair.Key] = pair.Value;
        }
        public void Cancel()
        {
            EndStroke();
            // 仅撤回本草稿最后仍拥有的格子，外部手采修改不被静默覆盖。
            foreach (var pair in owned)
                if (!map.Read(pair.Key).Cell.Equals(pair.Value)) throw new InvalidOperationException("地图存在外部编辑，取消被拒绝。");
            if (baseline.Count > 0) map.ApplyWorkshopCells(baseline);
            history.Clear(); baseline.Clear(); owned.Clear(); cursor = 0;
        }
        public byte[] CaptureCells()
        {
            var bytes = new byte[initial.Width * initial.Height * 2];
            for (int row = 0; row < initial.Height; row++) for (int x = 0; x < initial.Width; x++)
            {
                var cell = map.Read(new CellCoord(x, -row)).Cell; int index = (row * initial.Width + x) * 2;
                if (!cell.IsEmpty)
                {
                    for (int m = 1; m < Keys.Length; m++) if (cell.TileId == map.Tiles.ByKey(Keys[m])) bytes[index] = (byte)m;
                    if (cell.TileId == map.Tiles.ByKey("bedrock")) bytes[index] = 8;
                }
                bytes[index + 1] = initial.IsProtected(x, row) ? (byte)1 : (byte)cell.Flags;
            }
            return bytes;
        }
    }
}
