using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using GameCore.Objects.Singletons;
using GameCore.Objects.Types;
using GameCore.UI.UGUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Editor
{
    /// <summary>
    /// 在隔离宿主使用旧框架创建一次性冻结夹具；拒绝覆盖已有基线。
    /// 只写专用目录，记录公开签名和旧资产，供后续升级验证读取而非重新生成。
    /// </summary>
    public static class DefinitionIdentityBaseline
    {
        public const string FixtureRoot = "Assets/IdCompatibilityFixtures";

        public static void Capture()
        {
            if (Directory.Exists(FixtureRoot)) throw new InvalidOperationException("基线已经存在，禁止覆盖。");
            AssetDatabase.CreateFolder("Assets", "IdCompatibilityFixtures");
            var normal = Create("legacy_worker", 1001, ObjectType.Resource_Raw);
            Create("legacy_singleton", 10001, ObjectType.SingletonUtility);
            Create("legacy_ui", 20001, ObjectType.UI_Panel);
            var root = new GameObject("LegacyRoot");
            var instance = root.AddComponent<ObjectInstance>();
            var view = root.AddComponent<ObjectView>();
            var initializer = root.AddComponent<LocalObjectInstanceInitializer>();
            SetReference(instance, "_view", view);
            SetReference(view, "_ownerInstance", instance);
            SetReference(view, "_initializer", initializer);
            SetReference(initializer, "_objectInstance", instance);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, FixtureRoot + "/legacy.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            normal.PrefabRef = new UnityEngine.AddressableAssets.AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)));
            EditorUtility.SetDirty(normal);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var loader = placed.AddComponent<ObjectDefinitionLoader>();
            var serialized = new SerializedObject(loader);
            serialized.FindProperty("_definitionId").intValue = 1001;
            serialized.FindProperty("_initializerComponent").objectReferenceValue = placed.GetComponent<LocalObjectInstanceInitializer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, FixtureRoot + "/legacy.unity");
            var singletons = ScriptableObject.CreateInstance<ObjectSingletonDatabase>();
            singletons.SingletonRecords.Add(new SingletonRecord { BehaviourTypeName = "Frozen.Legacy.Type, Legacy", DefinitionId = 10001, IsEnabled = false });
            AssetDatabase.CreateAsset(singletons, FixtureRoot + "/legacy_singletons.asset");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("../artifacts/identity-baseline");
            File.WriteAllLines("../artifacts/identity-baseline/public-api.txt", PublicApi());
            File.Copy(typeof(ObjectDefinition).Assembly.Location, "../artifacts/identity-baseline/GameCore.Runtime.dll", false);
            Debug.Log("IDENTITY_BASELINE_CAPTURED");
        }

        public static string[] PublicApi()
        {
            var output = new List<string>();
            Type[] types = { typeof(ObjectDefinition), typeof(ObjectDefinitionDatabase), typeof(ObjectInstance),
                typeof(IObjectInstanceInitializer), typeof(ObjectInstanceFactory), typeof(StateSynchronizer),
                typeof(SingletonRecord), typeof(UGUIManager), typeof(DefinitionIDAttribute) };
            foreach (Type type in types)
            {
                foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    string entry = type.FullName + "|" + member.MemberType + "|" + member;
                    if (member is MethodBase method)
                    {
                        entry += "|" + string.Join(",", method.GetParameters().Select(p => p.Name + "=" + (p.HasDefaultValue ? Convert.ToString(p.DefaultValue, System.Globalization.CultureInfo.InvariantCulture) : "required")));
                    }
                    output.Add(entry);
                }
            }
            output.Sort(StringComparer.Ordinal);
            return output.ToArray();
        }

        private static ObjectDefinition Create(string name, int id, ObjectType type)
        {
            var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.name = name;
            definition.Name = name;
            definition.Id = id;
            definition.Type = type;
            definition.NetType = NetworkType.Local;
            AssetDatabase.CreateAsset(definition, FixtureRoot + "/" + name + ".asset");
            return definition;
        }

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
