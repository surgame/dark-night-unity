using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.View;
using GameCore.Objects.Runner;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.Editor
{
    /// <summary>
    /// 一批创建原生层级、精灵、动画及三类专用 EntityView；已有 Worker 只补齐已核实为空的外观。
    /// 节点名仅用于 Editor 首版源转换，运行时使用序列化引用；已有人工外观时拒绝覆盖。
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
                if (root.GetComponentInChildren<SpriteRenderer>(true) != null)
                    throw new InvalidOperationException("Visual output must be empty: " + path);
                ObjectInstance instance = root.GetComponent<ObjectInstance>();
                if (instance == null) instance = root.AddComponent<ObjectInstance>();
                LocalObjectInstanceInitializer initializer = root.GetComponent<LocalObjectInstanceInitializer>();
                if (initializer == null) initializer = root.AddComponent<LocalObjectInstanceInitializer>();
                EntityView view = RequireView(root, (string)spec["category"]);
                SetReference(instance, "_view", view);
                SetReference(initializer, "_objectInstance", instance);

                var sorting = root.AddComponent<SortingGroup>();
                sorting.sortingOrder = view is ActorView ? 100 : view is WorksiteView ? 10 : 0;
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
                        AddSprite(node, transform, key, sprites, renderers);
                    else if (type == "Polygon2D")
                        AddDepleted(node, transform, name, folder, depletedMaterial);
                }
                if (((JArray)spec["clips"]).Count > 0)
                    root.AddComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Configure(view, sorting, spec, nodes, renderers, sprites,
                    NativeAnimationBuilder.Create((JArray)spec["clips"], folder));
                view.EditorSetBindings(Array.Empty<GameCore.Objects.Views.ViewComponentBinding>(), false);
                view.ForceRefreshAllReferences();
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static EntityView RequireView(GameObject root, string category)
        {
            Type expected = category == "actors" ? typeof(ActorView) :
                category == "buildings" ? typeof(BuildingView) : typeof(WorksiteView);
            EntityView existing = root.GetComponent<EntityView>();
            if (existing != null)
            {
                if (existing.GetType() != expected) throw new InvalidOperationException("Worker skeleton has the wrong primary view.");
                return existing;
            }
            if (root.GetComponent<YY.Features.Players.View.ObjectView>() != null)
                throw new InvalidOperationException("Prefab still has a generic ObjectView.");
            return (EntityView)root.AddComponent(expected);
        }

        private static void AddSprite(JObject node, Transform transform, string key,
            List<SpriteRenderer> sprites, Dictionary<string, SpriteRenderer> renderers)
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

        private static void AddDepleted(JObject node, Transform transform, string name,
            string folder, Material depletedMaterial)
        {
            JArray polygon = (JArray)node["polygon"];
            var vertices = new Vector3[polygon.Count / 2];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new Vector3((float)polygon[i * 2] / 100, -(float)polygon[i * 2 + 1] / 100);
            if (vertices.Length != 4) throw new InvalidOperationException("Expected frozen depleted rectangle.");
            var mesh = new Mesh { name = name + " Depleted", vertices = vertices, triangles = new[] { 0, 1, 2, 0, 2, 3 } };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, folder + "/Depleted.asset");
            transform.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            transform.gameObject.AddComponent<MeshRenderer>().sharedMaterial = depletedMaterial;
            transform.gameObject.SetActive((bool)node["visible"]);
        }

        private static void Configure(EntityView view, SortingGroup sorting, JObject spec,
            Dictionary<string, Transform> nodes, Dictionary<string, SpriteRenderer> renderers,
            List<SpriteRenderer> sprites, PoseClip[] clips)
        {
            var serialized = new SerializedObject(view);
            serialized.FindProperty("portrait").objectReferenceValue = NativeAnimationBuilder.Sprite((string)spec["portrait"]);
            serialized.FindProperty("sorting").objectReferenceValue = sorting;
            JToken bounds = spec["pickBounds"];
            serialized.FindProperty("pickBounds").rectValue = new Rect((float)bounds[0] / 100,
                -((float)bounds[1] + (float)bounds[3]) / 100, (float)bounds[2] / 100, (float)bounds[3] / 100);
            SetTransform(serialized, "statusAnchor", nodes, Binding(spec, "StatusAnchor"));
            SetTransform(serialized, "selectionAnchor", nodes, Binding(spec, "SelectionAnchor"));
            SetTintTargets(serialized.FindProperty("tintTargets").FindPropertyRelative("targets"), sprites);
            if (view is ActorView)
            {
                SetTransform(serialized, "facing", nodes, Binding(spec, "Facing"));
                SetTransform(serialized, "poseRoot", nodes, Binding(spec, "Origin"));
                SetRenderer(serialized, "clothing", renderers, Binding(spec, "Clothing"));
                serialized.FindProperty("standingOffset").vector2Value = Point(spec["standing"]);
                serialized.FindProperty("deathOffset").vector2Value = Point(spec["death"]);
                SetPoses(serialized.FindProperty("clips"), clips);
            }
            else if (view is BuildingView)
            {
                SetRenderer(serialized, "complete", renderers, Binding(spec, "Complete"));
                SetRenderer(serialized, "foundation", renderers, Binding(spec, "Foundation"));
                SetRenderer(serialized, "rubble", renderers, Binding(spec, "Rubble"));
                serialized.FindProperty("fadeConstruction").boolValue = (bool)spec["fade"];
                SetPoses(serialized.FindProperty("clips"), clips);
            }
            else
            {
                string prefix = Binding(spec, "Variants");
                SetArray(serialized.FindProperty("variants"), renderers.Where(item =>
                    item.Key.StartsWith(prefix + "/", StringComparison.Ordinal)).Select(item => item.Value).ToArray());
                serialized.FindProperty("depleted").objectReferenceValue = nodes[Binding(spec, "Depleted")].gameObject;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string Binding(JObject spec, string name) => (string)spec["bindings"]?[name];

        private static void SetTransform(SerializedObject target, string field,
            Dictionary<string, Transform> nodes, string key)
        {
            target.FindProperty(field).objectReferenceValue = string.IsNullOrEmpty(key) ? null : nodes[key];
        }

        private static void SetRenderer(SerializedObject target, string field,
            Dictionary<string, SpriteRenderer> renderers, string key)
        {
            target.FindProperty(field).objectReferenceValue = string.IsNullOrEmpty(key) ? null : renderers[key];
        }

        private static void SetTintTargets(SerializedProperty target, IReadOnlyList<SpriteRenderer> sprites)
        {
            target.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
            {
                SerializedProperty item = target.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("renderer").objectReferenceValue = sprites[i];
                item.FindPropertyRelative("baseColor").colorValue = sprites[i].color;
            }
        }

        private static void SetPoses(SerializedProperty target, IReadOnlyList<PoseClip> clips)
        {
            target.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++)
            {
                SerializedProperty item = target.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Name").stringValue = clips[i].Name;
                item.FindPropertyRelative("Clip").objectReferenceValue = clips[i].Clip;
                item.FindPropertyRelative("Duration").doubleValue = clips[i].Duration;
                item.FindPropertyRelative("Loop").boolValue = clips[i].Loop;
            }
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
