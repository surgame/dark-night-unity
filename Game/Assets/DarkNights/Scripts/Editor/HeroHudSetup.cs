using System;
using System.Collections.Generic;
using System.IO;
using DarkNights.View;
using GameCore.Editor.Objects.Definition;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.Objects.Types;
using GameCore.UI.UGUI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace DarkNights.Editor
{
    /// <summary>
    /// 向指定空目录初建主角道具栏 Prefab 与 YYGC 定义，不修改已有五个面板的布局或资源身份。
    /// 动作绑定使用正式 UGUI 生成器；场景及普通导入不调用此安装入口。
    /// </summary>
    public static class HeroHudSetup
    {
        public const string Root = "Assets/DarkNights/Res/UI/Hero";
        public static void Create(ObjectDefinitionDatabase database)
        {
            if (Directory.Exists(Root)) throw new InvalidOperationException("Hero HUD output must be absent.");
            Directory.CreateDirectory(Root);
            var root = new GameObject("Hero", typeof(RectTransform));
            try
            {
                var instance = root.AddComponent<ObjectInstance>();
                var initializer = root.AddComponent<LocalObjectInstanceInitializer>();
                var view = root.AddComponent<UGUIView>();
                NativePrefabBuilder.SetReference(instance, "_view", view);
                NativePrefabBuilder.SetReference(initializer, "_objectInstance", instance);
                root.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                root.AddComponent<GraphicRaycaster>(); root.AddComponent<UGUIAuthoringCanvas>(); root.AddComponent<FullscreenPanel>();
                var panel = (RectTransform)new GameObject("Toolbar", typeof(RectTransform), typeof(Image)).transform;
                panel.SetParent(root.transform, false);
                panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0); panel.pivot = new Vector2(.5f, 0);
                panel.anchoredPosition = new Vector2(0, 300); panel.sizeDelta = new Vector2(730, 82);
                panel.GetComponent<Image>().color = new Color(.045f, .08f, .10f, .95f);
                Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/DarkNights/Res/UI/Shared/UIFont.fontsettings");
                var bindings = new Dictionary<string, Component>();
                NativeUiExtras.AddButton(panel, "Toggle", "操控居民 [Tab]", 8, 8, 144, 34, font, bindings);
                NativeUiExtras.AddButton(panel, "Item1", "1 职业武器", 160, 8, 126, 34, font, bindings);
                NativeUiExtras.AddButton(panel, "Item2", "2 工作工具", 294, 8, 126, 34, font, bindings);
                NativeUiExtras.AddButton(panel, "Item3", "3 喷气背包", 428, 8, 164, 34, font, bindings);
                NativeUiExtras.AddButton(panel, "Rebind", "修改跳跃键", 600, 8, 122, 34, font, bindings);
                var labelRoot = (RectTransform)new GameObject("Status", typeof(RectTransform), typeof(Text)).transform;
                labelRoot.SetParent(panel, false); labelRoot.anchorMin = labelRoot.anchorMax = labelRoot.pivot = Vector2.zero;
                labelRoot.anchoredPosition = new Vector2(12, 47); labelRoot.sizeDelta = new Vector2(706, 28);
                Text label = labelRoot.GetComponent<Text>(); label.font = font; label.fontSize = 14;
                label.color = new Color32(228, 229, 215, 255); label.raycastTarget = false;
                bindings.Add("Status", label); NativeUiBuilder.Bind(view, bindings);
                string prefab = Root + "/Hero.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefab);
                var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
                definition.Name = "主角道具栏"; definition.Type = ObjectType.UI_HUD; definition.NetType = NetworkType.Local;
                AssetDatabase.CreateAsset(definition, Root + "/Hero.asset");
                definition.EditorSetIdentity(DefinitionIdentityAuthoring.ReadAssetGuid(definition), "ui.hero", false);
                definition.PrefabRef = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(prefab));
                definition.BehaviourTypes.Add(typeof(HeroHudBehaviour).FullName);
                database.AddDefinition(definition); EditorUtility.SetDirty(definition);
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                settings.CreateOrMoveEntry(definition.PrefabRef.AssetGUID, settings.DefaultGroup).address = "dark_nights.ui.hero";
                EditorUtility.SetDirty(settings); EditorUtility.SetDirty(settings.DefaultGroup);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
