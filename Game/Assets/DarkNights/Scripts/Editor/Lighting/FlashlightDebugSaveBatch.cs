using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DarkNights.View;
using DarkNights.View.Lighting;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>调参台一次确认的有限保存事务；先验证所有目标和冲突，再备份、写回及记录历史，失败恢复已写目标且保留全部临时草稿。</summary>
    public sealed class FlashlightDebugSaveBatch
    {
        private readonly FlashlightDebugDraft light;
        private readonly SceneLightingProfile scene, source;
        private readonly ExplorationLightSettings original;
        private readonly FlashlightDebugRecord record;
        private readonly bool shared;
        private readonly List<UnityEngine.Object> targets = new List<UnityEngine.Object>();
        private readonly Dictionary<string, string> hashes = new Dictionary<string, string>();
        private readonly Dictionary<UnityEngine.Object, string> json = new Dictionary<UnityEngine.Object, string>();
        private readonly string[] sceneChanges;
        private GameObject prefab;
        private Vector3 oldPosition, oldRotation, oldScale;

        public FlashlightDebugSaveBatch(FlashlightDebugDraft light, SceneLightingProfile source,
            SceneLightingProfile scene, ExplorationLightSettings original, FlashlightDebugRecord record, bool shared)
        {
            this.light = light; this.source = source; this.scene = scene; this.original = original; this.record = record; this.shared = shared;
            sceneChanges = scene != null && original != null ? FlashlightDebugSave.SceneFields.Where(name =>
                !Equals(Member(original, name), Member(scene.Settings, name))).ToArray() : Array.Empty<string>();
        }
        private static object Member(ExplorationLightSettings value, string name) => typeof(ExplorationLightSettings).GetField(name).GetValue(value);
        private static string Hash(string path)
        { using (var algorithm = SHA256.Create()) return Convert.ToBase64String(algorithm.ComputeHash(File.ReadAllBytes(path))); }

        private void Add(UnityEngine.Object target)
        {
            if (target == null || !EditorUtility.IsPersistent(target)) throw new InvalidOperationException("保存目标必须是现有项目资产。");
            if (targets.Contains(target)) return;
            string path = AssetDatabase.GetAssetPath(target);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !AssetDatabase.IsOpenForEdit(path))
                throw new InvalidOperationException("保存目标不可写：" + path);
            if (EditorUtility.IsDirty(target)) throw new InvalidOperationException("目标包含其他未保存编辑，请先处理：" + path);
            targets.Add(target); hashes.Add(path, Hash(path));
        }
        private void Prepare()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("等待编译与导入完成后再保存。");
            record.Validate(); light?.Snapshot(); scene?.Settings.Validate();
            if (light?.HasLightChanges == true)
            {
                light.ValidateSaveTarget(shared);
                if (shared && light.HasPresetChange && light.Definition == null) throw new InvalidOperationException("缺少预设引用所属的 Definition。");
                if (shared && light.Values.Mask != Core.Config.LightOverrideMask.None) Add(light.Preset);
                if (!shared || light.HasPresetChange) Add(light.Definition);
                if (light.HasPresetChange && light.Preset != null)
                {
                    var settings = AddressableAssetSettingsDefaultObject.Settings;
                    if (settings == null) throw new InvalidOperationException("缺少现有 Addressables 配置。");
                    if (settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(light.Preset))) == null)
                    { Add(settings); Add(settings.DefaultGroup); }
                }
            }
            if (sceneChanges.Length != 0)
            {
                foreach (string name in sceneChanges)
                    if (!Equals(Member(original, name), Member(source.Settings, name)))
                        throw new InvalidOperationException("场景字段已被其他编辑修改：" + name + "。草稿保留。");
                Add(source);
            }
            if (light?.HasMountChanges == true)
            {
                string path = AssetDatabase.GUIDToAssetPath(light.Definition.PrefabRef.AssetGUID);
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.assetPath == path && stage.scene.isDirty) throw new InvalidOperationException("对应 Prefab Stage 有未保存修改，请先处理。");
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); Add(prefab);
                var view = prefab.GetComponent<FlashlightView>();
                if (view == null || view.Emitter == null) throw new InvalidOperationException("Prefab 缺少明确手电主视图或灯口绑定。");
                oldPosition = view.Emitter.localPosition; oldRotation = view.Emitter.localEulerAngles; oldScale = view.Emitter.localScale;
                light.ValidateMountSource(oldPosition, oldRotation, oldScale);
            }
        }
        public bool Execute()
        {
            Prepare();
            string summary = Summary();
            if (!EditorUtility.DisplayDialog("保存本轮全部调参", summary, "确认保存全部", "返回调参")) return false;
            foreach (var target in targets)
            {
                string path = AssetDatabase.GetAssetPath(target);
                if (EditorUtility.IsDirty(target) || Hash(path) != hashes[path]) throw new InvalidOperationException("确认期间目标发生变化，尚未写入：" + path);
            }
            string backup = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/lighting-tuner-uitk-20261013/saves", record.Id));
            Directory.CreateDirectory(backup);
            foreach (var target in targets)
            {
                string path = AssetDatabase.GetAssetPath(target), copy = Path.Combine(backup, path);
                Directory.CreateDirectory(Path.GetDirectoryName(copy)); File.Copy(path, copy);
                if (target != prefab) json.Add(target, EditorJsonUtility.ToJson(target));
            }
            File.WriteAllText(Path.Combine(backup, "保存清单.txt"), summary);
            bool mountAttempted = false;
            try
            {
                if (light?.HasLightChanges == true)
                {
                    if (shared && light.Values.Mask != Core.Config.LightOverrideMask.None && !FlashlightDebugSave.Profile(light, false, false)) throw new IOException("共享预设保存未完成。");
                    if ((!shared || light.HasPresetChange) && !FlashlightDebugSave.Definition(light, false, false, !shared)) throw new IOException("Definition 保存未完成。");
                }
                if (sceneChanges.Length != 0 && !FlashlightDebugSave.Scene(source, scene, original, false)) throw new IOException("场景配置保存未完成。");
                if (light?.HasMountChanges == true)
                { mountAttempted = true; if (!FlashlightDebugSave.Mount(light, false, false)) throw new IOException("灯口保存未完成。"); }
                FlashlightDebugRecordStore.Save(record);
            }
            catch (Exception failure)
            {
                var failures = new List<Exception> { failure };
                foreach (var entry in json.Reverse())
                    try { EditorJsonUtility.FromJsonOverwrite(entry.Value, entry.Key); EditorUtility.SetDirty(entry.Key); AssetDatabase.SaveAssetIfDirty(entry.Key); }
                    catch (Exception restore) { failures.Add(restore); }
                if (mountAttempted)
                    try { RestoreMount(); } catch (Exception restore) { failures.Add(restore); }
                File.WriteAllText(Path.Combine(backup, "失败与恢复.txt"), string.Join("\n", failures.Select(error => error.ToString())));
                LightingEditorRefresh.Request();
                if (failures.Count > 1) throw new AggregateException("保存失败且存在未完成恢复；草稿与备份保留：" + backup, failures);
                throw new InvalidOperationException("保存失败，已恢复作者资产；草稿保留。证据：" + backup, failure);
            }
            if (light?.HasLightChanges == true) light.AcceptSavedLight();
            if (light?.HasMountChanges == true) light.AcceptSavedMount();
            return true;
        }
        private void RestoreMount()
        {
            string path = AssetDatabase.GetAssetPath(prefab); var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<FlashlightView>();
                if (view == null || view.Emitter == null) throw new InvalidOperationException("灯口恢复缺少原绑定。");
                view.Emitter.localPosition = oldPosition; view.Emitter.localEulerAngles = oldRotation; view.Emitter.localScale = oldScale;
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success) throw new IOException("灯口恢复保存失败。");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public string Summary()
        {
            var rows = new List<string>();
            if (light?.HasLightChanges == true)
            {
                string fields = string.Join("、", LightParameterSchema.Fields.Where(item => light.Values.Has(LightParameterSchema.Mask(item.Name))).Select(item => item.Label));
                if (!shared || light.Values.Mask != Core.Config.LightOverrideMask.None) rows.Add("单灯 → " + AssetDatabase.GetAssetPath(shared ? (UnityEngine.Object)light.Preset : light.Definition) + "\n修改：" + fields);
                if (light.HasPresetChange) rows.Add("预设引用 → " + AssetDatabase.GetAssetPath(light.Definition));
                rows.Add(shared ? "共享预设影响所有继承者；Definition 已覆盖项仍优先。" : "单灯修改影响使用当前 Definition 的实例。");
            }
            if (sceneChanges.Length != 0) rows.Add("场景 → " + AssetDatabase.GetAssetPath(source) + "\n修改：" + string.Join("、", sceneChanges) + "；影响引用此配置的关卡。");
            if (light?.HasMountChanges == true) rows.Add("灯口 → " + AssetDatabase.GetAssetPath(prefab) + "\n仅修改绑定 Emitter 的 Position／Rotation／Scale。");
            rows.Add("调试记录 → " + FlashlightDebugRecordStore.RecordPath(record) + "\n名称：" + record.Name + "；包含完整调参快照、地图、位置、旋转、方向及镜头。");
            rows.Add("全部完成才清除草稿历史；取消或失败保留未保存调整。预览记录不写玩家存档。");
            return string.Join("\n\n", rows);
        }
    }
}
