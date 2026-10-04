using System;
using System.IO;
using System.Linq;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Compiler;
using AnyRules.Next.Editor;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using GameCore.Objects.Definition;
using UnityEditor;

namespace DarkNights.Editor.Terrain
{
    /// <summary>显式升级矿层业务目录到地图耐久合同；保留作者纹理、规则与 GUID，仅重建可追溯的编译目录并绑定会话配置。</summary>
    public static class MineralMapContentSetup
    {
        [MenuItem("Dark Nights/Content/升级矿层地图配置")]
        public static void Install()
        {
            var definition = MineralLayerAssets.Ensure();
            var source = AssetDatabase.LoadAssetAtPath<EditorAnyRuleDDatabase>(MineralLayerAssets.Root + "/Catalog.asset");
            var mineral = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/MineralDeposit/MineralDeposit.asset");
            var rules = mineral.SharedConfigs.OfType<MineralDepositRuleConfig>().Single();
            rules.Validate();
            foreach (var terrain in source.Terrains) { terrain.MaximumDurability = rules.HarvestDurability; EditorUtility.SetDirty(terrain); }
            var exported = source.Export(); string digest = RuleCompiler.ComputeSourceDigest(exported);
            var compiled = RuleCompiler.Compile(exported, digest);
            byte[] gameplay = RuleCatalogCodec.WriteGameplay(compiled.Gameplay), visual = RuleCatalogCodec.WriteVisual(compiled.Runtime);
            RuleCatalogCodec.ReadVisual(visual, RuleCatalogCodec.ReadGameplay(gameplay));
            string gameplayPath = AssetDatabase.GetAssetPath(definition.GameplayCatalog), visualPath = AssetDatabase.GetAssetPath(definition.VisualCatalog);
            File.WriteAllBytes(gameplayPath, gameplay); File.WriteAllBytes(visualPath, visual);
            AssetDatabase.ImportAsset(gameplayPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(visualPath, ImportAssetOptions.ForceSynchronousImport);
            definition.AuthoringSourceDigest = digest;
            definition.AuthoringBusinessDigest = new GridBusinessCatalog(compiled.Gameplay, definition.ExportBusinessDefinitions()).ContentDigest;
            var session = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(FormalObjectContentSetup.SessionDefinitionPath);
            session.SharedConfigs.OfType<TerrainProfileConfig>().Single().MineralDefinition = definition;
            EditorUtility.SetDirty(definition); EditorUtility.SetDirty(session); AssetDatabase.SaveAssets();
            _ = new FrozenMineralRules(definition, rules, mineral.Guid.ToString());
        }
    }
}
