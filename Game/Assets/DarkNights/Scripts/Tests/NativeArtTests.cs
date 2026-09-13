using System;
using System.IO;
using System.Linq;
using DarkNights.Editor;
using DarkNights.Runtime.Framework;
using DarkNights.View;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.Objects.Views;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Tests
{
    /// <summary>
    /// 对照冻结源轨道检验所有原生动画的实际采样、绑定和导入字节；每个 Prefab 保存并重开后再验证。
    /// 不重生成期望值、规则夹具或正式资产，测试加载副本可立即释放。
    /// </summary>
    public sealed class NativeArtTests
    {
        [Test]
        public void FrozenPixelsAndFifteenDefinitionsRemainComplete()
        {
            NativeArtSetup.Validate();
            var database = ObjectDefinitionDatabase.Instance;
            database.RebuildLookup();
            var map = new DefinitionRuleIndex(database);
            Assert.That(map.Count, Is.EqualTo(15));
            foreach (JObject spec in Input()["visuals"])
            {
                string name = (string)spec["name"];
                ObjectDefinition definition = map.GetRequired(NativeArtSetup.ContentId(name));
                Assert.That(definition.isLocal && definition.Id == 0 && !definition.Guid.IsEmpty);
                Assert.That(definition.BehaviourTypes.Count(value => value == CRefactorContentUpgrade.PresentationType(name).FullName), Is.EqualTo(1));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(definition.PrefabRef.AssetGUID));
                var visual = prefab.GetComponent<ObjectView>().Get<NativeVisual>("visual");
                Assert.That(visual, Is.Not.Null);
                // Play／资源重载可以产生不同托管包装；Unity 原生身份仍须指向完全相同的 Sprite。
                Assert.That(visual.Portrait == NativeAnimationBuilder.Sprite((string)spec["portrait"]), Is.True, name);
            }
        }

        [Test]
        public void FifteenPrefabCopiesSaveReopenAndAssemblePassivePresentations()
        {
            string temporary = "Assets/DarkNightsContentRoundTrip";
            Assert.That(Directory.Exists(temporary), Is.False, "Refuse to overwrite an existing round-trip output.");
            AssetDatabase.CreateFolder("Assets", "DarkNightsContentRoundTrip");
            try
            {
                foreach (JObject spec in Input()["visuals"])
                {
                    string name = (string)spec["name"];
                    string source = "Assets/DarkNights/Res/Objects/" + name + "/" + name;
                    string copy = temporary + "/" + name + ".prefab";
                    GameObject root = PrefabUtility.LoadPrefabContents(source + ".prefab");
                    try { Assert.That(PrefabUtility.SaveAsPrefabAsset(root, copy), Is.Not.Null); }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                    root = PrefabUtility.LoadPrefabContents(copy);
                    try
                    {
                        var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(source + ".asset");
                        var view = root.GetComponent<ObjectView>();
                        FormalObjectContentTests.ExpectRegistrationWithoutRuntime(definition.BehaviourTypes.Count);
                        ObjectDefinitionInitialization.Initialize(view.Initializer, definition);
                        var instance = root.GetComponent<ObjectInstance>();
                        var presentation = instance.GetAllBehaviors().OfType<EntityPresentationBehaviour>().Single();
                        Assert.That(presentation.GetType(), Is.EqualTo(CRefactorContentUpgrade.PresentationType(name)), name);
                        Assert.That(presentation.Visual, Is.SameAs(view.Get<NativeVisual>("visual")), name);
                        Assert.That(presentation.IsBound || presentation.IsAvailable, Is.False, name);
                        presentation.Visual.Preview(0, 0);
                        Assert.That(presentation.Id, Is.Zero, name);
                        Assert.That(presentation.Epoch, Is.Zero, name);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
            }
            finally { AssetDatabase.DeleteAsset(temporary); }
        }

        [Test]
        public void EveryNativeClipSamplesFrozenFramesOffsetsAndVisibility()
        {
            int checkedKeys = 0;
            foreach (JObject spec in Input()["visuals"])
            {
                string name = (string)spec["name"];
                GameObject root = PrefabUtility.LoadPrefabContents("Assets/DarkNights/Res/Objects/" + name + "/" + name + ".prefab");
                try
                {
                    NativeVisual visual = root.GetComponent<ObjectView>().Get<NativeVisual>("visual");
                    foreach (JObject source in spec["clips"])
                    {
                        PoseClip clip = visual.Clips.Single(item => item.Name == (string)source["name"]);
                        Assert.That(clip.Duration, Is.EqualTo((double)source["duration"]));
                        Assert.That(AnimationUtility.GetAnimationEvents(clip.Clip), Is.Empty);
                        foreach (JObject track in source["tracks"])
                        {
                            JArray times = (JArray)track["times"];
                            JArray values = (JArray)track["values"];
                            Transform art = root.transform.Find((string)track["path"] + "/Sprite");
                            Assert.That(art, Is.Not.Null);
                            for (int i = 0; i < times.Count; i++)
                            {
                                double end = i + 1 < times.Count ? (double)times[i + 1] : clip.Duration;
                                clip.Sample(root, ((double)times[i] + end) / 2);
                                string property = (string)track["property"];
                                string context = name + "/" + clip.Name + "/" + track["path"] + "/" + i;
                                if (property == "texture")
                                    Assert.That(art.GetComponent<SpriteRenderer>().sprite == NativeAnimationBuilder.Sprite((string)values[i]), Is.True, context);
                                else if (property == "visible")
                                    Assert.That(art.GetComponent<SpriteRenderer>().enabled, Is.EqualTo((bool)values[i]), context);
                                else
                                {
                                    Assert.That(art.localPosition.x, Is.EqualTo((float)values[i][0] / 100).Within(0.000001), context);
                                    Assert.That(art.localPosition.y, Is.EqualTo(-(float)values[i][1] / 100).Within(0.000001), context);
                                }
                                checkedKeys++;
                            }
                        }
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Assert.That(checkedKeys, Is.GreaterThan(500));
            Directory.CreateDirectory("../artifacts/migration");
            File.WriteAllText("../artifacts/migration/native-animation-measurement.json", "{\"visuals\":15,\"clips\":32,\"sampledKeys\":" + checkedKeys + "}");
        }

        private static JObject Input() => JObject.Parse(File.ReadAllText(NativeArtSetup.InputPath));
    }
}
