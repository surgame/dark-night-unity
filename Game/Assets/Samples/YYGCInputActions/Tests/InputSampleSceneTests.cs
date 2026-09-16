#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GameCore.Samples.InputActions.Tests
{
    /// <summary>
    /// 打开交付的原生场景验证设备输入、UGUI 阻塞、改键存取与重复启停，并保存真实渲染截图。
    /// 官方测试运行时隔离设备，设置文件使用临时目录；不改原始场景、绑定或玩家设置。
    /// </summary>
    public sealed class InputSampleSceneTests : InputTestFixture
    {
        private Scene scene;
        private string settings;

        [UnityTest]
        public IEnumerator NativeSceneInputUiRebindingAndLifecycle()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            string path = AssetDatabase.FindAssets("InputActions t:Scene").Select(AssetDatabase.GUIDToAssetPath)
                .Single(p => p.EndsWith("/Content/InputActions.unity", StringComparison.Ordinal));
            scene = EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var sample = UnityEngine.Object.FindAnyObjectByType<InputActionsSample>();
            Assert.That(sample, Is.Not.Null);
            settings = Path.Combine(Application.temporaryCachePath, "yy-input-scene-" + Guid.NewGuid().ToString("N") + ".json");
            sample.SettingsFile = settings;
            Assert.That(sample.Player.currentActionMap.name, Is.EqualTo("Player"));
            Assert.That(sample.Player.actions.FindAction("Player/Jump").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/space"));
            // 无前台窗口的 CI 中模拟 Unity 焦点消息；设备和动作仍使用官方测试运行时。
            sample.SendMessage("OnApplicationFocus", true);
            EventSystem.current.SendMessage("OnApplicationFocus", true);
            float x = sample.Actor.position.x;
            Press(keyboard.dKey); yield return new WaitForSeconds(.15f); Release(keyboard.dKey);
            Assert.That(sample.Actor.position.x, Is.GreaterThan(x));
            sample.SendMessage("OnApplicationFocus", false);
            x = sample.Actor.position.x; Press(keyboard.dKey); yield return new WaitForSeconds(.1f);
            Assert.That(sample.Actor.position.x, Is.EqualTo(x), "Focus loss immediately cancels movement.");
            Release(keyboard.dKey); sample.SendMessage("OnApplicationFocus", true); yield return null;
            Press(keyboard.spaceKey); yield return null; Release(keyboard.spaceKey);
            Assert.That(sample.JumpCount, Is.EqualTo(1));
            yield return null;
            Assert.That(sample.Actor.position.y, Is.GreaterThan(-1.5f));
            yield return new WaitForSeconds(1);
            Set(mouse.position, new Vector2(10, 10)); Press(mouse.leftButton); yield return null;
            Release(mouse.leftButton); yield return null;
            Assert.That(sample.UseCount, Is.EqualTo(1));
            Capture(sample, "sample-play.png");
            yield return null; Canvas.ForceUpdateCanvases();
            Set(mouse.position, RectTransformUtility.WorldToScreenPoint(null, sample.ModalButton.transform.position));
            yield return null; Press(mouse.leftButton); yield return null; Release(mouse.leftButton); yield return null;
            Assert.That(sample.ModalPanel.activeSelf, Is.True, "Actual UGUI click opens the modal.");
            Assert.That(sample.UseCount, Is.EqualTo(1), "UI click cannot also use a world item.");
            Press(keyboard.spaceKey); yield return null; Release(keyboard.spaceKey);
            Assert.That(sample.JumpCount, Is.EqualTo(1));
            yield return null;
            Capture(sample, "sample-modal.png");
            sample.RebindButton.onClick.Invoke(); Press(keyboard.kKey); yield return null;
            for (int i = 0; i < 10 && sample.Player.actions.FindAction("Player/Jump").bindings[0].effectivePath != "<Keyboard>/k"; i++)
            { currentTime += .1; yield return null; }
            TestContext.WriteLine("SCENE_REBIND keyboard=" + keyboard.kKey.isPressed + " time=" + currentTime +
                " ui=" + sample.Status.text + " binding=" + sample.BindingLabel.text + " overrides=" + sample.Player.actions.SaveBindingOverridesAsJson());
            Release(keyboard.kKey);
            Assert.That(sample.Player.actions.FindAction("Player/Jump").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/k"));
            sample.CloseButton.onClick.Invoke(); sample.SaveButton.onClick.Invoke();
            Assert.That(File.Exists(settings), Is.True);
            sample.Player.actions.RemoveAllBindingOverrides(); sample.LoadButton.onClick.Invoke();
            Assert.That(sample.Player.actions.FindAction("Player/Jump").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/k"));

            sample.OpenModal(); sample.RebindJump(); sample.enabled = false;
            Assert.That(sample.ModalPanel.activeSelf, Is.False);
            sample.enabled = true; sample.SendMessage("OnApplicationFocus", true); yield return null;
            sample.ModeButton.onClick.Invoke();
            Assert.That(sample.Player.currentActionMap.name, Is.EqualTo("Camp"), "One listener after enable, not two toggles.");
            sample.RebindJump(); sample.CancelRebind(); sample.CloseModal();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator CloseScene()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (settings != null && File.Exists(settings)) File.Delete(settings);
        }

        private static void Capture(InputActionsSample sample, string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-yyInputSampleEvidence");
            if (index < 0 || index + 1 >= args.Length) return;
            string directory = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(directory);
            var camera = sample.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).Single();
            var canvas = sample.Status.canvas;
            var target = new RenderTexture(1100, 700, 24);
            var texture = new Texture2D(1100, 700, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var mode = canvas.renderMode; var previousCamera = canvas.worldCamera;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1100, 700), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(directory, name), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                canvas.renderMode = mode; canvas.worldCamera = previousCamera;
                UnityEngine.Object.Destroy(texture); target.Release(); UnityEngine.Object.Destroy(target);
            }
        }
    }
}
#endif
