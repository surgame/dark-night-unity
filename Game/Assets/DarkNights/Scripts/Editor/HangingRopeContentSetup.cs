using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Physics;
using DarkNights.View;
using GameCore.Editor.Objects.Definition;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.Objects.Types;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkNights.Editor
{
    /// <summary>
    /// 吊绳共享定义的显式首次安装入口，沿原视觉源建立ObjectInstance、主视图及共享配置。
    /// 只写指定空目录，已有定义只核验，普通导入和运行不执行安装或覆盖作者资源。
    /// </summary>
    public static class HangingRopeContentSetup
    {
        public const string Root = "Assets/DarkNights/Res/Objects/HangingRope";
        public const string DefinitionPath = Root + "/HangingRope.asset";
        public const string PrefabPath = Root + "/HangingRope.prefab";
        public static ObjectDefinition Ensure(PhysicsProfile profile)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(DefinitionPath);
            if (existing != null) { HangingRopeConfig.Required(existing); return existing; }
            if (Directory.Exists(Root) && Directory.EnumerateFileSystemEntries(Root).Any())
                throw new InvalidOperationException("吊绳共享目录已有内容，请先核对安装状态。");
            NativeObjectContracts.RequireGenerated(typeof(HangingRopeBehaviour));
            var database = ObjectDefinitionDatabase.Instance;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (database == null || settings == null) throw new InvalidOperationException("缺少定义目录或Addressables配置。");
            if (!AssetDatabase.IsValidFolder(Root))
                AssetDatabase.CreateFolder("Assets/DarkNights/Res/Objects", "HangingRope");
            if (profile == null || profile.RopeViewPrefab == null || profile.PropViewPrefab == null)
                throw new InvalidOperationException("缺少显式的吊绳及固定点视觉来源。");
            var config = new HangingRopeConfig();
            var root = new GameObject("HangingRope");
            try
            {
                var line = UnityEngine.Object.Instantiate(profile.RopeViewPrefab, root.transform);
                line.transform.localPosition = Vector3.zero;
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.SetPosition(0, Vector3.zero);
                line.SetPosition(1, Vector3.down * (config.SegmentCount * config.SegmentLength));
                line.startWidth = line.endWidth = config.LineWidth;
                line.numCornerVertices = config.CornerVertices;
                line.numCapVertices = config.CapVertices;
                var anchorRoot = new GameObject("固定顶端");
                anchorRoot.transform.SetParent(root.transform, false);
                var anchor = anchorRoot.AddComponent<SpriteRenderer>();
                anchor.sprite = profile.PropViewPrefab.sprite;
                anchor.sharedMaterial = profile.PropViewPrefab.sharedMaterial;
                anchor.sortingOrder = line.sortingOrder;
                anchor.transform.localScale = new Vector3(.16f / anchor.sprite.bounds.size.x,
                    .16f / anchor.sprite.bounds.size.y, 1);
                var instance = root.AddComponent<ObjectInstance>();
                var initializer = root.AddComponent<LocalObjectInstanceInitializer>();
                var view = root.AddComponent<HangingRopeView>();
                view.Line = line; view.Anchor = anchor;
                NativePrefabBuilder.SetReference(instance, "_view", view);
                NativePrefabBuilder.SetReference(initializer, "_objectInstance", instance);
                view.EditorSetBindings(Array.Empty<GameCore.Objects.Views.ViewComponentBinding>(), false);
                view.ForceRefreshAllReferences();
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("吊绳物体Prefab保存失败。");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.Name = "可攀爬吊绳";
            definition.Type = ObjectType.Scenery_Interactive;
            definition.NetType = NetworkType.Local;
            definition.PrefabRef = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(PrefabPath));
            definition.SharedConfigs.Add(config);
            definition.BehaviourTypes.Add(typeof(HangingRopeBehaviour).FullName);
            AssetDatabase.CreateAsset(definition, DefinitionPath);
            definition.EditorSetIdentity(DefinitionIdentityAuthoring.ReadAssetGuid(definition), "environment.hanging-rope", false);
            database.AddDefinition(definition);
            var entry = settings.CreateOrMoveEntry(definition.PrefabRef.AssetGUID, settings.DefaultGroup);
            entry.address = "dark_nights.object.hanging_rope";
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssetIfDirty(definition);
            AssetDatabase.SaveAssetIfDirty(database);
            AssetDatabase.SaveAssetIfDirty(entry.parentGroup);
            AssetDatabase.SaveAssetIfDirty(settings);
            return definition;
        }
    }
}
