using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.View;
using GameCore.Objects.Runner;
using GameCore.Objects.Views;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using YY.Features.Players.View;

namespace DarkNights.Editor
{
    /// <summary>
    /// 一批创建原生层级、精灵、动画和显式 ObjectView 绑定；已有 Worker 仅补齐已核实为空的外观部分。
    /// 节点名仅用于 Editor 首版源转换，运行时必须使用序列化引用；保存失败不覆写其他对象资产。
    /// </summary>
    public static class NativePrefabBuilder
    {
        public static GameObject Create(JObject spec, string folder, Material depletedMaterial)
        {
            string name = (string)spec["name"];
            string path = folder + "/" + name + ".prefab";
            bool existing = name == "Worker";
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
            try
            {
                if (root.GetComponentInChildren<SpriteRenderer>(true) != null || root.GetComponent<NativeVisual>() != null)
                    throw new InvalidOperationException("Visual output must be empty: " + path);
                ObjectInstance instance = root.GetComponent<ObjectInstance>();
                if (instance == null) instance = root.AddComponent<ObjectInstance>();
                LocalObjectInstanceInitializer initializer = root.GetComponent<LocalObjectInstanceInitializer>();
                if (initializer == null) initializer = root.AddComponent<LocalObjectInstanceInitializer>();
                ObjectView view = root.GetComponent<ObjectView>();
                if (view == null) view = root.AddComponent<ObjectView>();
                SetReference(instance, "_view", view);
                SetReference(initializer, "_objectInstance", instance);
                var sorting = root.AddComponent<SortingGroup>();
                sorting.sortingOrder = (string)spec["category"] == "actors" ? 100 : (string)spec["category"] == "worksites" ? 10 : 0;
                var nodes = new Dictionary<string, Transform> { [""] = root.transform };
                var sprites = new List<SpriteRenderer>();
                var renderers = new Dictionary<string, SpriteRenderer>();
                foreach (JObject node in spec["nodes"])
                {
                    string parent = (string)node["parent"];
                    string type = (string)node["type"];
                    if (parent == "" || type == "AnimationPlayer") continue;
                    parent = parent == "." ? "" : parent;
                    string nodeName = (string)node["name"];
                    Transform transform = nodes[parent].Find(nodeName);
                    if (transform == null) transform = Child(nodes[parent], nodeName);
                    transform.localPosition = Point(node["position"]);
                    string key = parent == "" ? nodeName : parent + "/" + nodeName;
                    nodes.Add(key, transform);
                    if (type == "Sprite2D")
                    {
                        Transform art = Child(transform, "Sprite");
                        art.localPosition = Point(node["offset"]);
                        SpriteRenderer renderer = art.gameObject.AddComponent<SpriteRenderer>();
                        renderer.sprite = NativeAnimationBuilder.Sprite((string)node["texture"]);
                        renderer.enabled = (bool)node["visible"];
                        renderer.color = ColorValue(node["color"]);
                        renderer.sortingOrder = sprites.Count;
                        sprites.Add(renderer);
                        renderers.Add(key, renderer);
                    }
                    else if (type == "Polygon2D")
                    {
                        JArray polygon = (JArray)node["polygon"];
                        var vertices = new Vector3[polygon.Count / 2];
                        for (int i = 0; i < vertices.Length; i++) vertices[i] = new Vector3((float)polygon[i * 2] / 100, -(float)polygon[i * 2 + 1] / 100);
                        if (vertices.Length != 4) throw new InvalidOperationException("Expected frozen depleted rectangle.");
                        var mesh = new Mesh { name = name + " Depleted", vertices = vertices, triangles = new[] { 0, 1, 2, 0, 2, 3 } };
                        mesh.RecalculateBounds();
                        AssetDatabase.CreateAsset(mesh, folder + "/Depleted.asset");
                        transform.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                        transform.gameObject.AddComponent<MeshRenderer>().sharedMaterial = depletedMaterial;
                        transform.gameObject.SetActive((bool)node["visible"]);
                    }
                }
                NativeVisual visual = root.AddComponent<NativeVisual>();
                SetReference(visual, "sorting", sorting);
                if (((JArray)spec["clips"]).Count > 0)
                    root.AddComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Configure(visual, spec, nodes, renderers, sprites, NativeAnimationBuilder.Create((JArray)spec["clips"], folder));
                var bindings = view.Bindings.ToList();
                bindings.Add(new ViewComponentBinding("visual", visual));
                view.EditorSetBindings(bindings.ToArray(), false);
                view.ForceRefreshAllReferences();
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Configure(NativeVisual visual, JObject spec, Dictionary<string, Transform> nodes,
            Dictionary<string, SpriteRenderer> renderers, List<SpriteRenderer> sprites, PoseClip[] clips)
        {
            var serialized = new SerializedObject(visual);
            serialized.FindProperty("portrait").objectReferenceValue = NativeAnimationBuilder.Sprite((string)spec["portrait"]);
            JToken bounds = spec["pickBounds"];
            serialized.FindProperty("pickBounds").rectValue = new Rect((float)bounds[0] / 100,
                -((float)bounds[1] + (float)bounds[3]) / 100, (float)bounds[2] / 100, (float)bounds[3] / 100);
            foreach (JProperty binding in ((JObject)spec["bindings"]).Properties())
            {
                if (binding.Name == "Animator") continue;
                string field = char.ToLowerInvariant(binding.Name[0]) + binding.Name.Substring(1);
                string key = (string)binding.Value;
                if (field == "variants")
                {
                    var values = renderers.Where(item => item.Key.StartsWith(key + "/", StringComparison.Ordinal)).Select(item => item.Value).ToArray();
                    SetArray(serialized.FindProperty(field), values);
                }
                else serialized.FindProperty(field).objectReferenceValue = field == "depleted" ?
                    (UnityEngine.Object)nodes[key].gameObject : renderers.TryGetValue(key, out SpriteRenderer renderer) ? renderer : (UnityEngine.Object)nodes[key];
            }
            serialized.FindProperty("standingOffset").vector2Value = Point(spec["standing"]);
            serialized.FindProperty("deathOffset").vector2Value = Point(spec["death"]);
            serialized.FindProperty("fadeConstruction").boolValue = (bool)spec["fade"];
            SetArray(serialized.FindProperty("sprites"), sprites.ToArray());
            SerializedProperty colors = serialized.FindProperty("baseColors");
            colors.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++) colors.GetArrayElementAtIndex(i).colorValue = sprites[i].color;
            SerializedProperty poses = serialized.FindProperty("clips");
            poses.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
            {
                SerializedProperty item = poses.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Name").stringValue = clips[i].Name;
                item.FindPropertyRelative("Clip").objectReferenceValue = clips[i].Clip;
                item.FindPropertyRelative("Duration").doubleValue = clips[i].Duration;
                item.FindPropertyRelative("Loop").boolValue = clips[i].Loop;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(SerializedProperty target, UnityEngine.Object[] values)
        {
            target.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) target.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        public static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Vector2 Point(JToken value) => new Vector2((float)value[0] / 100, -(float)value[1] / 100);
        private static Color ColorValue(JToken value) => new Color((float)value[0], (float)value[1], (float)value[2], (float)value[3]);
    }
}
