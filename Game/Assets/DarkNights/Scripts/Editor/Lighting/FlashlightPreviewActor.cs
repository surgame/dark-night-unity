using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.View;
using DarkNights.View.Lighting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor.Lighting
{
    /// <summary>光照预览的被动人物与手电视觉副本；沿用作者 Sprite、层次、动画和明确挂点，不复制 YYGC 对象、业务脚本或碰撞器。</summary>
    public sealed class FlashlightPreviewActor : IDisposable
    {
        private readonly Dictionary<UnityEngine.Object, UnityEngine.Object> copies = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        private readonly LightEffectBinding binding = new LightEffectBinding();
        private readonly LightFillComposer fill = new LightFillComposer();
        private readonly List<LightEffect> effects = new List<LightEffect>(1);
        private GameObject root, tool;
        private Transform facing, pivot, emitter, mount, fillAnchor;
        private SpriteRenderer arm;
        private SpriteRenderer[] receivers;
        private Vector3 toolScale;
        public LightEffect Effect => binding.Effect;

        public void Open(Scene scene, FlashlightDebugDraft draft)
        {
            var author = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DarkNights/Res/Objects/Worker/Worker.prefab");
            var actor = author != null ? author.GetComponent<ActorView>() : null;
            if (actor == null || actor.LightAnchor == null || actor.LightFillAnchor == null) throw new InvalidOperationException("人物预览缺少原生 ActorView 或明确光照挂点。");
            if (draft.Definition?.PrefabRef == null) throw new InvalidOperationException("人物手电预览需要有效 Definition／Prefab。");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(draft.Definition.PrefabRef.AssetGUID));
            var flashlight = source != null ? source.GetComponent<FlashlightView>() : null;
            if (flashlight?.Emitter == null) throw new InvalidOperationException("手电预览缺少原生 FlashlightView／Emitter。");
            try
            {
                root = CopyTree(author, null); SceneManager.MoveGameObjectToScene(root, scene);
                root.name = "光照调参 · 被动人物";
                root.transform.localScale = author.transform.localScale * (EntityView.PixelsPerUnit / 16f);
                var idle = actor.Clips.FirstOrDefault(clip => clip.Name == "idle");
                if (idle == null) throw new InvalidOperationException("人物预览缺少原生 idle 动画。");
                idle.Sample(root, 0);
                var actorProperties = new SerializedObject(actor);
                facing = Copy<Transform>(actorProperties.FindProperty("facing").objectReferenceValue);
                var handheld = actorProperties.FindProperty("handheld").objectReferenceValue as HandheldView;
                if (handheld == null) throw new InvalidOperationException("人物缺少显式手持部件绑定。");
                var heldProperties = new SerializedObject(handheld);
                pivot = Copy<Transform>(heldProperties.FindProperty("pivot").objectReferenceValue);
                arm = Copy<SpriteRenderer>(heldProperties.FindProperty("arm").objectReferenceValue);
                Copy<SpriteRenderer>(heldProperties.FindProperty("item").objectReferenceValue).enabled = false;
                Copy<SpriteRenderer>(heldProperties.FindProperty("flash").objectReferenceValue).enabled = false;
                var parts = heldProperties.FindProperty("occupationalParts");
                for (int i = 0; i < parts.arraySize; i++) Copy<SpriteRenderer>(parts.GetArrayElementAtIndex(i).objectReferenceValue).enabled = false;
                mount = Copy<Transform>(actor.LightAnchor); fillAnchor = Copy<Transform>(actor.LightFillAnchor);
                tool = CopyTree(source, mount); tool.name = "光照调参 · 被动手电"; toolScale = tool.transform.localScale;
                emitter = Copy<Transform>(flashlight.Emitter);
                var body = Copy<SpriteRenderer>(new SerializedObject(flashlight).FindProperty("body").objectReferenceValue);
                receivers = actor.LightTargets.Select(Copy<SpriteRenderer>).Concat(new[] { body }).ToArray();
            }
            catch { Dispose(); throw; }
        }

        private T Copy<T>(UnityEngine.Object value) where T : UnityEngine.Object
        {
            if (value == null || !copies.TryGetValue(value, out var clone) || !(clone is T typed))
                throw new InvalidOperationException("作者预览绑定不属于对应视觉树：" + value);
            return typed;
        }
        private GameObject CopyTree(GameObject source, Transform parent)
        {
            var clone = new GameObject(source.name) { hideFlags = HideFlags.HideAndDontSave, layer = source.layer };
            var transform = clone.transform; transform.SetParent(parent, false);
            transform.localPosition = source.transform.localPosition; transform.localRotation = source.transform.localRotation; transform.localScale = source.transform.localScale;
            copies.Add(source.transform, transform);
            foreach (var sprite in source.GetComponents<SpriteRenderer>())
            {
                var renderer = clone.AddComponent<SpriteRenderer>(); renderer.sprite = sprite.sprite; renderer.color = sprite.color;
                renderer.sharedMaterial = sprite.sharedMaterial; renderer.sortingLayerID = sprite.sortingLayerID; renderer.sortingOrder = sprite.sortingOrder;
                renderer.flipX = sprite.flipX; renderer.flipY = sprite.flipY; renderer.enabled = sprite.enabled;
                renderer.drawMode = sprite.drawMode; renderer.size = sprite.size; renderer.maskInteraction = sprite.maskInteraction;
                copies.Add(sprite, renderer);
            }
            var sorting = source.GetComponent<SortingGroup>();
            if (sorting != null)
            { var group = clone.AddComponent<SortingGroup>(); group.sortingLayerID = sorting.sortingLayerID; group.sortingOrder = sorting.sortingOrder; }
            foreach (Transform child in source.transform) CopyTree(child.gameObject, transform);
            clone.SetActive(source.activeSelf); return clone;
        }
        public void Present(FlashlightDebugDraft draft, LightProfileSnapshot profile, Vector2 position, float angle, bool saved)
        {
            root.transform.position = new Vector3(position.x - .5f, -position.y + .5f, -1);
            float face = Mathf.Cos(angle * Mathf.Deg2Rad) < 0 ? -1 : 1;
            facing.localScale = new Vector3(face, 1, 1);
            pivot.localPosition = new Vector3(.015f, .09f, 0);
            pivot.localRotation = Quaternion.Euler(0, 0, face < 0 ? 180 - angle : angle);
            arm.enabled = true;
            tool.transform.localScale = new Vector3(Mathf.Abs(toolScale.x) * (tool.transform.parent.lossyScale.x < 0 ? -1 : 1), toolScale.y, toolScale.z);
            tool.transform.rotation = Quaternion.Euler(0, 0, angle);
            emitter.localPosition = saved ? draft.SavedMountPosition : draft.MountPosition;
            emitter.localEulerAngles = saved ? draft.SavedMountRotation : draft.MountRotation;
            emitter.localScale = saved ? draft.SavedMountScale : draft.MountScale;
            binding.Bind(profile, draft, emitter, mount, fillAnchor, receivers); binding.SetOn(true);
            foreach (Transform child in binding.Effect.transform) child.gameObject.hideFlags = HideFlags.HideAndDontSave;
            binding.Effect.gameObject.hideFlags = HideFlags.HideAndDontSave;
            effects.Clear(); effects.Add(binding.Effect); fill.Apply(effects, 1);
        }
        public void Dispose()
        {
            fill.Clear(); binding.Dispose(); effects.Clear(); copies.Clear();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            root = null; tool = null;
        }
    }
}
