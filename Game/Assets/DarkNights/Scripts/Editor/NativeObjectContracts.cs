using System;
using System.Linq;
using DarkNights.Runtime.Framework;
using DarkNights.Runtime.Network;
using DarkNights.View;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using UnityEditor;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Editor
{
    /// <summary>
    /// 只读核验正式定义、生成注册、Prefab 及显式组件引用的制作合同。
    /// 不升级或修复现有资源；定义 Key 可独立改名，规则身份由家族 RuleKey 配置决定。
    /// </summary>
    public static class NativeObjectContracts
    {
        private const string ObjectsRoot = "Assets/DarkNights/Res/Objects/";
        private static readonly string[] Names =
        {
            "Worker", "Spearman", "Archer", "Zombie", "Ghoul", "Armored",
            "House", "Tavern", "Barracks", "Farm", "Tower", "Trees", "Stone", "Iron", "Farmland"
        };

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

        public static Type ViewType(string name)
        {
            int index = Array.IndexOf(Names, name);
            if (index < 0) throw new ArgumentException("Unknown formal local object: " + name, nameof(name));
            return index < 6 ? typeof(ActorView) : index < 11 ? typeof(BuildingView) : typeof(WorksiteView);
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
            bool requireBehaviour = true)
        {
            if (definition == null || prefab == null) throw new InvalidOperationException("Local definition or Prefab missing: " + name);
            Type expected = PresentationType(name);
            RequireGenerated(expected);
            ValidateBehaviour(definition, expected, requireBehaviour);
            ValidateIdentity(definition, prefab, null, NetworkType.Local);
            if (DefinitionRuleIndex.RuleKey(definition) != NativeArtSetup.ContentId(name))
                throw new InvalidOperationException("Authored rule does not match its native object: " + name);
            var instance = prefab.GetComponent<ObjectInstance>();
            var view = prefab.GetComponent<EntityView>();
            var initializer = prefab.GetComponent<LocalObjectInstanceInitializer>();
            view?.BuildRuntimeCache();
            if (instance == null || view == null || initializer == null || instance.ObjectView != view ||
                initializer.ObjectInstance != instance || (UnityEngine.Object)view.Initializer != initializer ||
                view.GetType() != ViewType(name) || view.gameObject != prefab ||
                view.Bindings.Any(binding => binding.Key == "visual" || binding.Key == "art_offset" ||
                    binding.Key == "facing" || binding.Key == "status_anchor" || binding.Key == "selection_anchor"))
                throw new InvalidOperationException("Local instance does not use its dedicated EntityView as the sole primary view: " + name);
            if (view.StatusAnchor == null || view.SelectionAnchor == null || view.Portrait == null || view.TintTargetCount == 0)
                throw new InvalidOperationException("EntityView common authoring references are incomplete: " + name);
            var visualData = new SerializedObject(view);
            var sorting = visualData.FindProperty("sorting").objectReferenceValue as UnityEngine.Rendering.SortingGroup;
            int order = expected == typeof(ActorPresentationBehaviour) ? 100 : expected == typeof(WorksitePresentationBehaviour) ? 10 : 0;
            if (sorting == null || sorting.gameObject != prefab || sorting.sortingOrder != order)
                throw new InvalidOperationException("Explicit native sorting binding is incomplete: " + name);
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
                (UnityEngine.Object)view.Initializer != synchronizer || (link.Instance != null && link.Instance != instance) ||
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
            if (definition.GuidString != AssetDatabase.AssetPathToGUID(path) || string.IsNullOrWhiteSpace(definition.Key) || (key != null && definition.Key != key) ||
                definition.Id != 0 || definition.LegacyIdAliases.Count != 0 || definition.NetType != networkType ||
                definition.PrefabRef == null || definition.PrefabRef.AssetGUID != AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)))
                throw new InvalidOperationException("Definition identity/PrefabRef mismatch: " + path);
        }

        private static T Required<T>(string path) where T : UnityEngine.Object
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            return value != null ? value : throw new InvalidOperationException("Required asset is missing: " + path);
        }

    }
}
