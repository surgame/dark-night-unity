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
    /// <summary>从已保存的正式地形配置生成新联合地图资产，并仅替换开局地图引用；不覆盖旧图、场景或未保存草稿，不在运行中重抽矿巢。</summary>
    public static class JointMineContentSetup
    {
        public static PlayableTerrain Build(ExpeditionFlowConfig config, int count, bool nests)
        {
            var settings = config.FreezeCaveMap();
            var baseline = PlanetTerrainGenerator.GenerateCandidate(config.PreviewPlanet(), settings.Seed,
                TerrainGenerationPreview.WorldId, settings, pipeline: config.FreezeModifiers());
            return JointMineCandidate.Generate(baseline, count, nests);
        }

        public static ReviewedMineMap CreateAndInstall(PlayableTerrain candidate, string sourceIdentity, string recipe)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("先退出Play再生成联合地图。");
            const string sourcePath = "Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset";
            var source = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(sourcePath);
            if (source == null || EditorUtility.IsDirty(source)) throw new InvalidOperationException("正式会话配置缺失或有未保存修改，请先处理该草稿。");
            var config = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            if (candidate == null || ReviewedMineMap.SourceFingerprint(config) != sourceIdentity)
                throw new InvalidOperationException("候选或已保存作者输入已变化，请重新预览完整联合候选。");
            string folder = "Assets/DarkNights/Res/Terrain/StrataCave/JointMaps/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            if (Directory.Exists(folder)) throw new IOException("联合地图目录已存在。");
            Directory.CreateDirectory(folder);
            string path = folder + "/layout.json";
            string json = ReviewedMineMap.Encode(candidate, recipe);
            File.WriteAllText(path, json, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var asset = ScriptableObject.CreateInstance<ReviewedMineMap>();
            asset.Content = AssetDatabase.LoadAssetAtPath<TextAsset>(path); asset.Recipe = recipe;
            asset.SourceIdentity = sourceIdentity;
            using (var hash = SHA256.Create()) asset.ContentSha256 = BitConverter.ToString(hash.ComputeHash(asset.Content.bytes)).Replace("-", "").ToLowerInvariant();
            asset.Freeze(TerrainGenerationPreview.WorldId);
            AssetDatabase.CreateAsset(asset, folder + "/JointMine.asset");
            Undo.RecordObject(source, "为新局装配联合矿巢地图"); config.ReviewedMap = asset;
            Undo.FlushUndoRecordObjects();
            EditorUtility.SetDirty(source); AssetDatabase.SaveAssetIfDirty(source); AssetDatabase.SaveAssetIfDirty(asset);
            Selection.activeObject = asset;
            return asset;
        }
    }
}
