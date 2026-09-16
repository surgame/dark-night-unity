using System;
using System.Linq;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Behaviours.Interfaces;
using GameCore.Objects.Definition;
using GameCore.Objects.Types;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 为首版空目录安装流程装配正式业务能力、家族配置和 Archetype，不提供旧资产升级入口。
    /// 已有 Archetype 只校验而不改写；正式人工资源的日常导入和构建不会调用配置方法。
    /// </summary>
    public static class ObjectCapabilitySetup
    {
        public static void ConfigureLocal(ObjectDefinition definition, string ruleKey)
        {
            if (definition.Type == ObjectType.Unit)
            {
                definition.SharedConfigs.Add(new ActorRuleConfig { RuleKey = ruleKey });
                definition.SharedConfigs.Add(new MovementConfig());
                Add<ActorBehaviour>(definition);
                Add<MovementBehaviour>(definition);
                Add<ActorCombatBehaviour>(definition);
                Add<AutomaticActorControlBehaviour>(definition);
                if (ruleKey == "worker" || ruleKey == "spearman" || ruleKey == "archer")
                {
                    Add<HeroControlBehaviour>(definition);
                    Add<HeroMotionBehaviour>(definition);
                    Add<HeroInventoryBehaviour>(definition);
                }
                if (ruleKey == "archer") Add<ArrowAttackBehaviour>(definition);
                else Add<MeleeAttackBehaviour>(definition);
                definition.Archetype = Archetype("Actor", typeof(IActorCapability), typeof(IMovementCapability),
                    typeof(IActorCombatCapability), typeof(IAttackCapability), typeof(IAutomaticActorControl));
            }
            else if (definition.Type == ObjectType.Placeable_CompositeStructure)
            {
                definition.SharedConfigs.Add(new BuildingRuleConfig { RuleKey = ruleKey });
                Add<BuildingBehaviour>(definition);
                if (ruleKey == "barracks") Add<TrainingBehaviour>(definition);
                if (ruleKey == "tower") Add<TowerAttackBehaviour>(definition);
                definition.Archetype = Archetype("Building", typeof(IBuildingCapability));
            }
            else if (definition.Type == ObjectType.Scenery_ResourceNode)
            {
                definition.SharedConfigs.Add(new WorksiteRuleConfig { RuleKey = ruleKey });
                Add<WorksiteBehaviour>(definition);
                definition.Archetype = Archetype("Worksite", typeof(IWorksiteCapability));
            }
            else throw new InvalidOperationException("Explicit entity family is required.");
        }

        public static void ConfigureSession(ObjectDefinition definition)
        {
            Add<CampSimulationBehaviour>(definition);
            Add<EconomyBehaviour>(definition);
            Add<WaveBehaviour>(definition);
            Add<ProjectileBehaviour>(definition);
        }

        private static ObjectArchetype Archetype(string family, params Type[] capabilities)
        {
            const string folder = "Assets/DarkNights/Res/Shared/Archetypes";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/DarkNights/Res/Shared", "Archetypes");
            string path = folder + "/" + family + ".asset";
            var value = AssetDatabase.LoadAssetAtPath<ObjectArchetype>(path);
            string[] required = capabilities.Select(t => t.AssemblyQualifiedName).ToArray();
            if (value != null)
            {
                if (!value.RequiredCapabilityInterfaces.SequenceEqual(required))
                    throw new InvalidOperationException("Existing authored Archetype differs: " + path);
                return value;
            }
            value = ScriptableObject.CreateInstance<ObjectArchetype>();
            value.ArchetypeName = family;
            value.RequiredCapabilityInterfaces.AddRange(required);
            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        private static void Add<T>(ObjectDefinition definition) where T : IBehaviour
        {
            string name = typeof(T).FullName;
            if (!definition.BehaviourTypes.Contains(name)) definition.BehaviourTypes.Add(name);
        }
    }
}
