using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>从已保存的正式地形配置生成新完整地图资产，并仅替换开局地图引用；不覆盖旧图、场景或未保存草稿，不在运行中重抽矿巢。</summary>
    public static class JointMineContentSetup
    {
        internal const string VersionRoot = "Assets/DarkNights/Res/Terrain/StrataCave/JointMaps";
        private static ObjectDefinition Source => AssetDatabase.LoadAssetAtPath<ObjectDefinition>(
            "Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset");
        public static PlayableTerrain Build(ExpeditionFlowConfig config, int count, bool nests, Action<string> report = null)
        {
            var input = ExpeditionFlowDraft.Clone(config);
            TerrainAuthoringImport.Normalize(input, count, nests);
            var settings = input.FreezeCaveMap();
            return PlanetTerrainGenerator.GenerateCandidate(input.PreviewPlanet(), settings.Seed,
                TerrainGenerationPreview.WorldId, settings, contentReport: report);
        }

        public static ReviewedMineMap CreateAndInstall(PlayableTerrain candidate, string sourceIdentity, string recipe)
        {
            return Create(candidate, sourceIdentity, recipe, true);
        }

        /// <summary>冻结同一已预览候选；可只创建新版本，装配新局必须由调用方显式选择。</summary>
        public static ReviewedMineMap Create(PlayableTerrain candidate, string sourceIdentity, string recipe, bool install)
        {
            return Create(candidate, sourceIdentity, recipe, install, Source, VersionRoot);
        }

        internal static ReviewedMineMap Create(PlayableTerrain candidate, string sourceIdentity, string recipe, bool install,
            ObjectDefinition source, string versionRoot)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("先退出Play再生成完整地图。");
            if (source == null || EditorUtility.IsDirty(source)) throw new InvalidOperationException("正式会话配置缺失或有未保存修改，请先处理该草稿。");
            var config = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            if (candidate == null || ReviewedMineMap.SourceFingerprint(config) != sourceIdentity)
                throw new InvalidOperationException("候选或已保存作者输入已变化，请重新预览完整联合候选。");
            if (!AssetDatabase.IsValidFolder(versionRoot)) throw new IOException("冻结版本根目录尚未导入。");
            string folder = versionRoot + "/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            if (Directory.Exists(folder)) throw new IOException("完整地图目录已存在。");
            Directory.CreateDirectory(folder);
            string path = folder + "/layout.json";
            string json = ReviewedMineMap.Encode(candidate, recipe);
            File.WriteAllText(path, json, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var asset = ScriptableObject.CreateInstance<ReviewedMineMap>();
            asset.Content = AssetDatabase.LoadAssetAtPath<TextAsset>(path); asset.Recipe = recipe;
            asset.SourceIdentity = sourceIdentity;
            asset.SourcePlanetId = config.PreviewPlanet().Id;
            using (var hash = SHA256.Create()) asset.ContentSha256 = BitConverter.ToString(hash.ComputeHash(asset.Content.bytes)).Replace("-", "").ToLowerInvariant();
            asset.Freeze(TerrainGenerationPreview.WorldId);
            AssetDatabase.CreateAsset(asset, folder + "/JointMine.asset");
            AssetDatabase.SaveAssetIfDirty(asset);
            if (install) Install(asset, source);
            Selection.activeObject = asset;
            return asset;
        }

        /// <summary>只装配与当前已保存来源匹配的冻结版；保留旧资产及存档，拒绝覆盖未保存的正式配置。</summary>
        public static void Install(ReviewedMineMap asset)
        {
            Install(asset, Source);
        }

        internal static void Install(ReviewedMineMap asset, ObjectDefinition source)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || asset == null)
                throw new InvalidOperationException("仅编辑态可装配有效冻结地图。");
            if (source == null || EditorUtility.IsDirty(source))
                throw new InvalidOperationException("正式会话配置缺失或尚有未保存修改。");
            var config = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            asset.VerifySource(config);
            asset.Freeze(TerrainGenerationPreview.WorldId);
            Undo.RecordObject(source, "为新局装配冻结地图");
            config.ReviewedMap = asset;
            Undo.FlushUndoRecordObjects();
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssetIfDirty(source);
        }
    }
}
