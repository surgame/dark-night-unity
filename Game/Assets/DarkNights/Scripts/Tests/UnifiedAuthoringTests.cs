using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>
    /// 在专属空临时目录中编辑、保存并重载 Definition 和原生 Prefab，核验制作输入可持久化。
    /// 原资产字节与 GUID 全程保持；测试结束只删除本次创建的临时资产。
    /// </summary>
    [Category("UnifiedSlice")]
    public sealed class UnifiedAuthoringTests
    {
        [Test]
        public void RuleChoiceAndPrefabEditsSurviveSaveAndReopen()
        {
            const string folder = "Assets/UnifiedAuthoringProbe";
            const string source = "Assets/DarkNights/Res/Objects/Worker/Worker.asset";
            const string copy = folder + "/Worker.asset";
            const string prefab = folder + "/Worker.prefab";
            Assert.That(AssetDatabase.IsValidFolder(folder), Is.False, "Test output must start empty.");
            byte[] original = File.ReadAllBytes(source);
            string originalGuid = AssetDatabase.AssetPathToGUID(source);
            AssetDatabase.CreateFolder("Assets", "UnifiedAuthoringProbe");
            try
            {
                Assert.That(AssetDatabase.CopyAsset(source, copy), Is.True);
                ObjectDefinition definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(copy);
                var config = definition.SharedConfigs.OfType<ActorRuleConfig>().Single();
                Assert.That(Editor.RuleKeyDrawer.Family("units").Property("archer"), Is.Not.Null);
                config.RuleKey = "archer";
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
                string json = File.ReadAllText(copy);
                Assert.That(json, Does.Contain("RuleKey: archer"));
                Resources.UnloadAsset(definition);
                definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(copy);
                Assert.That(definition.SharedConfigs.OfType<ActorRuleConfig>().Single().RuleKey, Is.EqualTo("archer"));
                Assert.That(AssetDatabase.CopyAsset(Editor.FormalObjectContentSetup.WorkerPrefabPath, prefab), Is.True);
                GameObject contents = PrefabUtility.LoadPrefabContents(prefab);
                try
                {
                    contents.transform.localScale = new Vector3(1.125f, 1.125f, 1);
                    PrefabUtility.SaveAsPrefabAsset(contents, prefab);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                contents = PrefabUtility.LoadPrefabContents(prefab);
                try
                {
                    Assert.That(contents.transform.localScale, Is.EqualTo(new Vector3(1.125f, 1.125f, 1)));
                    Assert.That(contents.GetComponent<GameCore.Objects.Runner.ObjectInstance>(), Is.Not.Null);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                Assert.That(File.ReadAllBytes(source), Is.EqualTo(original));
                Assert.That(AssetDatabase.AssetPathToGUID(source), Is.EqualTo(originalGuid));
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }
    }
}
