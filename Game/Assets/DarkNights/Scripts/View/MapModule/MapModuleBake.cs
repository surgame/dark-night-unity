using System;
using System.Security.Cryptography;
using System.Text;
using DarkNights.Core.Config.Terrain;
using UnityEngine;

namespace DarkNights.View.MapModule
{
    /// <summary>
    /// 工作台显式生成的固定制作版本；保存初始格形及来源身份，不保存EntityId、epoch或玩家进度。
    /// 对外只提供复制后的冻结地图，每次运行注入新世界身份；内容摘要失配或缺失时拒绝加载。
    /// </summary>
    public sealed class MapModuleBake : ScriptableObject
    {
        public const int CurrentFormat = 1;
        [SerializeField] private int format;
        [SerializeField] private string studyId, caseId, sourceHash, contentHash;
        [SerializeField] private float spawnX, spawnHeight;
        [SerializeField] private byte[] materials = Array.Empty<byte>();
        [SerializeField] private byte[] shapes = Array.Empty<byte>();
        public string StudyId => studyId;
        public string CaseId => caseId;
        public string SourceHash => sourceHash;
        public string ContentHash => contentHash;
        public float SpawnX => spawnX;
        public float SpawnHeight => spawnHeight;
        public void Initialize(string study, string id, string source, float spawn, float height, PlayableTerrain data)
        {
            if (format != 0) throw new InvalidOperationException("固定制作版本不能覆盖，请新建版本。");
            format = CurrentFormat; studyId = study; caseId = id; sourceHash = source; spawnX = spawn; spawnHeight = height;
            materials = data.CopyMaterials(); shapes = data.CopyShapes(); contentHash = Hash();
            Freeze(Guid.Empty.ToString("N"));
        }
        public PlayableTerrain Freeze(string worldId)
        {
            if (format != CurrentFormat || string.IsNullOrWhiteSpace(studyId) || string.IsNullOrWhiteSpace(caseId) ||
                sourceHash == null || sourceHash.Length != 64 || contentHash != Hash() ||
                float.IsNaN(spawnX) || float.IsInfinity(spawnX) || spawnX < 0 || spawnX > 5120 ||
                float.IsNaN(spawnHeight) || float.IsInfinity(spawnHeight) ||
                materials.Length != TerrainGenerationSettings.Width * TerrainGenerationSettings.Height || shapes.Length != materials.Length)
                throw new InvalidOperationException("MapModule制作版本缺失、损坏或格式无效。");
            var protection = new bool[materials.Length];
            for (int x = 0; x < TerrainGenerationSettings.Width; x++) protection[materials.Length - TerrainGenerationSettings.Width + x] = true;
            string seed = "mm:" + contentHash;
            return new PlayableTerrain(worldId, seed, materials, protection, new bool[materials.Length],
                Array.Empty<TerrainRoom>(), Array.Empty<TerrainDepositBlueprint>(), shapes, true,
                new BackgroundBakeDescriptor(worldId, seed, materials, shapes));
        }
        private string Hash()
        {
            using var algorithm = SHA256.Create();
            byte[] header = Encoding.UTF8.GetBytes("mapmodule-bake-v1|" + studyId + "|" + caseId + "|" + sourceHash + "|" +
                spawnX.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                spawnHeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|");
            var bytes = new byte[header.Length + materials.Length + shapes.Length];
            Buffer.BlockCopy(header, 0, bytes, 0, header.Length);
            Buffer.BlockCopy(materials, 0, bytes, header.Length, materials.Length);
            Buffer.BlockCopy(shapes, 0, bytes, header.Length + materials.Length, shapes.Length);
            return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
