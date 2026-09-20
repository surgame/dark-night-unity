using System;
using System.IO;
using System.Linq;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.Compiler;
using AnyRules.Next.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>显式刷新受 shader 修改影响的派生二进制及版本摘要；保持作者资源和既有 GUID。</summary>
public static class ExpeditionTerrainRecompile
{
    public static string Run()
    {
        const string path = "Assets/DarkNights/Res/Terrain/CaveExploration/Style/Configuration/DualGridMap.asset";
        var map = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(path);
        var source = CatalogBuildValidation.FindSource(map);
        var input = source.Export();
        string digest = RuleCompiler.ComputeSourceDigest(input);
        var compiled = RuleCompiler.Compile(input, digest);
        byte[] gameplay = RuleCatalogCodec.WriteGameplay(compiled.Gameplay), visual = RuleCatalogCodec.WriteVisual(compiled.Runtime);
        RuleCatalogCodec.ReadVisual(visual, RuleCatalogCodec.ReadGameplay(gameplay));
        string gamePath = AssetDatabase.GetAssetPath(map.GameplayCatalog), visualPath = AssetDatabase.GetAssetPath(map.VisualCatalog);
        File.WriteAllBytes(gamePath, gameplay); File.WriteAllBytes(visualPath, visual);
        AssetDatabase.ImportAsset(gamePath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(visualPath, ImportAssetOptions.ForceSynchronousImport);
        map.VisualAssets = source.ExportBindings(); map.BusinessDefinitions = source.CollectGameplayDefinitions();
        map.AuthoringSourceDigest = digest;
        map.AuthoringBusinessDigest = new GridBusinessCatalog(compiled.Gameplay, map.BusinessDefinitions.Select(d => d.Export())).ContentDigest;
        EditorUtility.SetDirty(map); AssetDatabase.SaveAssetIfDirty(map);
        CatalogBuildValidation.RequireProjectCurrent();
        return "Current: " + path;
    }
}
