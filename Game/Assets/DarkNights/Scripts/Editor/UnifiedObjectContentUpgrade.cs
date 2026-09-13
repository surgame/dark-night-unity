using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Config;
using DarkNights.Runtime.Framework;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using GameCore.Objects.Behaviours.Interfaces;
using GameCore.Objects.Definition;
using GameCore.Objects.Types;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor
{
    /// <summary>
    /// U2 的显式一次性内容接入，保留所有现有 Prefab、资源 GUID 与场景布局。
    /// 只补规则配置、实际切片能力和缺失放置键；普通导入与构建不调用此入口。
    /// </summary>
    public static class UnifiedObjectContentUpgrade
    {
        [MenuItem("Dark Nights/Content/Apply Unified Object Slice")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorUtility.scriptCompilationFailed)
                throw new InvalidOperationException("Stop Play and resolve compilation before upgrading content.");
            var catalog = GameCatalogJson.Parse(File.ReadAllText("Assets/DarkNights/Res/Config/balance.json"),
                File.ReadAllText("Assets/DarkNights/Res/Config/pinewatch.json"));
            ObjectDefinitionDatabase database = ObjectDefinitionDatabase.Instance;
            var definitions = database.Definitions.Where(d => DefinitionRuleIndex.IsEntityType(d.Type)).ToArray();
            var input = definitions.Select(d => new
            {
                Definition = d, Rule = d.Key.Substring(d.Key.IndexOf('.') + 1), Family = d.Type
            }).ToArray();
            foreach (var entry in input)
            {
                bool known = entry.Family == ObjectType.Unit ? catalog.Balance.Units.ContainsKey(entry.Rule) :
                    entry.Family == ObjectType.Placeable_CompositeStructure ? catalog.Balance.Buildings.ContainsKey(entry.Rule) :
                    catalog.Balance.Worksites.ContainsKey(entry.Rule);
                if (!known) throw new InvalidOperationException("Cannot resolve the current authored rule: " + entry.Definition.Key);
            }
            var actor = Archetype("Actor", typeof(IActorCapability), typeof(IMovementCapability));
            var building = Archetype("Building", typeof(IBuildingCapability));
            var worksite = Archetype("Worksite", typeof(IWorksiteCapability));
            foreach (var entry in input)
            {
                ObjectDefinition definition = entry.Definition;
                IConfigData config = entry.Family == ObjectType.Unit ? (IConfigData)new ActorRuleConfig { RuleKey = entry.Rule } :
                    entry.Family == ObjectType.Placeable_CompositeStructure ? new BuildingRuleConfig { RuleKey = entry.Rule } :
                    new WorksiteRuleConfig { RuleKey = entry.Rule };
                if (!definition.SharedConfigs.Any(c => c != null && c.GetType() == config.GetType())) definition.SharedConfigs.Add(config);
                if (entry.Rule == "worker")
                {
                    Add<ActorBehaviour>(definition);
                    Add<MovementBehaviour>(definition);
                    if (!definition.SharedConfigs.Any(c => c is MovementConfig)) definition.SharedConfigs.Add(new MovementConfig());
                    definition.Archetype = actor;
                }
                else if (entry.Rule == "house" || entry.Rule == "tavern")
                {
                    Add<BuildingBehaviour>(definition);
                    definition.Archetype = building;
                }
                else if (entry.Rule == "wood")
                {
                    Add<WorksiteBehaviour>(definition);
                    definition.Archetype = worksite;
                }
                EditorUtility.SetDirty(definition);
            }
            ObjectDefinition session = database.GetDefinitionByKey(FormalObjectCatalog.SessionKey);
            Add<CampSimulationBehaviour>(session);
            Add<EconomyBehaviour>(session);
            EditorUtility.SetDirty(session);
            AssetDatabase.SaveAssets();
            UpgradePlacements();
            new DefinitionRuleIndex(database).Validate(catalog);
            Directory.CreateDirectory("../artifacts/yygc-unified/u2");
            File.WriteAllText("../artifacts/yygc-unified/u2/content-upgrade.txt",
                "success=true\nrule_configs=" + definitions.Length + "\nslice_definitions=4\n");
        }

        private static ObjectArchetype Archetype(string family, params Type[] capabilities)
        {
            const string folder = "Assets/DarkNights/Res/Shared/Archetypes";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/DarkNights/Res/Shared", "Archetypes");
            string path = folder + "/" + family + ".asset";
            var value = AssetDatabase.LoadAssetAtPath<ObjectArchetype>(path);
            if (value != null) return value;
            value = ScriptableObject.CreateInstance<ObjectArchetype>();
            value.ArchetypeName = family;
            value.RequiredCapabilityInterfaces.AddRange(capabilities.Select(t => t.AssemblyQualifiedName));
            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        private static void Add<T>(ObjectDefinition definition) where T : IBehaviour
        {
            string name = typeof(T).FullName;
            if (!definition.BehaviourTypes.Contains(name)) definition.BehaviourTypes.Add(name);
        }

        private static void UpgradePlacements()
        {
            const string path = "Assets/DarkNights/Res/Scenes/Pinewatch/Pinewatch.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save the authored scene before adding placement keys.");
                LevelPlacementMarker[] markers = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LevelPlacementMarker>(true)).ToArray();
                foreach (LevelPlacementMarker marker in markers)
                {
                    marker.EditorEnsurePlacementKey();
                    EditorUtility.SetDirty(marker);
                }
                foreach (LevelPlacementMarker marker in markers) SceneDefinitionAuthoring.ValidatePlacement(marker);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save placement keys.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
