using System.Linq;
using DarkNights.Core.ViewData;
using DarkNights.Editor;
using DarkNights.View;
using GameCore.Objects.Definition;
using GameCore.UI.UGUI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using YY.Features.Players.View;

namespace DarkNights.Tests
{
    /// <summary>
    /// 重开原生效果资产检查定义、绑定、源纹理和几何；生命周期与去重另由共享规则及 Play 探针验收。
    /// 测试只在临时 Prefab 内容上采样，不保存表现年龄或覆盖人工资产。
    /// </summary>
    public sealed class NativeEffectTests
    {
        [Test]
        public void EffectsReopenWithExplicitBindingsAndSourceGeometry()
        {
            foreach (string name in new[] { "Arrow", "Floating", "Command", "Audio" })
            {
                string folder = NativeEffectsSetup.Root + "/" + name + "/" + name;
                var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(folder + ".asset");
                Assert.AreEqual(AssetDatabase.AssetPathToGUID(folder + ".prefab"), definition.PrefabRef.AssetGUID);
                var root = PrefabUtility.LoadPrefabContents(folder + ".prefab");
                try
                {
                    var view = root.GetComponent<ObjectView>();
                    if (name == "Audio")
                    {
                        Assert.NotNull(view.Get<CampAudio>("audio"));
                        var audio = new SerializedObject(view.Get<CampAudio>("audio"));
                        Assert.AreEqual(8, audio.FindProperty("clips").arraySize);
                        for (int i = 0; i < 8; i++) Assert.NotNull(audio.FindProperty("clips").GetArrayElementAtIndex(i).objectReferenceValue);
                        var music = (AudioSource)audio.FindProperty("music").objectReferenceValue;
                        Assert.AreEqual("snd_vindsvept_hollow", music.clip.name);
                        Assert.That(music.volume, Is.EqualTo(Mathf.Pow(10, -23f / 20)).Within(0.00001));
                        Assert.IsTrue(music.loop);
                        continue;
                    }
                    NativeEffect effect = view.Get<NativeEffect>("effect"); Assert.NotNull(effect);
                    if (name == "Arrow")
                    {
                        effect.Present(new ProjectileViewData(1, 100, 300, 200, 300, 0.5, 1), 0.5, 320);
                        Assert.That(root.transform.position.x, Is.EqualTo(1.5).Within(0.00001));
                        Assert.That(root.transform.position.y, Is.EqualTo(0.3).Within(0.00001));
                        Assert.That(root.transform.eulerAngles.z, Is.EqualTo(0).Within(0.00001));
                        Assert.AreEqual("spr_archer_arrow_0", root.GetComponentInChildren<SpriteRenderer>().sprite.name);
                    }
                    else if (name == "Command")
                    {
                        effect.Present(new VisualCue("command", 200, 320), 0.4, 320);
                        var ring = root.GetComponentInChildren<LineRenderer>();
                        Assert.That(ring.GetPosition(0).x, Is.EqualTo(0.09).Within(0.00001));
                        Assert.That(ring.startColor.a, Is.EqualTo(0.5).Within(1.0 / 255));
                        Assert.AreEqual(20, ring.positionCount);
                    }
                    else
                    {
                        effect.Present(new VisualCue("resource", 200, 298, "+2", "wood"), 1.35, 320);
                        var label = root.GetComponentInChildren<Text>();
                        Assert.AreEqual("+2", label.text);
                        Assert.AreEqual(7, label.fontSize);
                        Assert.That(label.color.a, Is.EqualTo(0.5).Within(0.00001));
                        var point = label.rectTransform.TransformPoint(Vector3.zero);
                        Assert.That(point.x, Is.EqualTo(2 + (-5 + Mathf.Sin(200 * 3.7f + 298) * 5) / 100).Within(0.0001));
                        Assert.That(point.y, Is.EqualTo(0.22 + 0.135).Within(0.0001));
                        Assert.AreEqual("spr_int_resources_2", root.GetComponentInChildren<Image>().sprite.name);
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        [Test]
        public void HudFadesRemainBoundAfterReopening()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NativeUiSetup.Root + "/Chrome/Chrome.prefab");
            var view = prefab.GetComponent<UGUIView>();
            foreach (string key in new[] { "ToastFade", "BannerFade" })
            {
                Assert.AreEqual(1, view.Bindings.Count(b => b.Key == key));
                Assert.NotNull(view.Get<CanvasGroup>(key));
                Assert.IsFalse(view.Get<CanvasGroup>(key).blocksRaycasts);
            }
        }
    }
}
