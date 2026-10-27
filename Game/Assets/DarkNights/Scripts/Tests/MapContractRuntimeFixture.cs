using System;
using System.IO;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Editor.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using GameCore.Objects.Definition;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>仅本批测试启用的启动前配置夹具；在Bootstrap冻结握手前替换内存副本，退出恢复原引用，仅读历史保全文件。</summary>
    internal static class MapContractRuntimeFixture
    {
        internal const string ModeKey = "DarkNights.MapContractAcceptance.Mode";
        private static ObjectDefinition source;
        private static ExpeditionFlowConfig original;
        private static TerrainProfileConfig originalProfile;
        private static ReviewedMineMap historical;
        private static int index, profileIndex;
        internal static string Output => Path.GetFullPath(SessionState.GetString("DarkNights.MapFlow.Output",
            "../artifacts/map-contract-acceptance-20261027"));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrepareBeforeBootstrap()
        {
            string mode = SessionState.GetString(ModeKey, "");
            if (mode.Length == 0) return;
            source = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset");
            index = source.SharedConfigs.FindIndex(x => x is ExpeditionFlowConfig);
            original = (ExpeditionFlowConfig)source.SharedConfigs[index];
            ExpeditionFlowConfig flow;
            if (mode == "pool")
            {
                flow = JsonUtility.FromJson<ExpeditionFlowConfig>(JsonUtility.ToJson(original));
                flow.CaveMap.SeedSelection = new TerrainSeedSettings
                { Mode = TerrainSeedSettings.Pool, Seeds = new[] { flow.CaveMap.Seed, "MAP-CONTRACT-POOL-B" } };
            }
            else if (mode == "saved")
            {
                var document = AssetDatabase.LoadAssetAtPath<TerrainMapDocument>(
                    SessionState.GetString("DarkNights.MapFlow.Document", "Assets/__MapContractAcceptance20261027/Map.asset"));
                string isolatedSource = SessionState.GetString("DarkNights.MapFlow.Source", "");
                var installed = string.IsNullOrEmpty(isolatedSource) ? AssetDatabase.FindAssets("t:ObjectDefinition", new[] { "Assets/__MapContractAcceptance20261027" })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<ObjectDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Single(definition => definition.SharedConfigs.OfType<ExpeditionFlowConfig>().Single().ReviewedMap == document.Map) :
                    AssetDatabase.LoadAssetAtPath<ObjectDefinition>(isolatedSource);
                flow = JsonUtility.FromJson<ExpeditionFlowConfig>(
                    JsonUtility.ToJson(installed.SharedConfigs.OfType<ExpeditionFlowConfig>().Single()));
                profileIndex = source.SharedConfigs.FindIndex(x => x is TerrainProfileConfig);
                originalProfile = (TerrainProfileConfig)source.SharedConfigs[profileIndex];
                source.SharedConfigs[profileIndex] = JsonUtility.FromJson<TerrainProfileConfig>(
                    JsonUtility.ToJson(installed.SharedConfigs.OfType<TerrainProfileConfig>().Single()));
                flow.ReviewedMap.VerifySource(flow);
            }
            else
            {
                flow = JsonUtility.FromJson<ExpeditionFlowConfig>(File.ReadAllText(Path.Combine(Output, "historical-flow.json")));
                var metadata = JObject.Parse(File.ReadAllText(Path.Combine(Output, "historical-map.json")));
                historical = ScriptableObject.CreateInstance<ReviewedMineMap>();
                historical.Content = new TextAsset(File.ReadAllText(Path.GetFullPath("../artifacts/待清理/20261026-闲置地图结果/" +
                    "Game/Assets/DarkNights/Res/Terrain/StrataCave/JointMaps/20261022-171627-699/layout.json")));
                historical.Recipe = (string)metadata["Recipe"];
                historical.ContentSha256 = (string)metadata["ContentSha256"];
                historical.SourceIdentity = (string)metadata["SourceIdentity"];
                flow.ReviewedMap = historical;
                flow.ReviewedMap.VerifySource(flow);
            }
            source.SharedConfigs[index] = flow;
        }

        internal static void Restore()
        {
            SessionState.EraseString(ModeKey);
            if (source != null && original != null) source.SharedConfigs[index] = original;
            if (source != null && originalProfile != null) source.SharedConfigs[profileIndex] = originalProfile;
            if (historical != null)
            {
                UnityEngine.Object.DestroyImmediate(historical.Content);
                UnityEngine.Object.DestroyImmediate(historical);
            }
            source = null; original = null; originalProfile = null; historical = null;
        }
    }
}
