using System;
using System.IO;
using System.Linq;
using DarkNights.Core.ViewData;
using DarkNights.Editor;
using DarkNights.View;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 显式校准既有世界表现的有限资产批次，复用 GUID、源像素和当前人工布局。
/// 仅在隔离 Editor 中执行一次；保留 Linear 色彩空间，新增输出必须为空，普通导入和构建不调用。
/// </summary>
public static class WorldPresentationCalibration
{
    private const string EnvironmentRoot = NativeEnvironmentSetup.Root;
    private static readonly string[] Objects =
    {
        "Worker", "Spearman", "Archer", "Zombie", "Ghoul", "Armored", "House", "Tavern",
        "Barracks", "Farm", "Tower", "Trees", "Stone", "Iron", "Farmland"
    };

    public static object Main(string output)
    {
        if (Application.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Stop Play and wait for compilation.");
        if (PlayerSettings.colorSpace != ColorSpace.Linear) throw new InvalidOperationException("Linear color space is required.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve unsaved scene changes.");
        foreach (string path in new[] { EnvironmentRoot + "/CampVertexMesh.mat",
            NativeEffectsSetup.Root + "/Floating/FloatingFont.fontsettings", NativeEffectsSetup.Root + "/Command/CommandRing.asset" })
            if (File.Exists(path)) throw new InvalidOperationException("Calibration output already exists: " + path);
        var changed = new JArray();
        var report = new JObject { ["passed"] = false, ["colorSpace"] = "Linear", ["changed"] = changed };
        try
        {
            var source = Required<Material>(EnvironmentRoot + "/CampMesh.mat");
            var vertices = new Material(source) { name = "CampVertexMesh" };
            vertices.SetFloat("_VertexColorIsGamma", 1);
            AssetDatabase.CreateAsset(vertices, EnvironmentRoot + "/CampVertexMesh.mat");
            changed.Add(AssetDatabase.GetAssetPath(vertices));
            foreach (string name in new[] { "Soil", "Depleted" })
            {
                var material = Required<Material>(EnvironmentRoot + "/" + name + ".mat");
                material.SetFloat("_VertexColorIsGamma", 1); EditorUtility.SetDirty(material);
                changed.Add(AssetDatabase.GetAssetPath(material));
            }
            for (int i = 0; i < Objects.Length; i++)
            {
                string path = "Assets/DarkNights/Res/Objects/" + Objects[i] + "/" + Objects[i] + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var visual = root.GetComponent<NativeVisual>();
                    var sorting = root.GetComponent<SortingGroup>();
                    if (visual == null || sorting == null) throw new InvalidOperationException("Missing authored visual/sorting: " + path);
                    sorting.sortingOrder = i < 6 ? 100 : i < 11 ? 0 : 10;
                    NativePrefabBuilder.SetReference(visual, "sorting", sorting);
                    if (i >= 11)
                        foreach (var mesh in root.GetComponentsInChildren<MeshRenderer>(true))
                            mesh.sharedMaterial = Required<Material>(EnvironmentRoot + "/Depleted.mat");
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException(path);
                    changed.Add(path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            EditEnvironment(vertices, changed);
            EditEffects(changed);
            Scene scene = EditorSceneManager.OpenScene(PinewatchLayoutSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var mesh in root.GetComponentsInChildren<MeshRenderer>(true))
                        if (mesh.sharedMaterial == source)
                        {
                            mesh.sharedMaterial = vertices;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(mesh);
                        }
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save Pinewatch.");
                changed.Add(PinewatchLayoutSetup.ScenePath);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            AssetDatabase.SaveAssets();
            NativeObjectContracts.Validate();
            NativeArtSetup.Validate();
            report["prefabs"] = Objects.Length + 3;
            report["passed"] = true;
        }
        catch (Exception error) { report["exception"] = error.ToString(); }
        File.WriteAllText(output, report.ToString());
        return report;
    }

    private static void EditEnvironment(Material material, JArray changed)
    {
        string path = EnvironmentRoot + "/Environment.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var terrain = root.GetComponentsInChildren<MeshRenderer>(true).Single();
            terrain.sharedMaterial = material;
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException(path);
            changed.Add(path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void EditEffects(JArray changed)
    {
        Font font = NativeEffectsSetup.CreateFloatingFont(); changed.Add(AssetDatabase.GetAssetPath(font));
        string path = NativeEffectsSetup.Root + "/Floating/Floating.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var text = root.GetComponentsInChildren<Text>(true).Single();
            text.font = font;
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException(path);
            changed.Add(path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var ringMaterial = Required<Material>(NativeEffectsSetup.Root + "/CommandRing.mat");
        ringMaterial.shader = Shader.Find("Dark Nights/Camp Sprite");
        ringMaterial.SetFloat("_UseGlobalAmbient", 1); ringMaterial.SetFloat("_VertexColorIsGamma", 1);
        EditorUtility.SetDirty(ringMaterial); changed.Add(AssetDatabase.GetAssetPath(ringMaterial));
        path = NativeEffectsSetup.Root + "/Command/Command.prefab";
        root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var line = root.GetComponentsInChildren<LineRenderer>(true).Single();
            GameObject ring = line.gameObject; UnityEngine.Object.DestroyImmediate(line);
            var filter = ring.AddComponent<MeshFilter>();
            var renderer = ring.AddComponent<MeshRenderer>(); renderer.sharedMaterial = ringMaterial;
            renderer.sortingOrder = 160; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var effect = root.GetComponent<NativeEffect>(); NativePrefabBuilder.SetReference(effect, "ring", filter);
            effect.Present(new VisualCue("command", 0, 320), 0, 320);
            Mesh mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
            mesh.name = "Command Ring"; mesh.hideFlags = HideFlags.None;
            string meshPath = NativeEffectsSetup.Root + "/Command/CommandRing.asset";
            AssetDatabase.CreateAsset(mesh, meshPath); filter.sharedMesh = mesh; changed.Add(meshPath);
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException(path);
            changed.Add(path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static T Required<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing authored asset: " + path);
}
