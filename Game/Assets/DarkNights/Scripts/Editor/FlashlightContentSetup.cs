using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using GameCore.Editor.Objects.Definition;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.Objects.Types;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;

namespace DarkNights.Editor
{
    /// <summary>显式安装一次手电资产及角色照明挂点；保留既有 GUID、绑定身份、动画和参数，普通启动不执行迁移。</summary>
    public static class FlashlightContentSetup
    {
        public const string Root = "Assets/DarkNights/Res/Objects/Flashlight";
        public const string DefinitionPath = Root + "/Flashlight.asset";
        private const string PrefabPath = Root + "/Flashlight.prefab";

        [MenuItem("Dark Nights/Tools/安装手电照明切片")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("退出 Play 并等待编译完成后安装。");
            var database = AssetDatabase.LoadAssetAtPath<ObjectDefinitionDatabase>("Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset");
            if (database == null) throw new InvalidOperationException("缺少正式 Definition 数据库。");
            if (!File.Exists(DefinitionPath))
            {
                if (Directory.Exists(Root)) throw new InvalidOperationException("手电目录已有内容，拒绝覆盖。");
                Create(database);
            }
            foreach (var definition in database.Definitions.Where(value => value != null &&
                value.BehaviourTypes.Contains(typeof(HeroControlBehaviour).FullName)))
            {
                if (!definition.BehaviourTypes.Contains(typeof(HeroLightBehaviour).FullName))
                    definition.BehaviourTypes.Add(typeof(HeroLightBehaviour).FullName);
                EditorUtility.SetDirty(definition);
                string path = AssetDatabase.GUIDToAssetPath(definition.PrefabRef.AssetGUID);
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view = contents.GetComponent<ActorView>();
                    if (view == null) throw new InvalidOperationException("可控角色缺少 ActorView：" + path);
                    if (view.LightAnchor == null)
                    {
                        var anchor = new GameObject("Light Anchor").transform;
                        anchor.SetParent(view.transform,false); anchor.localPosition = new Vector3(0,.08f,0);
                        NativePrefabBuilder.SetReference(view,"lightAnchor",anchor);
                        PrefabUtility.SaveAsPrefabAsset(contents,path);
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(HeroInputAssetSetup.Path);
            if (input == null) throw new InvalidOperationException("缺少正式输入资产。");
            var player = input.FindActionMap("Player",true);
            if (player.FindAction("ToggleLight") == null)
            {
                player.AddAction("ToggleLight",InputActionType.Button,"<Keyboard>/f");
                File.WriteAllText(HeroInputAssetSetup.Path,input.ToJson());
                AssetDatabase.ImportAsset(HeroInputAssetSetup.Path);
            }
            EditorUtility.SetDirty(database); AssetDatabase.SaveAssets(); Validate();
            Debug.Log("DARK_NIGHTS_FLASHLIGHT_INSTALLED protocol=28 save=22");
        }

        public static void Validate()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(DefinitionPath);
            if (definition == null || !definition.isLocal || definition.Guid.IsEmpty ||
                definition.SharedConfigs.OfType<FlashlightToolConfig>().Count() != 1 ||
                !definition.BehaviourTypes.Contains(typeof(FlashlightToolBehaviour).FullName))
                throw new InvalidOperationException("手电 Definition 合同不完整。");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var view = prefab != null ? prefab.GetComponent<FlashlightView>() : null;
            if (view == null || view.Owner == null || view.LightingShader == null ||
                definition.PrefabRef.AssetGUID != AssetDatabase.AssetPathToGUID(PrefabPath))
                throw new InvalidOperationException("手电 PrefabRef／视图／计算资源缺失。");
            NativeObjectContracts.RequireGenerated(typeof(FlashlightToolBehaviour));
            NativeObjectContracts.RequireGenerated(typeof(HeroLightBehaviour));
            definition.SharedConfigs.OfType<FlashlightToolConfig>().Single().Freeze();
        }

        private static void Create(ObjectDefinitionDatabase database)
        {
            Directory.CreateDirectory(Root);
            var texture = new Texture2D(6,3,TextureFormat.RGBA32,false,true);
            var pixels = new Color32[18];
            for (int y=0;y<3;y++) for (int x=0;x<6;x++)
                pixels[y*6+x] = x>=4 ? new Color32(234,218,163,255) :
                    y==1 ? new Color32(110,119,119,255) : new Color32(49,57,59,255);
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(Root+"/Flashlight.png",texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(Root+"/Flashlight.png",ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root+"/Flashlight.png");
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.sRGBTexture = true;
            importer.spritePivot = new Vector2(0,.5f); importer.SaveAndReimport();
            var root = new GameObject("Flashlight");
            try
            {
                var instance = root.AddComponent<ObjectInstance>();
                var initializer = root.AddComponent<LocalObjectInstanceInitializer>();
                var view = root.AddComponent<FlashlightView>();
                var body = root.AddComponent<SpriteRenderer>(); body.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Flashlight.png");
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/DarkNights/Res/Shared/Lighting/CampSprite.shader");
                if (shader == null) throw new InvalidOperationException("缺少正式受光 Shader。");
                var material = new Material(shader) { name = "Flashlight" };
                AssetDatabase.CreateAsset(material,Root+"/Flashlight.mat"); body.sharedMaterial = material;
                body.sortingOrder = 105;
                var emitter = new GameObject("Emitter").transform; emitter.SetParent(root.transform,false);
                emitter.localPosition = new Vector3(.05f,0,0);
                NativePrefabBuilder.SetReference(instance,"_view",view);
                NativePrefabBuilder.SetReference(initializer,"_objectInstance",instance);
                NativePrefabBuilder.SetReference(view,"body",body); NativePrefabBuilder.SetReference(view,"emitter",emitter);
                NativePrefabBuilder.SetReference(view,"lightingShader",AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/DarkNights/Res/Shared/Lighting/ExplorationLighting.compute"));
                view.ForceRefreshAllReferences(); PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.Name = "手电筒"; definition.Type = ObjectType.Tool; definition.NetType = NetworkType.Local;
            definition.PrefabRef = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(PrefabPath));
            definition.SharedConfigs.Add(new FlashlightToolConfig()); definition.BehaviourTypes.Add(typeof(FlashlightToolBehaviour).FullName);
            AssetDatabase.CreateAsset(definition,DefinitionPath);
            definition.EditorSetIdentity(DefinitionIdentityAuthoring.ReadAssetGuid(definition),"item.flashlight",false);
            database.AddDefinition(definition);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.CreateOrMoveEntry(definition.PrefabRef.AssetGUID,settings.DefaultGroup).address = "dark_nights.item.flashlight";
            EditorUtility.SetDirty(definition);
        }
    }
}
