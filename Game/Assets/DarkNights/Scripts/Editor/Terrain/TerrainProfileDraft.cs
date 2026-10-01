using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnyRules.Next.Authoring;
using AnyRules.Next.Editor;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>ObjectDefinition 网格业务草稿；显式应用才保存，编译输出采用新路径，保留原生作者资产、旧目录和全部 GUID。</summary>
    internal sealed class TerrainProfileDraft : ScriptableObject
    {
        public TerrainProfileConfig Profile;
        public HandheldConfig Tools;
        public MineralDepositRuleConfig Deposits;
        internal ObjectDefinition Source { get; private set; }
        internal ObjectDefinition DepositSource { get; private set; }
        internal EditorAnyRuleDDatabase Catalog { get; private set; }
        internal List<TerrainDurabilityDraft> Durability { get; } = new List<TerrainDurabilityDraft>();
        private string baseline, depositBaseline;
        private ARDMapDefinition compiledSource;
        private bool contour;
        internal void Load()
        {
            Source = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset");
            DepositSource = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/MineralDeposit/MineralDeposit.asset");
            if (Source == null || DepositSource == null) throw new InvalidOperationException("缺少正式会话或矿床 Definition。");
            var profile = Source.SharedConfigs.OfType<TerrainProfileConfig>().Single();
            var tools = Source.SharedConfigs.OfType<HandheldConfig>().Single();
            var deposits = DepositSource.SharedConfigs.OfType<MineralDepositRuleConfig>().Single();
            baseline = Identity(profile, tools); depositBaseline = JsonUtility.ToJson(deposits);
            Profile = Copy(profile); Tools = Copy(tools); Deposits = Copy(deposits);
            LoadCatalog(false);
        }
        internal void LoadCatalog(bool useContour)
        {
            contour = useContour; compiledSource = contour ? Profile.ContourDefinition : Profile.Definition;
            Catalog = compiledSource == null ? null : AssetDatabase.LoadAssetAtPath<EditorAnyRuleDDatabase>(
                AssetDatabase.GUIDToAssetPath(compiledSource.AuthoringAssetGuid));
            Durability.Clear();
            if (Catalog != null) foreach (var terrain in Catalog.Terrains)
                if (terrain != null) Durability.Add(new TerrainDurabilityDraft(terrain));
        }
        internal void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("请退出 Play 并等待编译完成后再应用配置。");
            if (Identity(Source.SharedConfigs.OfType<TerrainProfileConfig>().Single(), Source.SharedConfigs.OfType<HandheldConfig>().Single()) != baseline ||
                JsonUtility.ToJson(DepositSource.SharedConfigs.OfType<MineralDepositRuleConfig>().Single()) != depositBaseline)
                throw new InvalidOperationException("ObjectDefinition 配置已被其他编辑修改，请重新加载。");
            if (Catalog == null || compiledSource == null || compiledSource != (contour ? Profile.ContourDefinition : Profile.Definition))
                throw new InvalidOperationException("请选择带作者来源的目录，并重新载入原生材质草稿。");
            Tools.Validate(); Deposits.Validate(); Profile.Freeze();
            foreach (var entry in Durability) entry.RequireUnchanged();
            foreach (var group in Durability.GroupBy(entry => entry.Target))
                if (group.Select(entry => (entry.HitPoints, entry.Hardness, entry.Stages)).Distinct().Count() != 1)
                    throw new InvalidOperationException("共享 GameplayDefinition 的材质配置冲突。");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            try
            {
                foreach (var entry in Durability) entry.Apply();
                string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(compiledSource)).Replace('\\', '/');
                string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/MiningProfile.asset");
                var compiled = RuleCatalogBuild.CompileToNewAssets(Catalog, path);
                compiled.Width = compiledSource.Width; compiled.Height = compiledSource.Height;
                compiled.MinU = compiledSource.MinU; compiled.MinV = compiledSource.MinV;
                compiled.ChunkSize = compiledSource.ChunkSize; compiled.PageSize = compiledSource.PageSize; compiled.CellSize = compiledSource.CellSize;
                EditorUtility.SetDirty(compiled);
                if (Profile.Definition == compiledSource) Profile.Definition = compiled;
                if (Profile.ContourDefinition == compiledSource) Profile.ContourDefinition = compiled;
                Profile.Freeze(); if (Profile.ContourDefinition != null) Profile.Freeze(Profile.ContourDefinition);
                Undo.RecordObject(Source, "应用网格业务 Profile");
                Undo.RecordObject(DepositSource, "应用矿床采集配置");
                Replace(Source, Profile); Replace(Source, Tools); Replace(DepositSource, Deposits);
                EditorUtility.SetDirty(Source); EditorUtility.SetDirty(DepositSource);
                foreach (var entry in Durability) AssetDatabase.SaveAssetIfDirty(entry.Target);
                AssetDatabase.SaveAssetIfDirty(compiled); AssetDatabase.SaveAssetIfDirty(Source); AssetDatabase.SaveAssetIfDirty(DepositSource);
                Undo.CollapseUndoOperations(undo); Load();
            }
            catch
            {
                Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }
        private static string Identity(TerrainProfileConfig profile, HandheldConfig tools) => JsonUtility.ToJson(profile) + "|" + JsonUtility.ToJson(tools);
        private static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        private static void Replace<T>(ObjectDefinition definition, T config) where T : GameCore.Objects.Behaviours.Interfaces.IConfigData
        {
            var previous = definition.SharedConfigs.OfType<T>().Single();
            definition.SharedConfigs[definition.SharedConfigs.IndexOf(previous)] = Copy(config);
        }
    }
}
