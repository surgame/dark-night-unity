using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>固定地图初始格子的窗口草稿；只编辑可破坏内部格，应用时核对源文件并保留原有资产引用。</summary>
    public sealed class TerrainMapDraft
    {
        private TerrainMapAsset map;
        private string assetPath;
        private byte[] baseline, working;
        private int changed;
        private readonly Dictionary<int, TerrainBlueprintCellChange> pending = new Dictionary<int, TerrainBlueprintCellChange>();
        private readonly HashSet<int> modifiedIndexes = new HashSet<int>();
        private readonly List<HistoryEntry> history = new List<HistoryEntry>();
        private Dictionary<int, CellValue> strokeBefore;
        private int historyCursor;

        public bool HasChanges => changed != 0;
        public int ChangedCells => changed;
        public bool IsReady => working != null;
        public string Error { get; private set; }

        public void Open(TerrainMapAsset source)
        {
            map = source; assetPath = null; baseline = null; working = null; changed = 0; pending.Clear();
            modifiedIndexes.Clear(); history.Clear(); historyCursor = 0; strokeBefore = null; Error = null;
            if (map == null) return;
            try
            {
                map.ReadBlueprint();
                string mapPath = AssetDatabase.GetAssetPath(map);
                assetPath = AssetDatabase.GetAssetPath(map.InitialCells);
                if (string.IsNullOrEmpty(mapPath) || assetPath != Path.ChangeExtension(mapPath, ".cells.bytes"))
                    throw new InvalidOperationException("初始格子必须是地图同名的 .cells.bytes 文件。当前地图仅可预览。");
                baseline = File.ReadAllBytes(AbsolutePath());
                if (baseline.Length != TerrainGenerationSettings.Width * TerrainGenerationSettings.Height * 2 ||
                    !baseline.SequenceEqual(map.InitialCells.bytes))
                    throw new InvalidOperationException("初始格子文件与 Unity 导入内容不一致，请先重新导入。");
                working = (byte[])baseline.Clone();
            }
            catch (Exception error) { Error = error.Message; assetPath = null; baseline = null; working = null; }
        }

        public bool Paint(int x, int y, bool fill, byte material)
        {
            if (working == null || x <= 0 || y <= 0 || x >= TerrainGenerationSettings.Width - 1 ||
                y >= TerrainGenerationSettings.Height - 1 || (fill && (material < 1 || material > 7))) return false;
            int offset = (y * TerrainGenerationSettings.Width + x) * 2;
            if ((working[offset + 1] & 1) != 0 || working[offset] == 8) return false;
            byte value = fill ? material : (byte)0;
            if (working[offset] == value && working[offset + 1] == 0) return false;
            bool ownStroke = strokeBefore == null;
            if (ownStroke) BeginStroke();
            int index = y * TerrainGenerationSettings.Width + x;
            if (!strokeBefore.ContainsKey(index)) strokeBefore.Add(index, Read(working, index));
            Write(index, new CellValue(value, 0));
            if (ownStroke) EndStroke();
            return true;
        }

        /// <summary>把连续鼠标拖动合成一个撤销事务；权威草稿仍只持有当前输入快照。</summary>
        public void BeginStroke()
        { if (working != null && strokeBefore == null) strokeBefore = new Dictionary<int, CellValue>(); }

        public void EndStroke()
        {
            if (strokeBefore == null) return;
            var edits = new List<CellEdit>(strokeBefore.Count);
            foreach (var pair in strokeBefore)
            {
                CellValue after = Read(working, pair.Key);
                if (!pair.Value.Equals(after)) edits.Add(new CellEdit(pair.Key, pair.Value, after));
            }
            if (edits.Count != 0)
            {
                if (historyCursor < history.Count) history.RemoveRange(historyCursor, history.Count - historyCursor);
                history.Add(new HistoryEntry(edits.ToArray())); historyCursor = history.Count;
            }
            strokeBefore = null;
        }

        public bool CanUndo => historyCursor > 0 && working != null;
        public bool CanRedo => historyCursor < history.Count && working != null;

        /// <summary>撤回最近一次笔触，并通过同一稀疏变化出口更新运行时预览。</summary>
        public bool Undo()
        {
            EndStroke(); if (!CanUndo) return false;
            var entry = history[--historyCursor]; foreach (var edit in entry.Edits) Write(edit.Index, edit.Before);
            return entry.Edits.Length != 0;
        }

        /// <summary>重放下一次已撤销笔触，不改写地图资产直到显式 Apply。</summary>
        public bool Redo()
        {
            EndStroke(); if (!CanRedo) return false;
            var entry = history[historyCursor++]; foreach (var edit in entry.Edits) Write(edit.Index, edit.After);
            return entry.Edits.Length != 0;
        }

        /// <summary>丢弃所有未应用格草稿；仅遍历已修改格并把恢复值排入预览批次。</summary>
        public bool Cancel()
        {
            EndStroke(); if (!HasChanges) return false;
            var indexes = new List<int>(modifiedIndexes);
            foreach (int index in indexes) Write(index, Read(baseline, index));
            changed = 0; modifiedIndexes.Clear(); history.Clear(); historyCursor = 0;
            return true;
        }

        /// <summary>取出自上次预览提交以来触及格子的最终值，不复制整张地图来发现差异。</summary>
        public IReadOnlyList<TerrainBlueprintCellChange> DrainChangedCells()
        {
            var result = new List<TerrainBlueprintCellChange>(pending.Values);
            result.Sort((left, right) => left.Row != right.Row ? left.Row.CompareTo(right.Row) : left.X.CompareTo(right.X));
            pending.Clear(); return result;
        }

        public byte[] CopyMaterials() => Extract(false, working);
        public byte[] CopyShapes() => Extract(true, working);
        public byte[] CopyOriginalMaterials() => Extract(false, baseline);
        public byte[] CopyOriginalShapes() => Extract(true, baseline);

        private byte[] Extract(bool shapes, byte[] source)
        {
            if (source == null) throw new InvalidOperationException(Error ?? "地图草稿不可用。");
            var result = new byte[source.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = shapes ? (byte)(source[i * 2 + 1] >> 1) : source[i * 2];
            return result;
        }

        private void Write(int index, CellValue value)
        {
            int offset = index * 2; bool beforeChanged = working[offset] != baseline[offset] || working[offset + 1] != baseline[offset + 1];
            working[offset] = value.Material; working[offset + 1] = value.Shape;
            bool afterChanged = working[offset] != baseline[offset] || working[offset + 1] != baseline[offset + 1];
            if (beforeChanged != afterChanged) changed += afterChanged ? 1 : -1;
            if (afterChanged) modifiedIndexes.Add(index); else modifiedIndexes.Remove(index);
            pending[index] = new TerrainBlueprintCellChange(index % TerrainGenerationSettings.Width,
                index / TerrainGenerationSettings.Width, value.Material, value.Shape);
        }

        private static CellValue Read(byte[] source, int index) => new CellValue(source[index * 2], source[index * 2 + 1]);

        /// <summary>一个像素格的原生材料与坡形状态。</summary>
        private readonly struct CellValue
        {
            internal readonly byte Material, Shape;
            internal CellValue(byte material, byte shape) { Material = material; Shape = shape; }
        }

        /// <summary>一笔笔触的有序格快照，可用于撤销和重做。</summary>
        private readonly struct CellEdit
        {
            internal readonly int Index; internal readonly CellValue Before, After;
            internal CellEdit(int index, CellValue before, CellValue after) { Index = index; Before = before; After = after; }
        }

        /// <summary>一组鼠标笔触格差异；保留同一笔画的撤销事务边界。</summary>
        private sealed class HistoryEntry
        {
            internal readonly CellEdit[] Edits;
            internal HistoryEntry(CellEdit[] edits) { Edits = edits; }
        }

        public void Apply()
        {
            if (!HasChanges) return;
            if (!File.ReadAllBytes(AbsolutePath()).SequenceEqual(baseline))
                throw new InvalidOperationException("初始格子文件已在窗口外变化；请先取消地图草稿并重新打开。");
            // 同一格子文件只属于当前地图；不通过替换 TextAsset 破坏场景与地图资产的 GUID。
            string[] maps = AssetDatabase.FindAssets("t:TerrainMapAsset");
            foreach (string guid in maps)
            {
                var other = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (other != null && other != map && other.InitialCells == map.InitialCells)
                    throw new InvalidOperationException("其他地图也引用此格子文件；请先为当前地图复制独立格子资产。");
            }
            try
            {
                File.WriteAllBytes(AbsolutePath(), working);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }
            catch
            {
                File.WriteAllBytes(AbsolutePath(), baseline);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                throw;
            }
            Open(map);
        }

        /// <summary>导入同一固定地图的运行时最终格快照；先完整验证源基线、边界和保护位，再进入原有原子保存流程。</summary>
        public void ImportCells(byte[] original, byte[] cells)
        {
            if (working == null || original == null || cells == null || cells.Length != baseline.Length || !baseline.SequenceEqual(original))
                throw new InvalidOperationException("固定地图源文件已变化或快照不完整；保存被拒绝。");
            for (int i = 0; i < cells.Length / 2; i++)
            {
                int offset = i * 2, x = i % TerrainGenerationSettings.Width, y = i / TerrainGenerationSettings.Width;
                if (cells[offset] == baseline[offset] && cells[offset + 1] == baseline[offset + 1]) continue;
                if (x == 0 || y == 0 || x == TerrainGenerationSettings.Width - 1 || y == TerrainGenerationSettings.Height - 1 ||
                    (baseline[offset + 1] & 1) != 0 || baseline[offset] == 8 || cells[offset] > 7 ||
                    (cells[offset + 1] & 1) != 0 || cells[offset + 1] > 24 || (cells[offset] == 0 && cells[offset + 1] != 0))
                    throw new InvalidOperationException("运行草稿包含非法、边界或保护格修改。");
            }
            for (int i = 0; i < cells.Length / 2; i++)
                if (cells[i * 2] != baseline[i * 2] || cells[i * 2 + 1] != baseline[i * 2 + 1])
                    Write(i, new CellValue(cells[i * 2], cells[i * 2 + 1]));
        }

        private string AbsolutePath() => Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
    }
}
