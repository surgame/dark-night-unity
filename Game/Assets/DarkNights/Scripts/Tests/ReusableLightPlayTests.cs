using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Entry;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using DarkNights.View;
using GameCore.Objects.Definition;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>正式 Bootstrap 的短时照明装配探针；真实客户端请求验证占格、开关、切工具和移除，不修改作者场景或权威副本。</summary>
    public sealed class ReusableLightPlayTests
    {
        private const string SceneKey = "DarkNights.ReusableLightPlay.Scene";
        [UnityTest]
        public IEnumerator BootstrapInventoryMountToggleAndRemove()
        {
            SessionState.SetString(SceneKey, EditorSceneManager.GetActiveScene().path);
            EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
            yield return new EnterPlayMode();
            yield return UniTask.ToCoroutine(Verify);
            yield return new ExitPlayMode();
        }

        private static async UniTask Verify()
        {
            SessionNetwork network = null;
            await Until(() => (network = UnityEngine.Object.FindAnyObjectByType<SessionNetwork>()) != null &&
                MenuAvailable(network), "正式菜单初始化");
            await UniTask.Yield();
            var ui = network.GetComponent<SessionUiController>();
            ui.ActivateButton("MainMenu", "NewGame");
            var hero = network.GetComponent<HeroPlayerController>();
            await Until(() => network.Client.Ready && hero.Current != null, "地面人物Ready");
            Assert.That(hero.Current.Slot0, Is.EqualTo(4));
            Assert.That(hero.Current.Slot0Definition, Is.EqualTo(hero.Current.LightDefinition));
            var stage = UnityEngine.Object.FindAnyObjectByType<PinewatchStage>();
            FlashlightView tool = null;
            var target = new RenderTexture(640, 360, 0, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(640, 360, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            void Render(string name = null)
            {
                RenderPipeline.SubmitRenderRequest(stage.SceneCamera, new RenderPipeline.StandardRequest { destination = target });
                if (name == null) return;
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); pixels.Apply();
                File.WriteAllBytes("../artifacts/reusable-light-20261009/" + name + ".png", pixels.EncodeToPNG());
            }
            try
            {
                await Until(() => { Render(); tool = UnityEngine.Object.FindObjectsByType<FlashlightView>(FindObjectsSortMode.None)
                    .FirstOrDefault(view => view.gameObject.activeInHierarchy); return tool != null; }, "正式手电实例装配");
                Assert.That(tool.Effect.IsOn, Is.True); Assert.That(tool.Effect.LocalFill.Targets, Is.Not.Empty);
                Render("bootstrap-light-on");
                var block = new MaterialPropertyBlock(); tool.Effect.LocalFill.Targets[0].GetPropertyBlock(block);
                Assert.That(block.GetFloat("_DNLocalFillCount"), Is.GreaterThan(0));
                await network.Client.Send(SessionOperation.SetHeroLight, new[] { hero.Current.Id }, value: 0, controlLease: hero.Current.ControlLease);
                await Until(() => !hero.Current.LightEnabled, "可信关灯"); Render("bootstrap-light-off");
                Assert.That(tool.Effect.IsOn, Is.False);
                tool.Effect.LocalFill.Targets[0].GetPropertyBlock(block); Assert.That(block.GetFloat("_DNLocalFillCount"), Is.Zero);
                await network.Client.Send(SessionOperation.SetHeroLight, new[] { hero.Current.Id }, value: 1, controlLease: hero.Current.ControlLease);
                await Until(() => hero.Current.LightEnabled, "可信开灯");
                await network.Client.Send(SessionOperation.DebugSetEnabled, value: 1);
                string pickaxe = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("item.pickaxe").Guid.ToString();
                await network.Client.Send(SessionOperation.DebugGiveEquipment, new[] { hero.Current.Id },
                    kind: pickaxe, value: hero.Current.InventoryRevision, x: 1);
                await Until(() => hero.Current.Slot1 == 2, "实际添加矿镐");
                await network.Client.Send(SessionOperation.SelectHeroItem, new[] { hero.Current.Id }, value: 1, controlLease: hero.Current.ControlLease);
                await Until(() => hero.Current.SelectedItem == 1, "切换矿镐"); Render(); Assert.That(tool.Effect.IsOn, Is.True);
                await network.Client.Send(SessionOperation.DebugRemoveEquipment, new[] { hero.Current.Id },
                    kind: hero.Current.LightDefinition, value: hero.Current.InventoryRevision);
                await Until(() => hero.Current.LightDefinition == "" && hero.Current.Slot0 == 0, "实际移除手电");
                Render(); Assert.That(tool == null || !tool.gameObject.activeInHierarchy, Is.True);
                network.Disconnect();
            }
            finally
            {
                RenderTexture.active = previous; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels);
                if (network != null) network.Disconnect();
            }
        }

        private static async UniTask Until(Func<bool> predicate, string stage)
        {
            double end = Time.realtimeSinceStartupAsDouble + 25;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < end) await UniTask.Delay(50);
            Assert.That(predicate(), Is.True, stage);
        }

        private static bool MenuAvailable(SessionNetwork network)
        {
            var ui = network.GetComponent<SessionUiController>();
            return ui != null && (bool)typeof(SessionUiController).GetField("initialized",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ui);
        }

        [UnityTearDown]
        public IEnumerator RestoreScene()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            string path = SessionState.GetString(SceneKey, "");
            if (path.Length > 0 && File.Exists(path)) EditorSceneManager.OpenScene(path);
        }
    }
}
