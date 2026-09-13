using System;
using System.IO;
using System.Linq;
using DarkNights.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor
{
    /// <summary>
    /// 在指定空目录输出可编辑环境 Prefab，并对既有场景和十五类外观作一次明确材质绑定。
    /// 预检场景无未保存改动、灯光输出为空；不改变布局标记、既有 GUID 或原始纹理字节。
    /// </summary>
    public static class NativeEnvironmentSetup
    {
        public const string Root = "Assets/DarkNights/Res/Levels/Pinewatch/Environment";
        [MenuItem("Dark Nights/Content/Install Initial Native Environment")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before installing environment.");
            if (Directory.Exists(Root) && Directory.GetFileSystemEntries(Root).Length != 0)
                throw new InvalidOperationException("Initial environment output must be empty.");
            var scene = SceneManager.GetSceneByPath(PinewatchLayoutSetup.ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(PinewatchLayoutSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save authored scene changes first.");
                var root = scene.GetRootGameObjects().Single();
                if (root.GetComponentInChildren<NativeEnvironment>() != null) throw new InvalidOperationException("Native environment already installed.");
                Folder(Root);
                var shader = Shader.Find("Dark Nights/Camp Sprite");
                if (shader == null) throw new InvalidOperationException("Camp sprite shader is not imported.");
                var material = new Material(shader) { name = "Camp Sprite" };
                AssetDatabase.CreateAsset(material, Root + "/CampSprite.mat");
                var meshMaterial = new Material(material) { name = "Camp Static Mesh" };
                meshMaterial.SetFloat("_UseGlobalAmbient", 1);
                AssetDatabase.CreateAsset(meshMaterial, Root + "/CampMesh.mat");
                var vertexMaterial = new Material(meshMaterial) { name = "CampVertexMesh" };
                vertexMaterial.SetFloat("_VertexColorIsGamma", 1);
                AssetDatabase.CreateAsset(vertexMaterial, Root + "/CampVertexMesh.mat");
                Sprite white = EnvironmentGeometry.White(Root), circle = EnvironmentGeometry.Circle(Root);
                GameObject torch = Torch(white, material);
                GameObject prefab = Environment(torch, white, circle, material, vertexMaterial);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                var environment = instance.GetComponent<NativeEnvironment>();
                var stage = root.GetComponent<PinewatchStage>();
                NativePrefabBuilder.SetReference(stage, "environment", environment);
                BindMaterials(root, material, meshMaterial);
                var data = new SerializedObject(environment);
                var statics = root.GetComponentsInChildren<SpriteRenderer>(true).Where(s => s.gameObject.name == "CampEdge" || (
                    !s.transform.IsChildOf(instance.transform) && s.GetComponentInParent<NativeVisual>() == null &&
                    s.GetComponentInParent<NativeBackdrop>() == null && s.gameObject.name != "Sky")).ToArray();
                EnvironmentGeometry.Array(data, "staticSprites", statics);
                var colors = data.FindProperty("staticColors"); colors.arraySize = statics.Length;
                for (int i = 0; i < statics.Length; i++) colors.GetArrayElementAtIndex(i).colorValue = statics[i].color;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(environment);
                environment.Present(0, 0.16f, 255, stage.Ambient);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save native environment scene.");
                AssetDatabase.SaveAssets();
                PinewatchLayoutSetup.Validate();
                Debug.Log("DARK_NIGHTS_ENVIRONMENT_INSTALLED torches=4 lights=7 fireflies=15 terrainDetails=190 actorShadows=6");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static GameObject Torch(Sprite white, Material material)
        {
            var root = new GameObject("Torch");
            try
            {
                var torch = root.AddComponent<CampTorch>();
                var pole = EnvironmentGeometry.Sprite(root.transform, "Pole", white, new Vector2(-0.01f, 0.17f), new Vector2(2, 17), -85, material);
                var flame = EnvironmentGeometry.Sprite(root.transform, "Flame", null, new Vector2(-0.05f, 0.23f), Vector2.one, -84, material);
                var data = new SerializedObject(torch);
                data.FindProperty("pole").objectReferenceValue = pole;
                data.FindProperty("flame").objectReferenceValue = flame;
                EnvironmentGeometry.Array(data, "frames", Enumerable.Range(0, 7).Select(i => NativeAnimationBuilder.Sprite(
                    "res://assets/sprites/spr_dec_torch/spr_dec_torch_" + i + ".png")).ToArray());
                data.ApplyModifiedPropertiesWithoutUndo(); torch.Present(0, Color.white);
                return PrefabUtility.SaveAsPrefabAsset(root, Root + "/Torch.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject Environment(GameObject torch, Sprite white, Sprite circle, Material material, Material meshMaterial)
        {
            var root = new GameObject("Environment");
            try
            {
                var view = root.AddComponent<NativeEnvironment>();
                Transform moon = EnvironmentGeometry.Child(root.transform, "Moon"); moon.localPosition = new Vector3(3.93f, 1.55f);
                moon.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = -99;
                var disc = EnvironmentGeometry.Sprite(moon, "Disc", circle, Vector2.zero, Vector2.one * (22f / 64), 0, material);
                var cutout = EnvironmentGeometry.Sprite(moon, "Cutout", circle, new Vector2(.04f, .03f), Vector2.one * (20f / 64), 1, material);
                var torches = new CampTorch[4]; var lights = new CampLight[7];
                float[] xs = { 186, 365, 630, 788 }, offsets = { -31, 0, 0, 32 };
                for (int i = 0; i < 4; i++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(torch, root.transform);
                    instance.name = "Torch" + (i + 1); instance.transform.localPosition = new Vector3(xs[i] / 100, 0);
                    torches[i] = instance.GetComponent<CampTorch>();
                    var light = EnvironmentGeometry.Child(instance.transform, "Light");
                    light.localPosition = new Vector3(offsets[i] / 100, .2f); lights[i] = light.gameObject.AddComponent<CampLight>();
                }
                float[] extra = { 94, 302, 745 };
                for (int i = 0; i < 3; i++)
                {
                    var light = EnvironmentGeometry.Child(root.transform, "CampLight" + (i + 1));
                    light.localPosition = new Vector3(extra[i] / 100, .2f); lights[i + 4] = light.gameObject.AddComponent<CampLight>();
                }
                var flies = new SpriteRenderer[15];
                Transform fireflies = EnvironmentGeometry.Child(root.transform, "Fireflies");
                for (int i = 0; i < flies.Length; i++) flies[i] = EnvironmentGeometry.Sprite(fireflies, "Glow" + i, white, Vector2.zero, Vector2.one * .8f, -80, material);
                var terrain = EnvironmentGeometry.Child(root.transform, "TerrainDetails");
                terrain.gameObject.AddComponent<MeshFilter>().sharedMesh = EnvironmentGeometry.Terrain(Root);
                var terrainRenderer = terrain.gameObject.AddComponent<MeshRenderer>(); terrainRenderer.sharedMaterial = meshMaterial; terrainRenderer.sortingOrder = -92;
                var edge = EnvironmentGeometry.Sprite(root.transform, "CampEdge", white, new Vector2(.26f, -.075f), new Vector2(828, 1), -91, material);
                edge.color = new Color(.57f, .5f, .34f, .12f);
                var data = new SerializedObject(view);
                data.FindProperty("moon").objectReferenceValue = moon;
                data.FindProperty("moonDisc").objectReferenceValue = disc;
                data.FindProperty("moonCutout").objectReferenceValue = cutout;
                EnvironmentGeometry.Array(data, "torches", torches); EnvironmentGeometry.Array(data, "lights", lights);
                EnvironmentGeometry.Array(data, "fireflies", flies);
                EnvironmentGeometry.Array(data, "staticSprites", new UnityEngine.Object[] { edge });
                var colors = data.FindProperty("staticColors"); colors.arraySize = 1; colors.GetArrayElementAtIndex(0).colorValue = edge.color;
                data.ApplyModifiedPropertiesWithoutUndo(); view.Present(0, .16f, 255, Color.white);
                return PrefabUtility.SaveAsPrefabAsset(root, Root + "/Environment.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BindMaterials(GameObject scene, Material spriteMaterial, Material meshMaterial)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DarkNights/Res/Objects" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var visual = root.GetComponent<NativeVisual>(); if (visual == null) continue;
                    foreach (var sprite in root.GetComponentsInChildren<SpriteRenderer>(true)) sprite.sharedMaterial = spriteMaterial;
                    if (visual.Clips.Any(c => c.Name == "idle"))
                    {
                        var circle = AssetDatabase.LoadAllAssetsAtPath(Root + "/Circle.asset").OfType<Sprite>().Single();
                        var shadow = EnvironmentGeometry.Sprite(root.transform, "Shadow", circle, new Vector2(0, .005f), new Vector2(10f / 64, 3f / 64), -1, meshMaterial);
                        shadow.color = new Color(.06f, .08f, .08f, .5f);
                        NativePrefabBuilder.SetReference(visual, "shadow", shadow);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var sprite in scene.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sprite.sharedMaterial = sprite.gameObject.name == "Shadow" ? meshMaterial : spriteMaterial;
                PrefabUtility.RecordPrefabInstancePropertyModifications(sprite);
            }
            foreach (var mesh in scene.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mesh.sharedMaterial == meshMaterial) continue;
                Material original = mesh.sharedMaterial;
                var replacement = new Material(meshMaterial) { name = original.name, color = original.color };
                replacement.SetFloat("_VertexColorIsGamma", 1);
                string path = Root + "/" + original.name + ".mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing == null) { AssetDatabase.CreateAsset(replacement, path); existing = replacement; }
                else UnityEngine.Object.DestroyImmediate(replacement);
                mesh.sharedMaterial = existing; PrefabUtility.RecordPrefabInstancePropertyModifications(mesh);
            }
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
