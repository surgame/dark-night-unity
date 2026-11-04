using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkNights.View;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor
{
    /// <summary>
    /// 本轮主角原生资源迁移的有限入口；仅接受已选Hero序列帧绑定，保留Prefab／场景身份和所有作者布局。
    /// 首次迁移保存到现有资产，重复调用只跳过已迁移对象；有脏场景、Play或缺失绑定时拒绝继续。
    /// </summary>
    public static class HeroSpineAssetSetup
    {
        public const string Root = "Assets/DarkNights/Res/Objects/Hero/Spine/";
        public const string DataPath = Root + "HeroRuntime_SkeletonData.asset";
        private const string Rig = "Assets/DarkNights/Res/Terrain/MapModule/HeroRig.prefab";

        public static string Migrate()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("主角迁移须在空闲编辑态执行。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("未保存场景必须保留，拒绝开始迁移。");
            var data = PrepareAssets();
            var output = new List<string>();
            Backup(Rig);
            var prefab = PrefabUtility.LoadPrefabContents(Rig);
            try
            {
                if (Bind(prefab.GetComponent<ActorView>(), data))
                {
                    PrefabUtility.SaveAsPrefabAsset(prefab, Rig);
                    output.Add(Rig);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            string[] scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/DarkNights/Res/Scenes/Workbenches" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(value => value).ToArray();
            Scene active = SceneManager.GetActiveScene();
            foreach (string path in scenes)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    bool changed = false;
                    var actors = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<ActorView>(true)).ToArray();
                    if (actors.Any(value => value.Clips.Any(clip => clip.Name == "move" &&
                        AssetDatabase.GetAssetPath(clip.Clip).Contains("/Objects/Hero/AnimationClips/"))))
                        Backup(path);
                    foreach (var actor in actors)
                        changed |= Bind(actor, data);
                    if (changed)
                    {
                        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("主角场景保存失败：" + path);
                        output.Add(path);
                    }
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            AssetDatabase.SaveAssetIfDirty(data);
            return string.Join("\n", output);
        }

        private static void Backup(string path)
        {
            string target = Path.GetFullPath("../artifacts/hero-spine-runtime-20261103/before-binding/Game/" + path);
            if (File.Exists(target)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(path, target);
            if (File.Exists(path + ".meta")) File.Copy(path + ".meta", target + ".meta");
        }

        private static SkeletonDataAsset PrepareAssets()
        {
            string source = Root + "HeroRuntime.source.json";
            if (AssetDatabase.LoadMainAssetAtPath(source) != null)
            {
                string error = AssetDatabase.MoveAsset(source, source + ".txt");
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            var atlas = AssetDatabase.LoadAssetAtPath<SpineAtlasAsset>(Root + "HeroRuntime_Atlas.asset");
            var data = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(DataPath);
            var binary = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "HeroRuntime.skel.bytes");
            var shader = Shader.Find("Dark Nights/Camp Spine");
            if (atlas == null || data == null || binary == null || shader == null)
                throw new InvalidOperationException("原生骨骼／图集／Shader尚未导入。");
            foreach (Material material in atlas.materials)
            {
                material.shader = shader;
                material.SetColor("_Color", Color.white);
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(material.mainTexture)) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("图集贴图导入器缺失。");
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true; importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }
            data.skeletonJSON = binary;
            data.atlasAssets = new AtlasAssetBase[] { atlas };
            data.scale = 1f / 2083.3333333333335f;
            data.defaultMix = .1f;
            data.Clear();
            var skeleton = data.GetSkeletonData(false);
            if (skeleton == null || skeleton.FindAnimation("move") == null || skeleton.FindAnimation("jump_move") == null)
                throw new InvalidOperationException("骨骼数据缺少已选动作。");
            EditorUtility.SetDirty(data);
            return data;
        }

        private static bool Bind(ActorView actor, SkeletonDataAsset data)
        {
            if (actor == null) return false;
            if (actor.UsesSkeleton)
            {
                var inherited = new SerializedObject(actor);
                var oldClips = inherited.FindProperty("clips");
                var oldTargets = inherited.FindProperty("lightTargets");
                var boundDriver = (SpineActorAnimation)inherited.FindProperty("spineAnimation").objectReferenceValue;
                bool feetChanged = boundDriver.AlignFeet();
                if (feetChanged)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(boundDriver.Renderer.transform);
                    EditorUtility.SetDirty(boundDriver.Renderer.transform);
                }
                var boundRenderer = boundDriver.Renderer;
                bool meshSettingChanged = boundRenderer.fixPrefabOverrideViaMeshFilter != SettingsTriState.Enable;
                if (oldClips.arraySize == 0 && oldTargets.arraySize == 0 && !meshSettingChanged) return feetChanged;
                boundRenderer.fixPrefabOverrideViaMeshFilter = SettingsTriState.Enable;
                boundRenderer.EditorUpdateMeshFilterHideFlags();
                PrefabUtility.RecordPrefabInstancePropertyModifications(boundRenderer);
                EditorUtility.SetDirty(boundRenderer);
                for (int i = 0; i < oldTargets.arraySize; i++)
                    if (oldTargets.GetArrayElementAtIndex(i).objectReferenceValue is SpriteRenderer oldSprite)
                    {
                        oldSprite.enabled = false;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(oldSprite);
                    }
                oldClips.arraySize = oldTargets.arraySize = 0;
                inherited.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
                return true;
            }
            if (!actor.Clips.Any(value => value.Name == "move" &&
                AssetDatabase.GetAssetPath(value.Clip) == "Assets/DarkNights/Res/Objects/Hero/AnimationClips/Hero-move.anim"))
                return false;
            var properties = new SerializedObject(actor);
            var targets = properties.FindProperty("lightTargets");
            if (targets.arraySize != 1 || !(targets.GetArrayElementAtIndex(0).objectReferenceValue is SpriteRenderer previous))
                throw new InvalidOperationException("已选主角必须明确绑定唯一作者外观。");
            if (previous.sprite == null) throw new InvalidOperationException("迁移前的作者首帧缺失。");
            var root = new GameObject("Spine Hero");
            root.transform.SetParent(previous.transform.parent, false);
            root.transform.localPosition = previous.transform.localPosition;
            root.transform.localScale = previous.transform.localScale;
            var components = SkeletonAnimation.AddToGameObject(root, data);
            var renderer = components.skeletonRenderer;
            var animation = components.skeletonAnimation;
            renderer.pmaVertexColors = false;
            renderer.fixPrefabOverrideViaMeshFilter = SettingsTriState.Enable;
            renderer.clearStateOnDisable = false;
            renderer.UpdateWhenInvisible = UpdateMode.FullUpdate;
            animation.UpdateTiming = UpdateTiming.ManualUpdate;
            var mesh = root.GetComponent<MeshRenderer>();
            mesh.sortingLayerID = previous.sortingLayerID; mesh.sortingOrder = previous.sortingOrder;
            var driver = actor.gameObject.AddComponent<SpineActorAnimation>();
            var binding = new SerializedObject(driver);
            binding.FindProperty("skeletonAnimation").objectReferenceValue = animation;
            binding.FindProperty("skeletonRenderer").objectReferenceValue = renderer;
            binding.FindProperty("lightReceiver").objectReferenceValue = mesh;
            binding.ApplyModifiedPropertiesWithoutUndo();
            driver.Preview();
            driver.AlignFeet();
            previous.enabled = false;
            properties.FindProperty("spineAnimation").objectReferenceValue = driver;
            properties.FindProperty("clips").arraySize = 0;
            targets.arraySize = 0;
            var receivers = properties.FindProperty("skeletalLightTargets");
            receivers.arraySize = 1;
            receivers.GetArrayElementAtIndex(0).objectReferenceValue = mesh;
            properties.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
            PrefabUtility.RecordPrefabInstancePropertyModifications(previous);
            EditorUtility.SetDirty(actor);
            return true;
        }
    }
}
