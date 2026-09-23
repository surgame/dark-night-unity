using System;
using System.IO;
using System.Linq;
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

        public bool HasChanges => changed != 0;
        public int ChangedCells => changed;
        public bool IsReady => working != null;
        public string Error { get; private set; }

        public void Open(TerrainMapAsset source)
        {
            map = source; assetPath = null; baseline = null; working = null; changed = 0; Error = null;
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
            bool wasChanged = working[offset] != baseline[offset] || working[offset + 1] != baseline[offset + 1];
            working[offset] = value; working[offset + 1] = 0;
            bool isChanged = working[offset] != baseline[offset] || working[offset + 1] != baseline[offset + 1];
            if (wasChanged != isChanged) changed += isChanged ? 1 : -1;
            return true;
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

        private string AbsolutePath() => Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
    }
}
