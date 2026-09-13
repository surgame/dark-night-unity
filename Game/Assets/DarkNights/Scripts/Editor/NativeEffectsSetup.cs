using System;
using System.IO;
using System.Linq;
using DarkNights.View;
using GameCore.Editor.Objects.Definition;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.Objects.Views;
using GameCore.UI.UGUI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YY.Features.Players.View;

namespace DarkNights.Editor
{
    /// <summary>
    /// 在空 Effects 目录安装首版箭矢、浮字、命令圈和音频 Prefab，并为已有 HUD 增加显式淡出绑定。
    /// 尸体和废墟复用既有对象定义；本入口不在导入或构建时执行，后续原生编辑不会被覆盖。
    /// </summary>
    public static class NativeEffectsSetup
    {
        public const string Root = "Assets/DarkNights/Res/Effects";
        [MenuItem("Dark Nights/Content/Install Initial Native Effects")]
        public static void Install()
        {
            if (Directory.Exists(Root) && Directory.GetFileSystemEntries(Root).Length != 0)
                throw new InvalidOperationException("Effects output must be empty.");
            var hudAsset = AssetDatabase.LoadAssetAtPath<GameObject>(NativeUiSetup.Root + "/Chrome/Chrome.prefab");
            if (hudAsset.GetComponent<UGUIView>().Bindings.Any(b => b.Key == "ToastFade" || b.Key == "BannerFade"))
                throw new InvalidOperationException("HUD effect bindings already exist.");
            Folder(Root);
            var lineMaterial = new Material(Shader.Find("Dark Nights/Camp Sprite")) { name = "Command Ring" };
            lineMaterial.SetFloat("_UseGlobalAmbient", 1); lineMaterial.SetFloat("_VertexColorIsGamma", 1);
            AssetDatabase.CreateAsset(lineMaterial, Root + "/CommandRing.mat");
            foreach (string name in new[] { "Arrow", "Floating", "Command", "Audio" }) Create(name, lineMaterial);
            AddHudFade();
            AssetDatabase.SaveAssets();
            Debug.Log("DARK_NIGHTS_NATIVE_EFFECTS_INSTALLED definitions=4 prefabs=4 remnantDefinitions=15 hudFades=2");
        }

        private static void Create(string name, Material lineMaterial)
        {
            Folder(Root + "/" + name);
            var root = new GameObject(name);
            try
            {
                var instance = root.AddComponent<ObjectInstance>();
                var initializer = root.AddComponent<LocalObjectInstanceInitializer>();
                var view = root.AddComponent<ObjectView>();
                NativePrefabBuilder.SetReference(instance, "_view", view);
                NativePrefabBuilder.SetReference(initializer, "_objectInstance", instance);
                Component binding;
                if (name == "Audio") binding = Audio(root);
                else
                {
                    var effect = root.AddComponent<NativeEffect>();
                    binding = effect;
                    if (name == "Arrow")
                    {
                        var sprite = Child(root.transform, "Sprite").AddComponent<SpriteRenderer>();
                        sprite.sprite = NativeAnimationBuilder.Sprite("res://assets/sprites/spr_archer_arrow/spr_archer_arrow_0.png");
                        sprite.transform.localPosition = new Vector3(-0.04f, 0.04f);
                        sprite.sortingOrder = 150;
                    }
                    else if (name == "Command")
                    {
                        var ring = Child(root.transform, "Ring").AddComponent<MeshFilter>();
                        var renderer = ring.gameObject.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = lineMaterial; renderer.sortingOrder = 160;
                        NativePrefabBuilder.SetReference(effect, "ring", ring);
                        effect.Present(new DarkNights.Core.ViewData.VisualCue("command", 0, 320), 0, 320);
                        var mesh = UnityEngine.Object.Instantiate(ring.sharedMesh);
                        mesh.name = "Command Ring"; mesh.hideFlags = HideFlags.None;
                        AssetDatabase.CreateAsset(mesh, Root + "/Command/CommandRing.asset"); ring.sharedMesh = mesh;
                    }
                    else Floating(root, effect);
                }
                view.EditorSetBindings(new[] { new ViewComponentBinding(name == "Audio" ? "audio" : "effect", binding) }, false);
                view.ForceRefreshAllReferences();
                string path = Root + "/" + name + "/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
                definition.Name = name;
                definition.NetType = NetworkType.Local;
                definition.PrefabRef = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(path));
                AssetDatabase.CreateAsset(definition, Root + "/" + name + "/" + name + ".asset");
                definition.EditorSetIdentity(DefinitionIdentityAuthoring.ReadAssetGuid(definition),
                    name == "Audio" ? "audio.camp" : "effect." + name.ToLowerInvariant(), false);
                EditorUtility.SetDirty(definition);
                var database = AssetDatabase.LoadAssetAtPath<ObjectDefinitionDatabase>("Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset");
                database.AddDefinition(definition); EditorUtility.SetDirty(database);
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                settings.CreateOrMoveEntry(definition.PrefabRef.AssetGUID, settings.DefaultGroup).address = "dark_nights.effect." + name.ToLowerInvariant();
                EditorUtility.SetDirty(settings); EditorUtility.SetDirty(settings.DefaultGroup);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Floating(GameObject root, NativeEffect effect)
        {
            var canvas = Child(root.transform, "TextCanvas", true).AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 170;
            canvas.transform.localScale = Vector3.one * 0.01f;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.pivot = Vector2.zero; canvasRect.sizeDelta = Vector2.zero;
            var label = Child(canvas.transform, "Text", true).AddComponent<Text>();
            label.font = CreateFloatingFont();
            label.fontSize = 7; label.alignment = TextAnchor.LowerLeft; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.zero;
            label.rectTransform.pivot = Vector2.zero; label.rectTransform.sizeDelta = new Vector2(80, 12);
            var icon = Child(canvas.transform, "Icon", true).AddComponent<Image>();
            icon.raycastTarget = false; icon.rectTransform.sizeDelta = new Vector2(8, 8);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = Vector2.zero;
            icon.rectTransform.pivot = new Vector2(0, 1);
            NativePrefabBuilder.SetReference(effect, "label", label);
            NativePrefabBuilder.SetReference(effect, "icon", icon);
            var serialized = new SerializedObject(effect);
            var icons = serialized.FindProperty("resources"); icons.arraySize = 5;
            for (int i = 0; i < 5; i++) icons.GetArrayElementAtIndex(i).objectReferenceValue = NativeAnimationBuilder.Sprite(
                "res://assets/sprites/spr_int_resources/spr_int_resources_" + (i + 1) + ".png");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            effect.Present(new DarkNights.Core.ViewData.VisualCue("damage", 0, 320, "12"), 0, 320);
        }

        /// <summary>在专用空路径创建七像素世界字体，持久化独立材质和点采样图集，避免影响菜单字体。</summary>
        public static Font CreateFloatingFont()
        {
            string path = Root + "/Floating/FloatingFont.fontsettings";
            if (File.Exists(path)) throw new InvalidOperationException("Floating font output must be empty.");
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 7);
            var material = UnityEngine.Object.Instantiate(font.material);
            var texture = UnityEngine.Object.Instantiate(font.material.mainTexture);
            material.name = "Floating Font Material"; texture.name = "Floating Font Atlas";
            texture.filterMode = FilterMode.Point; material.mainTexture = texture;
            AssetDatabase.CreateAsset(font, path);
            AssetDatabase.AddObjectToAsset(material, font); AssetDatabase.AddObjectToAsset(texture, font);
            var data = new SerializedObject(font);
            data.FindProperty("m_DefaultMaterial").objectReferenceValue = material;
            data.FindProperty("m_Texture").objectReferenceValue = texture;
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(font);
            return font;
        }

        private static CampAudio Audio(GameObject root)
        {
            var binding = root.AddComponent<CampAudio>();
            var music = Child(root.transform, "Music").AddComponent<AudioSource>();
            music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(NativeArtSetup.OriginalRoot + "/audio/snd_vindsvept_hollow.ogg");
            music.volume = Mathf.Pow(10, -23f / 20); music.loop = true; music.playOnAwake = false;
            var effects = Child(root.transform, "Sounds").AddComponent<AudioSource>(); effects.playOnAwake = false;
            NativePrefabBuilder.SetReference(binding, "music", music);
            NativePrefabBuilder.SetReference(binding, "effects", effects);
            string[] paths = Directory.GetFiles(NativeArtSetup.OriginalRoot + "/audio", "*.wav").OrderBy(p => p, StringComparer.Ordinal).ToArray();
            var serialized = new SerializedObject(binding);
            var keys = serialized.FindProperty("keys"); var clips = serialized.FindProperty("clips");
            keys.arraySize = clips.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++)
            {
                keys.GetArrayElementAtIndex(i).stringValue = Path.GetFileNameWithoutExtension(paths[i]);
                clips.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i].Replace('\\', '/'));
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return binding;
        }

        private static void AddHudFade()
        {
            string path = NativeUiSetup.Root + "/Chrome/Chrome.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<UGUIView>();
                var bindings = view.Bindings.ToList();
                foreach (string key in new[] { "ToastPanel", "Banner" })
                {
                    RectTransform panel = view.Get<RectTransform>(key);
                    var group = panel.gameObject.AddComponent<CanvasGroup>();
                    group.blocksRaycasts = false;
                    bindings.Add(new ViewComponentBinding(key == "ToastPanel" ? "ToastFade" : "BannerFade", group));
                }
                view.EditorSetBindings(bindings.ToArray(), false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static GameObject Child(Transform parent, string name, bool rect = false)
        {
            var child = rect ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
            child.transform.SetParent(parent, false); return child;
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
