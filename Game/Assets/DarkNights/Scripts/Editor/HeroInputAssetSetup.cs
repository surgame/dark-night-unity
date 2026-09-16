using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkNights.Editor
{
    /// <summary>
    /// 首次创建正式玩法动作资产，沿用原模板 Player／UI 和 Move／Jump／Attack／Crouch 命名。
    /// 只写新的 Input 目录，既有资产与绑定身份不覆盖；之后由 Unity Input Actions 编辑器维护。
    /// </summary>
    public static class HeroInputAssetSetup
    {
        public const string Path = "Assets/DarkNights/Res/Input/Gameplay.inputactions";
        public static InputActionAsset Create()
        {
            if (File.Exists(Path)) throw new InvalidOperationException("Gameplay input already exists; edit it in the Input Actions editor.");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var asset = InputActionAsset.FromJson(source.ToJson());
            try
            {
                var player = asset.FindActionMap("Player", true);
                var move = player.FindAction("Move", true);
                Clear(move); move.expectedControlType = "Axis";
                move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
                Set(player.FindAction("Jump", true), "<Keyboard>/space");
                Set(player.FindAction("Attack", true), "<Mouse>/leftButton");
                Set(player.FindAction("Crouch", true), "<Keyboard>/s");
                Set(player.FindAction("Interact", true), "<Keyboard>/w");
                for (int i = 1; i <= 3; i++) player.AddAction("Item" + i, InputActionType.Button, "<Keyboard>/" + i);
                player.AddAction("ToggleMode", InputActionType.Button, "<Keyboard>/tab");
                var camp = asset.AddActionMap("Camp");
                camp.AddAction("Move", InputActionType.Value, expectedControlLayout: "Axis")
                    .AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
                foreach (var pair in new[] {
                    ("Select", "<Mouse>/leftButton"), ("Orders", "<Mouse>/rightButton"), ("Append", "<Keyboard>/leftShift"),
                    ("Pan", "<Mouse>/middleButton"), ("Pause", "<Keyboard>/p"), ("Help", "<Keyboard>/h"),
                    ("Save", "<Keyboard>/f5"), ("Load", "<Keyboard>/f9"), ("Home", "<Keyboard>/home"),
                    ("Guards", "<Keyboard>/g"), ("IdleWorkers", "<Keyboard>/i"), ("ToggleMode", "<Keyboard>/tab") })
                    camp.AddAction(pair.Item1, InputActionType.Button, pair.Item2);
                camp.FindAction("Append").AddBinding("<Keyboard>/rightShift");
                camp.AddAction("PanDelta", InputActionType.PassThrough, "<Mouse>/delta", expectedControlLayout: "Vector2");
                File.WriteAllText(Path, asset.ToJson());
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(Path);
        }
        private static void Set(InputAction action, string binding) { Clear(action); action.AddBinding(binding); }
        private static void Clear(InputAction action)
        {
            while (action.bindings.Count > 0) action.ChangeBinding(0).Erase();
        }
    }
}
