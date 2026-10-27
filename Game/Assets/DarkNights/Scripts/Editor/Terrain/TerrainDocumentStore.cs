using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地图配置文件的保存、另存与显式新局装配；同一配置保持路径和GUID，派生数据就绪后才更新引用。
    /// 保存不写共享源，装配使用外部冲突检查并在失败时恢复原正式配置，不覆盖旧版本。
    /// </summary>
    internal static class TerrainDocumentStore
    {
        internal const string Root = "Assets/DarkNights/Res/Terrain/StrataCave/MapDocuments";

        internal static TerrainMapDocument Save(MapWorkbenchWindow owner)
        {
            if (owner.State.Document == null) return SaveAs(owner);
            return SaveTo(owner, AssetDatabase.GetAssetPath(owner.State.Document));
        }

        internal static TerrainMapDocument SaveAs(MapWorkbenchWindow owner)
        {
            owner.Commands.Guard();
            SurfaceSceneryAuthoring.EnsureFolder(Root);
            string name = owner.State.Document != null ? owner.State.Document.name + "-副本" : "地图配置";
            string path = EditorUtility.SaveFilePanelInProject("保存地图配置", name, "asset", "选择配置文件位置。", Root);
            if (string.IsNullOrEmpty(path)) return null;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path))
                throw new IOException("该文件已存在；保存当前配置请使用保存，另存为请选择新文件名。");
            return SaveTo(owner, path);
        }

        /// <summary>先写齐本次地图和私有表现，再更新同一配置文件；失败保留旧引用及新产生的诊断资产。</summary>
        internal static TerrainMapDocument SaveTo(MapWorkbenchWindow owner, string documentPath)
        {
            owner.Commands.Guard();
            var model = owner.Generation;
            if (!owner.Preview.SaveReady)
                throw new InvalidOperationException("请等待当前地图计算与编辑上下文安装完成，再保存；无需等待美术加载。");
            if (model.GeologyConflict.Length != 0) throw new InvalidOperationException(model.GeologyConflict);
            if (owner.Style == null) throw new InvalidOperationException("配置缺少表现设置，不能保存不完整文件。");
            owner.Draft.Config.Validate();
            if (owner.Draft.Config.CaveMap.Seed != model.LastCandidate.Seed)
                throw new InvalidOperationException("本次种子与候选不一致，请重新生成后保存。");
            var environment = owner.Styles.Draft(owner.Style).SurfaceEnvironment;
            var layout = View.Expedition.SurfaceSceneryLayout.Find(environment, model.LastCandidate.Background);
            if (layout != null) SurfaceSceneryAuthoring.Validate(owner.Styles.Draft(layout), model.LastCandidate);
            var existing = AssetDatabase.LoadAssetAtPath<TerrainMapDocument>(documentPath);
            if (!documentPath.StartsWith("Assets/", StringComparison.Ordinal) || !documentPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                throw new IOException("配置必须保存在本工程Assets内的.asset文件。");
            if (existing != null && (existing != owner.State.Document ||
                EditorJsonUtility.ToJson(existing) != owner.State.DocumentBaseline || EditorUtility.IsDirty(existing)))
                throw new InvalidOperationException("配置文件已在窗口外修改；请重新打开或另存为，避免覆盖。");
            if (existing == null && (File.Exists(documentPath) || AssetDatabase.LoadMainAssetAtPath(documentPath) != null))
                throw new IOException("目标位置已有其他文件，请选择新位置。");
            string json = ReviewedMineMap.Encode(model.LastCandidate, "map-workbench-v2");
            string parent = Path.GetDirectoryName(documentPath).Replace('\\', '/');
            SurfaceSceneryAuthoring.EnsureFolder(parent);
            string dataRoot = parent + "/" + Path.GetFileNameWithoutExtension(documentPath) + "-Data";
            string folder = dataRoot + "/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            if (Directory.Exists(folder)) throw new IOException("配置数据路径已存在。");
            SurfaceSceneryAuthoring.EnsureFolder(folder);
            string path = folder + "/layout.json";
            File.WriteAllText(path, json, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var map = ScriptableObject.CreateInstance<ReviewedMineMap>();
            map.Content = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            map.SourcePlanetId = model.PlanetId;
            map.SourceIdentity = ReviewedMineMap.SourceFingerprint(owner.Draft.Config, model.PlanetId);
            map.Recipe = "map-workbench-v2";
            using (var hash = SHA256.Create())
                map.ContentSha256 = BitConverter.ToString(hash.ComputeHash(map.Content.bytes)).Replace("-", "").ToLowerInvariant();
            map.Freeze(TerrainGenerationPreview.WorldId);
            AssetDatabase.CreateAsset(map, folder + "/Map.asset");
            var style = CopyStyle(owner, folder);
            var design = TerrainDesignCache.Save(folder, model.LastCandidate, map.ContentSha256);
            var document = existing != null ? existing : ScriptableObject.CreateInstance<TerrainMapDocument>();
            string before = existing != null ? EditorJsonUtility.ToJson(existing) : null;
            try
            {
                document.Input = ExpeditionFlowDraft.Clone(owner.Draft.Config);
                document.Input.CaveMap.SeedSelection =
                    (owner.Draft.Config.CaveMap.SeedSelection ?? new Core.Config.Terrain.TerrainSeedSettings()).CopyValidated();
                document.Input.CaveMap = document.Input.FreezeCaveMap();
                document.Input.ModifierSchemaVersion = 1;
                document.Input.Modifiers.Clear();
                document.Input.ReviewedMap = null;
                document.Map = map; document.Style = style; document.PlanetId = model.PlanetId;
                document.DesignPreview = design; document.DesignPreviewSource = map.ContentSha256;
                document.GenerationMetadata = TerrainGenerationMetadata.Encode(model.Result, map.ContentSha256);
                map.VerifySource(document.Input); document.Version = 2;
                if (existing == null) AssetDatabase.CreateAsset(document, documentPath);
                EditorUtility.SetDirty(document);
                AssetDatabase.SaveAssetIfDirty(map); AssetDatabase.SaveAssetIfDirty(document);
            }
            catch
            {
                if (before != null)
                { EditorJsonUtility.FromJsonOverwrite(before, document); EditorUtility.SetDirty(document); AssetDatabase.SaveAssetIfDirty(document); }
                throw;
            }
            owner.State.Document = document; owner.State.DocumentBaseline = EditorJsonUtility.ToJson(document);
            owner.Draft.MarkSaved(); owner.Styles.MarkSaved();
            model.MarkCandidateSaved();
            Selection.activeObject = document;
            owner.Notice = "已保存配置：" + documentPath;
            owner.Refresh();
            return document;
        }

        private static CaveTerrainStyle CopyStyle(MapWorkbenchWindow owner, string folder)
        {
            var copies = new Dictionary<ScriptableObject, ScriptableObject>();
            ScriptableObject Copy(ScriptableObject source)
            {
                if (source == null) return null;
                if (copies.TryGetValue(source, out var found)) return found;
                var value = UnityEngine.Object.Instantiate(owner.Styles.Draft(source));
                value.hideFlags = HideFlags.None; value.name = source.name;
                copies.Add(source, value);
                AssetDatabase.CreateAsset(value, folder + "/Art-" + copies.Count + "-" + source.GetType().Name + ".asset");
                using (var data = new SerializedObject(value))
                {
                    var iterator = data.GetIterator();
                    while (iterator.Next(true))
                    {
                        if (iterator.propertyType != SerializedPropertyType.ObjectReference ||
                            !(iterator.objectReferenceValue is ScriptableObject asset) ||
                            !(asset.GetType().Namespace ?? "").StartsWith("DarkNights.View", StringComparison.Ordinal)) continue;
                        iterator.objectReferenceValue = Copy(asset);
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(value);
                AssetDatabase.SaveAssetIfDirty(value);
                return value;
            }
            return (CaveTerrainStyle)Copy(owner.Style);
        }

        internal static void Open(MapWorkbenchWindow owner, TerrainMapDocument document)
        { owner.FileLoad.Open(document); }

        /// <summary>仅在异步读取与来源复核成功后一次安装文档；首帧复用同份已保存地图，不再次随机生成。</summary>
        internal static void OpenPrepared(MapWorkbenchWindow owner, TerrainMapDocument document, ExpeditionFlowConfig input,
            CaveTerrainStyle style, string planet, string baseline, DarkNights.Core.Config.Terrain.PlayableTerrain map,
            DarkNights.Core.Config.Terrain.CaveRulePlan plan, bool asCopy,
            DarkNights.Core.Config.Terrain.TerrainGenerationResult result = null)
        {
            Undo.ClearUndo(owner.Draft); owner.Preview.Map.Cancel();
            owner.Draft.LoadDocument(input);
            owner.SetDocumentStyle(style);
            owner.State.Document = asCopy ? null : document; owner.State.DocumentBaseline = asCopy ? "" : baseline;
            owner.State.SelectedRegion = 0; owner.State.Pick = 0; owner.State.SelectedSite = "";
            owner.State.ViewedMap = null;
            owner.Generation.PlanetId = planet;
            owner.Draft.MarkSaved();
            owner.Generation.AcceptSaved(map, plan, result);
            owner.Preview.RequestOpen(); owner.SelectPage("space");
            owner.Notice = asCopy ? "已从 " + document.name + " 创建未命名副本；保存时选择新文件。" :
                "已打开配置：" + AssetDatabase.GetAssetPath(document) + "；保存将更新此文件。";
        }

        internal static void Install(MapWorkbenchWindow owner)
        {
            owner.Commands.Guard();
            var document = owner.State.Document;
            if (document == null || owner.Generation.HasChanges || owner.Generation.CandidateNeedsSave || owner.Styles.HasChanges ||
                owner.Generation.PlanetId != document.PlanetId)
                throw new InvalidOperationException("先保存当前配置，再用于新局。");
            if (EditorUtility.IsDirty(document) || EditorJsonUtility.ToJson(document) != owner.State.DocumentBaseline)
                throw new InvalidOperationException("配置文件已在窗口外修改，请先重新打开。");
            document.Map.VerifySource(document.Input);
            var source = owner.Draft.Source;
            var current = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            if (EditorUtility.IsDirty(source) || current.CanonicalIdentity() != owner.Draft.BaselineJson)
                throw new InvalidOperationException("正式配置已在窗口外变化；请重新打开这份已保存地图以同步基线，再执行用于新局。");
            var profile = source.SharedConfigs.OfType<TerrainProfileConfig>().Single();
            string before = EditorJsonUtility.ToJson(source);
            var next = ExpeditionFlowDraft.Clone(document.Input);
            next.Enabled = current.Enabled; next.Navigation = current.Navigation;
            next.PreparationTimeoutSeconds = current.PreparationTimeoutSeconds;
            next.ArrivalTimeoutSeconds = current.ArrivalTimeoutSeconds;
            next.ReviewedMap = document.Map;
            document.Map.VerifySource(next);
            Undo.RecordObject(source, "将已保存地图用于新局");
            try
            {
                source.SharedConfigs[source.SharedConfigs.IndexOf(current)] = next;
                profile.CaveStyle = document.Style; profile.BackgroundStyle = document.Style.Background;
                Undo.FlushUndoRecordObjects();
                EditorUtility.SetDirty(source); AssetDatabase.SaveAssetIfDirty(source);
                owner.Draft.RefreshSourceBaseline();
            }
            catch
            {
                EditorJsonUtility.FromJsonOverwrite(before, source);
                EditorUtility.SetDirty(source); AssetDatabase.SaveAssetIfDirty(source);
                throw;
            }
            owner.Notice = "已将当前配置用于后续新局。";
        }
    }
}
