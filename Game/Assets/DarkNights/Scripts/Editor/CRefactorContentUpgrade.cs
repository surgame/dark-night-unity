using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Framework;
using DarkNights.Runtime.Network;
using DarkNights.View;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using GameCore.Objects.Views;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Editor
{
    /// <summary>
    /// 对既有十六个定义及会话 Prefab 进行有限、可重复的 C 方案升级。
    /// 全部身份、生成注册和绑定预检通过后才追加行为与显式引用；不重建外观或改写其他定义字段。
    /// </summary>
    public static class CRefactorContentUpgrade
    {
        public const string ReportPath = "../artifacts/c-refactor/content-upgrade.json";
        public const string FarmPrefabPath = "Assets/DarkNights/Res/Objects/Farm/Farm.prefab";
        private const string ObjectsRoot = "Assets/DarkNights/Res/Objects/";
        private static readonly string[] Names =
        {
            "Worker", "Spearman", "Archer", "Zombie", "Ghoul", "Armored",
            "House", "Tavern", "Barracks", "Farm", "Tower", "Trees", "Stone", "Iron", "Farmland"
        };

        [MenuItem("Dark Nights/Content/Upgrade C Refactor Behaviours")]
        public static void Upgrade()
        {
            var completed = new JArray();
            var changed = new JArray();
            var failures = new List<string>();
            var definitions = new List<(ObjectDefinition Definition, Type Behaviour)>();
            GameObject sessionRoot = null;
            GameObject farmRoot = null;
            string stage = "preflight";
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                    throw new InvalidOperationException("Upgrade requires a compiled, idle Editor outside Play mode.");
                foreach (string name in Names)
                {
                    try
                    {
                        string path = ObjectsRoot + name + "/" + name;
                        var definition = Required<ObjectDefinition>(path + ".asset");
                        if (name == "Farm") farmRoot = PrefabUtility.LoadPrefabContents(FarmPrefabPath);
                        ValidateLocal(name, definition, Required<GameObject>(path + ".prefab"), false, name == "Farm");
                        definitions.Add((definition, PresentationType(name)));
                    }
                    catch (Exception error) { failures.Add(name + ": " + error.Message); }
                }
                try
                {
                    var definition = Required<ObjectDefinition>(FormalObjectContentSetup.SessionDefinitionPath);
                    ValidateSession(definition, false);
                    sessionRoot = PrefabUtility.LoadPrefabContents(FormalObjectContentSetup.SessionPrefabPath);
                    ValidateSessionLink(sessionRoot, false);
                    definitions.Add((definition, typeof(CampSessionBehaviour)));
                }
                catch (Exception error) { failures.Add("WorldSession: " + error.Message); }
                if (failures.Count != 0) throw new InvalidOperationException(string.Join("\n", failures));

                stage = "save";
                foreach (var item in definitions)
                {
                    string path = AssetDatabase.GetAssetPath(item.Definition);
                    if (!item.Definition.BehaviourTypes.Any(value => BehaviourTypeResolver.GetTypeFromName(value) == item.Behaviour))
                    {
                        item.Definition.BehaviourTypes.Add(item.Behaviour.FullName);
                        EditorUtility.SetDirty(item.Definition);
                        AssetDatabase.SaveAssetIfDirty(item.Definition);
                        changed.Add(path);
                    }
                    completed.Add(path);
                }
                var link = sessionRoot.GetComponent<SessionObjectLink>();
                bool referenceChanged = Reference(link, "instance", sessionRoot.GetComponent<ObjectInstance>());
                referenceChanged |= Reference(link, "synchronizer", sessionRoot.GetComponent<StateSynchronizer>());
                if (referenceChanged)
                {
                    if (PrefabUtility.SaveAsPrefabAsset(sessionRoot, FormalObjectContentSetup.SessionPrefabPath) == null)
                        throw new InvalidOperationException("Saving WorldSession Prefab failed.");
                    changed.Add(FormalObjectContentSetup.SessionPrefabPath);
                }
                completed.Add(FormalObjectContentSetup.SessionPrefabPath);
                if (RepairAuthorizedFarmKey(farmRoot))
                {
                    if (PrefabUtility.SaveAsPrefabAsset(farmRoot, FarmPrefabPath) == null)
                        throw new InvalidOperationException("Saving the authorized Farm binding-key repair failed.");
                    changed.Add(FarmPrefabPath);
                }
                completed.Add(FarmPrefabPath);
                stage = "verify";
                Validate();
                WriteReport(true, stage, completed, changed, new JArray());
                Debug.Log("DARK_NIGHTS_C_CONTENT_UPGRADED definitions=16 local=15 prefabs=2 changed=" + changed.Count + " report=" + ReportPath);
            }
            catch (Exception error)
            {
                WriteReport(false, stage, completed, changed, failures.Count == 0 ? new JArray(error.Message) : new JArray(failures));
                throw;
            }
            finally
            {
                if (sessionRoot != null) PrefabUtility.UnloadPrefabContents(sessionRoot);
                if (farmRoot != null) PrefabUtility.UnloadPrefabContents(farmRoot);
            }
        }

        public static void Validate()
        {
            foreach (string name in Names)
            {
                string path = ObjectsRoot + name + "/" + name;
                ValidateLocal(name, Required<ObjectDefinition>(path + ".asset"), Required<GameObject>(path + ".prefab"));
            }
            ValidateSession(Required<ObjectDefinition>(FormalObjectContentSetup.SessionDefinitionPath));
            ValidateSessionLink(Required<GameObject>(FormalObjectContentSetup.SessionPrefabPath));
        }

        public static Type PresentationType(string name)
        {
            int index = Array.IndexOf(Names, name);
            if (index < 0) throw new ArgumentException("Unknown formal local object: " + name, nameof(name));
            return index < 6 ? typeof(ActorPresentationBehaviour) : index < 11 ?
                typeof(BuildingPresentationBehaviour) : typeof(WorksitePresentationBehaviour);
        }

        public static void RequireGenerated(Type type)
        {
            if (!BehaviourTypeResolver.Factories.TryGetValue(type, out var factory) || factory == null ||
                !BehaviourTypeResolver.TypeStringMap.TryGetValue(type.FullName, out Type registered) || registered != type)
                throw new InvalidOperationException("Generated Behaviour registry is missing: " + type.FullName);
            if (BehaviourTypeResolver.Factories.ContainsKey(typeof(EntityPresentationBehaviour)))
                throw new InvalidOperationException("Abstract presentation base must not be registered as a concrete Behaviour.");
        }

        public static void ValidateLocal(string name, ObjectDefinition definition, GameObject prefab,
            bool requireBehaviour = true, bool allowAuthorizedFarmKey = false)
        {
            if (definition == null || prefab == null) throw new InvalidOperationException("Local definition or Prefab missing: " + name);
            Type expected = PresentationType(name);
            RequireGenerated(expected);
            ValidateBehaviour(definition, expected, requireBehaviour);
            string prefix = expected == typeof(ActorPresentationBehaviour) ? "unit." :
                expected == typeof(BuildingPresentationBehaviour) ? "building." : "worksite.";
            ValidateIdentity(definition, prefab, prefix + NativeArtSetup.ContentId(name), NetworkType.Local);
            var instance = prefab.GetComponent<ObjectInstance>();
            var view = prefab.GetComponent<ObjectView>();
            var initializer = prefab.GetComponent<LocalObjectInstanceInitializer>();
            view?.BuildRuntimeCache();
            string key = allowAuthorizedFarmKey && name == "Farm" && view != null &&
                view.Bindings.Count(binding => binding.Key == "visual") == 0 ? "Farm" : "visual";
            if (instance == null || view == null || initializer == null || instance.ObjectView != view ||
                initializer.ObjectInstance != instance || view.Initializer != initializer ||
                view.Bindings.Count(binding => binding.Key == key) != 1 ||
                view.Get<NativeVisual>(key) == null || view.Get<NativeVisual>(key).gameObject != prefab)
                throw new InvalidOperationException("Explicit local instance/initializer/visual binding is incomplete: " + name);
            foreach (string value in definition.BehaviourTypes)
            {
                Type other = BehaviourTypeResolver.GetTypeFromName(value);
                if (other == null || (typeof(EntityPresentationBehaviour).IsAssignableFrom(other) && other != expected))
                    throw new InvalidOperationException("Unknown or conflicting local Behaviour: " + name + "/" + value);
            }
        }

        public static void ValidateSession(ObjectDefinition definition, bool requireBehaviour = true)
        {
            RequireGenerated(typeof(WorldSessionBehaviour));
            RequireGenerated(typeof(CampSessionBehaviour));
            ValidateBehaviour(definition, typeof(WorldSessionBehaviour), true);
            ValidateBehaviour(definition, typeof(CampSessionBehaviour), requireBehaviour);
            ValidateIdentity(definition, Required<GameObject>(FormalObjectContentSetup.SessionPrefabPath),
                FormalObjectCatalog.SessionKey, NetworkType.Network);
            Type[] types = definition.BehaviourTypes.Select(BehaviourTypeResolver.GetTypeFromName).ToArray();
            int campIndex = Array.IndexOf(types, typeof(CampSessionBehaviour));
            if (types.Any(type => type == null) || types.Count(type => type == typeof(WorldSessionBehaviour)) != 1 ||
                (campIndex >= 0 && campIndex < Array.IndexOf(types, typeof(WorldSessionBehaviour))))
                throw new InvalidOperationException("WorldSession must retain its projection Behaviour before CampSessionBehaviour.");
        }

        public static void ValidateSessionLink(GameObject prefab, bool requireReferences = true)
        {
            var instance = prefab.GetComponent<ObjectInstance>();
            var synchronizer = prefab.GetComponent<StateSynchronizer>();
            var view = prefab.GetComponent<ObjectView>();
            var link = prefab.GetComponent<SessionObjectLink>();
            if (instance == null || synchronizer == null || view == null || link == null || instance.ObjectView != view ||
                view.Initializer != synchronizer || (link.Instance != null && link.Instance != instance) ||
                (link.Synchronizer != null && link.Synchronizer != synchronizer) ||
                (requireReferences && (link.Instance != instance || link.Synchronizer != synchronizer)))
                throw new InvalidOperationException("WorldSession instance/synchronizer/link references are incomplete or conflict.");
            var serialized = new SerializedObject(link);
            if (serialized.FindProperty("instance") == null || serialized.FindProperty("synchronizer") == null)
                throw new InvalidOperationException("WorldSession explicit Link fields are not compiled.");
        }

        private static void ValidateBehaviour(ObjectDefinition definition, Type type, bool required)
        {
            int count = definition.BehaviourTypes.Count(value => BehaviourTypeResolver.GetTypeFromName(value) == type);
            if (count > 1 || (required && count != 1))
                throw new InvalidOperationException("Behaviour must occur exactly once: " + definition.name + "/" + type.FullName);
        }

        private static void ValidateIdentity(ObjectDefinition definition, GameObject prefab, string key, NetworkType networkType)
        {
            string path = AssetDatabase.GetAssetPath(definition);
            if (definition.GuidString != AssetDatabase.AssetPathToGUID(path) || definition.Key != key ||
                definition.Id != 0 || definition.LegacyIdAliases.Count != 0 || definition.NetType != networkType ||
                definition.PrefabRef == null || definition.PrefabRef.AssetGUID != AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)))
                throw new InvalidOperationException("Definition identity/PrefabRef mismatch: " + path);
        }

        private static bool Reference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property.objectReferenceValue == value) return false;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool RepairAuthorizedFarmKey(GameObject prefab)
        {
            var view = prefab.GetComponent<ObjectView>();
            if (view.Bindings.Any(binding => binding.Key == "visual")) return false;
            // 用户仅授权恢复此既有键；直接改 key，保留 targetObject、target 和全部美术字段。
            var serialized = new SerializedObject(view);
            var bindings = serialized.FindProperty("bindings");
            for (int i = 0; i < bindings.arraySize; i++)
            {
                var key = bindings.GetArrayElementAtIndex(i).FindPropertyRelative("key");
                if (key.stringValue != "Farm") continue;
                key.stringValue = "visual";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                view.BuildRuntimeCache();
                return true;
            }
            throw new InvalidOperationException("Authorized Farm binding changed after preflight.");
        }

        private static T Required<T>(string path) where T : UnityEngine.Object
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            return value != null ? value : throw new InvalidOperationException("Required asset is missing: " + path);
        }

        private static void WriteReport(bool success, string stage, JArray completed, JArray changed, JArray failed)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, new JObject
            {
                ["success"] = success, ["stage"] = stage, ["utc"] = DateTime.UtcNow.ToString("O"),
                ["expectedDefinitions"] = 16, ["expectedLocalObjects"] = 15, ["expectedPrefabs"] = 2,
                ["authorizedRepair"] = "Farm Prefab binding key Farm -> visual; component references and art unchanged",
                ["completed"] = completed, ["changed"] = changed, ["failed"] = failed
            }.ToString());
        }
    }
}
