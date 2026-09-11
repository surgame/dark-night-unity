using System;
using System.IO;
using System.Linq;
using DarkNights.View;
using GameCore.Editor.Objects.Definition;
using GameCore.Interactions;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.Objects.Singletons;
using GameCore.Objects.Types;
using GameCore.UI.UGUI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace DarkNights.Editor
{
    /// <summary>
    /// 显式批量安装五个 UGUI 面板及现有 Interaction Sessions 服务定义，输出仅限指定空 UI 目录。
    /// 所有面板经 UGUIManager／DefinitionReference 装配；生成绑定就绪后才保存资产，普通构建不调用此入口。
    /// </summary>
    public static class NativeUiSetup
    {
        public const string InputPath = "Assets/DarkNights/Scripts/Editor/UiLayoutInput.json";
        public const string Root = "Assets/DarkNights/Res/UI";

        [MenuItem("Dark Nights/Content/Install Initial Native UI")]
        public static void Install()
        {
            if (Directory.Exists(Root) && Directory.GetFileSystemEntries(Root).Length != 0)
                throw new InvalidOperationException("UI output must be empty; native edits are never overwritten.");
            JObject input = JObject.Parse(File.ReadAllText(InputPath));
            Folder(Root + "/Shared");
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 15);
            Font title = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman" }, 87);
            SaveFont(font, "UIFont");
            SaveFont(title, "TitleFont");
            var database = AssetDatabase.LoadAssetAtPath<ObjectDefinitionDatabase>("Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset");
            JArray first = (JArray)input["profiles"][0]["pages"], second = (JArray)input["profiles"][1]["pages"];
            foreach (JObject page in first)
            {
                string name = (string)page["name"];
                Folder(Root + "/" + name);
                GameObject root = new GameObject(name, typeof(RectTransform));
                try
                {
                    var instance = root.AddComponent<ObjectInstance>();
                    var initializer = root.AddComponent<LocalObjectInstanceInitializer>();
                    UGUIView view = root.AddComponent<UGUIView>();
                    NativePrefabBuilder.SetReference(instance, "_view", view);
                    NativePrefabBuilder.SetReference(initializer, "_objectInstance", instance);
                    var bindings = NativeUiBuilder.Populate(root, page, second.Cast<JObject>().Single(p => (string)p["name"] == name), font, title);
                    NativeUiExtras.Add(root, name, font, bindings);
                    NativeUiBuilder.Bind(view, bindings);
                    root.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    root.AddComponent<GraphicRaycaster>();
                    root.AddComponent<UGUIAuthoringCanvas>();
                    root.AddComponent<FullscreenPanel>();
                    string path = Root + "/" + name + "/" + name + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    ObjectDefinition definition = Definition(Root + "/" + name + "/" + name + ".asset", "ui." + name.ToLowerInvariant());
                    definition.Type = name == "Chrome" ? ObjectType.UI_HUD : ObjectType.UI_Panel;
                    definition.PrefabRef = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(path));
                    definition.BehaviourTypes.Add(Behaviour(name).FullName);
                    EditorUtility.SetDirty(definition);
                    database.AddDefinition(definition);
                    var settings = AddressableAssetSettingsDefaultObject.Settings;
                    settings.CreateOrMoveEntry(definition.PrefabRef.AssetGUID, settings.DefaultGroup).address = "dark_nights.ui." + name.ToLowerInvariant();
                    EditorUtility.SetDirty(settings);
                    EditorUtility.SetDirty(settings.DefaultGroup);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            ObjectDefinition interaction = Definition(Root + "/Shared/InteractionSessions.asset", "service.interaction_sessions");
            interaction.BehaviourTypes.Add(typeof(YYInteractionSessionService).FullName);
            EditorUtility.SetDirty(interaction);
            database.AddDefinition(interaction);
            var singletons = AssetDatabase.LoadAssetAtPath<ObjectSingletonDatabase>("Assets/Addressables/Datas/GlobalSO/ObjectSingletonDatabase.asset");
            if (singletons.SingletonRecords.Any(r => r.BehaviourTypeName == typeof(YYInteractionSessionService).AssemblyQualifiedName))
                throw new InvalidOperationException("Interaction service already registered.");
            singletons.SingletonRecords.Add(new SingletonRecord
            {
                BehaviourTypeName = typeof(YYInteractionSessionService).AssemblyQualifiedName,
                DefinitionGuid = interaction.GuidString, IsEnabled = true
            });
            EditorUtility.SetDirty(singletons);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log("DARK_NIGHTS_NATIVE_UI_INSTALLED panels=5 interactionServices=1");
        }

        public static Type Behaviour(string name) => name switch
        {
            "Chrome" => typeof(CampHudBehaviour), "MainMenu" => typeof(MainMenuBehaviour),
            "PauseMenu" => typeof(PauseMenuBehaviour), "Help" => typeof(HelpMenuBehaviour),
            "Result" => typeof(ResultMenuBehaviour), _ => throw new ArgumentException("Unknown UI page: " + name)
        };

        private static ObjectDefinition Definition(string path, string key)
        {
            var result = ScriptableObject.CreateInstance<ObjectDefinition>();
            result.Name = key;
            result.NetType = NetworkType.Local;
            AssetDatabase.CreateAsset(result, path);
            result.EditorSetIdentity(DefinitionIdentityAuthoring.ReadAssetGuid(result), key, false);
            return result;
        }

        private static void SaveFont(Font font, string name)
        {
            // Dynamic OS fonts still need a persisted material and empty atlas to reload correctly.
            var material = UnityEngine.Object.Instantiate(font.material);
            var texture = UnityEngine.Object.Instantiate(font.material.mainTexture);
            material.name = name + " Material";
            texture.name = name + " Atlas";
            material.mainTexture = texture;
            AssetDatabase.CreateAsset(font, Root + "/Shared/" + name + ".fontsettings");
            AssetDatabase.AddObjectToAsset(material, font);
            AssetDatabase.AddObjectToAsset(texture, font);
            var serialized = new SerializedObject(font);
            serialized.FindProperty("m_DefaultMaterial").objectReferenceValue = material;
            serialized.FindProperty("m_Texture").objectReferenceValue = texture;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(font);
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
