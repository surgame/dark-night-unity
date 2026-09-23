using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GameCore.Samples.InputActions.Editor
{
    /// <summary>
    /// 仅在空 Content 目录初建可直接打开的输入示例场景；依赖脚本编译完成后执行。
    /// 正常导入与构建不调用此生成器，不覆盖已维护的动作、场景或 GUID。
    /// </summary>
    public static class InputActionsSampleBuilder
    {
        public const string Root = "Assets/Samples/YYGCInputActions/Content";
        [MenuItem("YY/Samples/Create Input Actions Sample In Empty Directory")]
        public static void Create()
        {
            if (Directory.Exists(Root)) throw new InvalidOperationException("Sample Content must be absent; existing assets are protected.");
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            foreach (string name in new[] { "Player", "Camp" })
            {
                var map = asset.AddActionMap(name);
                var move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Axis");
                move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
                map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
                map.AddAction("UseItem", InputActionType.Button, "<Mouse>/leftButton");
            }
            var ui = asset.AddActionMap("UI");
            ui.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position", expectedControlLayout: "Vector2");
            ui.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton", expectedControlLayout: "Button");
            ui.AddAction("ScrollWheel", InputActionType.PassThrough, "<Mouse>/scroll", expectedControlLayout: "Vector2");
            ui.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
            ui.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            ui.AddAction("Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2")
                .AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            File.WriteAllText(Root + "/InputActions.inputactions", asset.ToJson());
            UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(Root + "/InputActions.inputactions", ImportAssetOptions.ForceSynchronousImport);
            asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root + "/InputActions.inputactions");
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 5;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.backgroundColor = new Color(0.04f, 0.07f, 0.12f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, Root + "/Demo.mat");
            var pixel = new Texture2D(2, 2);
            pixel.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); pixel.Apply();
            File.WriteAllBytes(Root + "/Pixel.png", pixel.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(pixel);
            AssetDatabase.ImportAsset(Root + "/Pixel.png", ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "/Pixel.png");
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 2;
            importer.filterMode = FilterMode.Point; importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Pixel.png");
            var actor = Sprite("Input feedback actor", sprite, material, new Vector3(0, -1.5f), new Vector3(.6f, 1, 1), new Color(.2f, .8f, .7f));
            Sprite("Ground", sprite, material, new Vector3(0, -2.1f), new Vector3(16, .2f, 1), new Color(.3f, .4f, .5f));
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = events.GetComponent<InputSystemUIInputModule>();
            module.actionsAsset = asset;
            module.point = Reference(asset, "Point");
            module.leftClick = Reference(asset, "Click");
            module.scrollWheel = Reference(asset, "ScrollWheel");
            module.submit = Reference(asset, "Submit");
            module.cancel = Reference(asset, "Cancel");
            module.move = Reference(asset, "Navigate");
            var host = new GameObject("Input Actions Sample");
            var player = host.AddComponent<PlayerInput>();
            player.actions = asset; player.defaultActionMap = "Player";
            player.notificationBehavior = PlayerNotifications.InvokeCSharpEvents; player.uiInputModule = module;
            var demo = host.AddComponent<InputActionsSample>(); demo.Player = player; demo.Actor = actor.transform;
            var canvas = new GameObject("Sample UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1100, 700);
            demo.Status = Label(canvas.transform, "Status", "YYGC Input Actions", new Vector2(550, 620), new Vector2(1000, 110), 20);
            demo.BindingLabel = Label(canvas.transform, "Binding", "Jump: Space", new Vector2(550, 535), new Vector2(1000, 35), 22);
            demo.ModeButton = Button(canvas.transform, "Switch mode", new Vector2(165, 455));
            demo.ModalButton = Button(canvas.transform, "Open modal", new Vector2(420, 455));
            demo.SaveButton = Button(canvas.transform, "Save bindings", new Vector2(675, 455));
            demo.LoadButton = Button(canvas.transform, "Load bindings", new Vector2(930, 455));
            demo.ModalPanel = new GameObject("Modal", typeof(RectTransform), typeof(Image));
            demo.ModalPanel.transform.SetParent(canvas.transform, false);
            var panel = demo.ModalPanel.GetComponent<RectTransform>(); panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero; demo.ModalPanel.GetComponent<Image>().color = new Color(.05f, .1f, .15f, .96f);
            Label(panel, "Instructions", "Gameplay is blocked. Rebind Jump, then Save after closing.\nPress Escape to cancel an active rebind.", new Vector2(550, 430), new Vector2(1000, 110), 23);
            demo.RebindButton = Button(panel, "Rebind Jump", new Vector2(300, 300));
            demo.CancelButton = Button(panel, "Cancel rebind", new Vector2(550, 300));
            demo.CloseButton = Button(panel, "Close modal", new Vector2(800, 300));
            demo.ModalPanel.SetActive(false);
            EditorSceneManager.SaveScene(scene, Root + "/InputActions.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("YYGC_INPUT_SAMPLE=" + Root + "/InputActions.unity");
        }

        private static InputActionReference Reference(InputActionAsset asset, string name)
        {
            var value = InputActionReference.Create(asset.FindAction("UI/" + name, true));
            AssetDatabase.CreateAsset(value, Root + "/UI_" + name + ".asset");
            return value;
        }
        private static GameObject Sprite(string name, Sprite sprite, Material material, Vector3 position, Vector3 scale, Color color)
        {
            var value = new GameObject(name, typeof(SpriteRenderer));
            value.transform.position = position; value.transform.localScale = scale;
            var renderer = value.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.color = color;
            return value;
        }
        private static Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size, int fontSize)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(Text)); value.transform.SetParent(parent, false);
            var rect = value.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var label = value.GetComponent<Text>(); label.text = text; label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize; label.alignment = TextAnchor.MiddleCenter; label.color = Color.white; label.raycastTarget = false;
            return label;
        }
        private static Button Button(Transform parent, string title, Vector2 position)
        {
            var label = Label(parent, title, title, position, new Vector2(230, 54), 20);
            var root = label.gameObject; var textRect = root.GetComponent<RectTransform>();
            var buttonObject = new GameObject(title + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.anchoredPosition = position; rect.sizeDelta = textRect.sizeDelta;
            buttonObject.GetComponent<Image>().color = new Color(.13f, .29f, .38f);
            label.transform.SetParent(buttonObject.transform, false); textRect.anchoredPosition = Vector2.zero;
            textRect.anchorMin = textRect.anchorMax = new Vector2(.5f, .5f);
            return buttonObject.GetComponent<Button>();
        }
    }
}
