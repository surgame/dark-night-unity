using System;
using System.IO;
using System.Linq;
using DarkNights.Editor;
using DarkNights.Runtime.Framework;
using DarkNights.View;
using GameCore.Objects.Definition;
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
            var map = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(FormalObjectContentSetup.SessionDefinitionPath)
                .SharedConfigs.OfType<ContentDefinitionMap>().Single();
            Assert.That(map.Entries.Count, Is.EqualTo(15));
            foreach (JObject spec in Input()["visuals"])
            {
                string name = (string)spec["name"];
                ObjectDefinition definition = map.GetRequired(NativeArtSetup.ContentId(name), database);
                Assert.That(definition.isLocal && definition.Id == 0 && !definition.Guid.IsEmpty);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(definition.PrefabRef.AssetGUID));
                Assert.That(prefab.GetComponent<ObjectView>().Get<NativeVisual>("visual"), Is.Not.Null);
            }
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
                                    Assert.That(art.GetComponent<SpriteRenderer>().sprite, Is.SameAs(NativeAnimationBuilder.Sprite((string)values[i])), context);
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
