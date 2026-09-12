using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Runtime.Framework;
using DarkNights.View;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor
{
    /// <summary>
    /// 在既有布局场景中显式安装首版原生背景、摄像机和标记外观，保留原布局及所有实体顺序。
    /// 仅允许尚未装配表现的场景；背景数值逐项沿用冻结 Pinewatch.tscn，之后由人工维护。
    /// </summary>
    public static class PinewatchVisualSetup
    {
        [MenuItem("Dark Nights/Content/Install Initial Pinewatch Visuals")]
        public static void Install()
        {
            Scene scene = SceneManager.GetSceneByPath(PinewatchLayoutSetup.ScenePath);
            bool wasOpen = scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(PinewatchLayoutSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save the authored scene before installing visuals.");
                GameObject root = scene.GetRootGameObjects().Single();
                if (root.GetComponent<PinewatchStage>() != null)
                    throw new InvalidOperationException("Native Pinewatch visuals already exist; installation never overwrites them.");
                var layout = root.GetComponentInChildren<LevelLayoutAuthoring>();
                layout.transform.localScale = Vector3.one * 0.01f;
                layout.transform.localPosition = new Vector3(0, -3.2f, 0);
                AddPreviews(layout);
                var stage = root.AddComponent<PinewatchStage>();
                Camera camera = Child(root.transform, "Camera").gameObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 800f / 2 / 2.8f / 100;
                camera.transform.localPosition = new Vector3(2.55f, 800f * 0.215f / 2.8f / 100, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(170, 188, 193, 255);
                camera.depth = 10;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 50;
                Transform entities = Child(root.transform, "EntityViews");
                Transform background = Child(root.transform, "Backgrounds");
                SpriteRenderer sky = Sprite(background, "Sky", "bg_sky_clear", new Vector2(-5, 6), -100);
                sky.transform.localScale = new Vector3(8.203125f, 1, 1);
                sky.color = new Color32(170, 188, 193, 255);
                var layers = new List<NativeBackdrop>
                {
                    Layer(background, "Stars", "bg_sky_stars", 5.4f, 0.03f, new Color(1, 1, 1, 0.85f), -99, true),
                    Layer(background, "DistantForest", "bg_Lforest_middle", 1.85f, 0.13f, new Color32(160, 173, 160, 255), -98),
                    Layer(background, "MiddleForest", "bg_forest_middle", 1.10f, 0.3f, new Color32(115, 138, 128, 255), -97),
                    Layer(background, "NearForest", "bg_forest_new_close", 0.77f, 0.5f, new Color32(120, 136, 121, 255), -96),
                    Layer(background, "FrontForest", "bg_forest_close", 0.54f, 0.7f, new Color32(95, 117, 101, 255), -95)
                };
                Ground(background);
                SpriteRenderer tomb = Sprite(background, "Tomb", "spr_ubld_tomb", new Vector2(9.74f, 1), -90);
                tomb.color = new Color32(185, 185, 177, 255);
                NativePrefabBuilder.SetReference(stage, "sceneCamera", camera);
                NativePrefabBuilder.SetReference(stage, "entities", entities);
                NativePrefabBuilder.SetReference(stage, "sky", sky);
                var data = new SerializedObject(stage);
                SerializedProperty targets = data.FindProperty("backgrounds");
                targets.arraySize = layers.Count;
                for (int i = 0; i < layers.Count; i++) targets.GetArrayElementAtIndex(i).objectReferenceValue = layers[i];
                data.ApplyModifiedPropertiesWithoutUndo();
                foreach (NativeBackdrop layer in layers) layer.Apply(255, 0.16f, stage.Ambient);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save Pinewatch visuals.");
                PinewatchLayoutSetup.Validate();
                Debug.Log("DARK_NIGHTS_PINEWATCH_VISUALS_INSTALLED previews=16 backgroundLayers=5");
            }
            finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void AddPreviews(LevelLayoutAuthoring layout)
        {
            foreach (LevelPlacementMarker marker in layout.GetComponentsInChildren<LevelPlacementMarker>())
            {
                SceneDefinitionAuthoring.ValidatePlacement(marker);
                marker.View.Get<NativeVisual>("visual").Preview(marker.SpawnOrder, marker.Variant);
            }
        }

        private static NativeBackdrop Layer(Transform parent, string name, string texture, float y,
            float factor, Color tint, int order, bool fade = false)
        {
            Transform root = Child(parent, name);
            Sprite source = NativeAnimationBuilder.Sprite("res://assets/sprites/" + texture + "/" + texture + "_0.png");
            var sprites = new List<SpriteRenderer>();
            for (int i = -3; i <= 3; i++)
                sprites.Add(Sprite(root, "Repeat" + i, texture, new Vector2(i * source.rect.width / 100, y), order));
            var layer = root.gameObject.AddComponent<NativeBackdrop>();
            var data = new SerializedObject(layer);
            data.FindProperty("factor").floatValue = factor;
            data.FindProperty("tint").colorValue = tint;
            data.FindProperty("fadeWithNight").boolValue = fade;
            SerializedProperty targets = data.FindProperty("sprites");
            targets.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++) targets.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            return layer;
        }

        private static void Ground(Transform parent)
        {
            Transform ground = Child(parent, "Ground");
            var material = new Material(Shader.Find("Unlit/Color")) { color = new Color32(48, 46, 40, 255) };
            AssetDatabase.CreateAsset(material, PinewatchLayoutSetup.Root + "/Soil.mat");
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-1, 0.01f), new Vector3(12, 0.01f), new Vector3(12, -5.99f), new Vector3(-1, -5.99f) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, PinewatchLayoutSetup.Root + "/Soil.asset");
            ground.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer soil = ground.gameObject.AddComponent<MeshRenderer>();
            soil.sharedMaterial = material;
            soil.sortingOrder = -94;
            Sprite full = NativeAnimationBuilder.Sprite("res://assets/sprites/spr_block_grassy/spr_block_grassy_0.png");
            Sprite grass = UnityEngine.Sprite.Create(full.texture, new Rect(0, full.texture.height - 7, 256, 7), new Vector2(0, 1), 100, 0, SpriteMeshType.FullRect);
            AssetDatabase.CreateAsset(grass, PinewatchLayoutSetup.Root + "/Grass.asset");
            for (int i = -1; i <= 5; i++)
            {
                var renderer = Child(ground, "Grass" + (i + 1)).gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = grass;
                renderer.transform.localPosition = new Vector3(i * 2.56f, 0.01f, 0);
                renderer.color = new Color32(188, 194, 160, 255);
                renderer.sortingOrder = -93;
            }
        }

        private static SpriteRenderer Sprite(Transform parent, string name, string texture, Vector2 position, int order)
        {
            var renderer = Child(parent, name).gameObject.AddComponent<SpriteRenderer>();
            renderer.transform.localPosition = position;
            renderer.sprite = NativeAnimationBuilder.Sprite("res://assets/sprites/" + texture + "/" + texture + "_0.png");
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
