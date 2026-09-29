using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DarkNights.Editor;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;
using NUnit.Framework;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>接口配置的真实 Unity 序列化与 Odin 属性树验收；只写本批独立证据资产，正式 WorldSession 保持不变。</summary>
    public sealed class TerrainModifierAuthoringTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Call(ExpeditionFlowDraft draft, string method, params object[] args)
        {
            try { typeof(ExpeditionFlowDraft).GetMethod(method, Private).Invoke(draft, args); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }

        [Test]
        public void NestedInterfaceAssetSurvivesSaveUnloadAndReimport()
        {
            string path = "Assets/TerrainModifierVerification-" + Guid.NewGuid().ToString("N") + ".asset";
            var source = ScriptableObject.CreateInstance<ObjectDefinition>();
            var config = new ExpeditionFlowConfig();
            config.Modifiers = new List<ITerrainGenerationModifierConfig>
            {
                new EntranceWalkwayModifierConfig { Enabled = false, Headroom = 7, MaxCandidates = 17 },
                new EntranceWalkwayModifierConfig { Enabled = true, Headroom = 4, MaxCandidates = 31 }
            };
            source.SharedConfigs.Add(config); AssetDatabase.CreateAsset(source, path); AssetDatabase.SaveAssetIfDirty(source);
            string identity = config.Fingerprint();
            Resources.UnloadAsset(source);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            source = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(path);
            var restored = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            Assert.That(restored.Fingerprint(), Is.EqualTo(identity));
            Assert.That(restored.Modifiers, Has.Count.EqualTo(2));
            Assert.That(restored.Modifiers[0], Is.TypeOf<EntranceWalkwayModifierConfig>());
            Assert.That(((EntranceWalkwayModifierConfig)restored.Modifiers[0]).Enabled, Is.False);
            Assert.That(((EntranceWalkwayModifierConfig)restored.Modifiers[0]).Headroom, Is.EqualTo(7));
            Assert.That(restored.Modifiers[1], Is.Not.SameAs(restored.Modifiers[0]));
            TestContext.WriteLine("Evidence asset: " + path);
        }

        [Test]
        public void OdinResolvesInterfaceListAndUnityTypeSelectionSupportsUndo()
        {
            var draft = ScriptableObject.CreateInstance<ExpeditionFlowDraft>();
            try
            {
                using var serialized = new SerializedObject(draft);
                using var tree = PropertyTree.Create(serialized);
                tree.UpdateTree();
                var list = tree.GetPropertyAtPath("Config.Modifiers");
                Assert.That(list, Is.Not.Null); Assert.That(list.Children.Count, Is.EqualTo(1));
                Assert.That(list.Children[0].ValueEntry.WeakSmartValue, Is.TypeOf<EntranceWalkwayModifierConfig>());
                var entry = serialized.FindProperty("Config.Modifiers").GetArrayElementAtIndex(0);
                Assert.That(entry.propertyType, Is.EqualTo(SerializedPropertyType.ManagedReference));
                Undo.IncrementCurrentGroup(); Undo.RecordObject(draft, "选择接口类型");
                entry.managedReferenceValue = new EntranceWalkwayModifierConfig { Enabled = false, Headroom = 7 };
                serialized.ApplyModifiedProperties(); Undo.FlushUndoRecordObjects();
                Assert.That(((EntranceWalkwayModifierConfig)draft.Config.Modifiers[0]).Headroom, Is.EqualTo(7));
                Undo.PerformUndo();
                Assert.That(((EntranceWalkwayModifierConfig)draft.Config.Modifiers[0]).Headroom, Is.EqualTo(5));
                Undo.PerformRedo();
                Assert.That(((EntranceWalkwayModifierConfig)draft.Config.Modifiers[0]).Enabled, Is.False);
            }
            finally { Undo.ClearUndo(draft); UnityEngine.Object.DestroyImmediate(draft); }
        }

        [Test]
        public void DraftCopyApplyCancelAndConflictKeepInterfaceObjectsIndependent()
        {
            var source = ScriptableObject.CreateInstance<ObjectDefinition>();
            var draft = ScriptableObject.CreateInstance<ExpeditionFlowDraft>();
            var config = new ExpeditionFlowConfig(); source.SharedConfigs.Add(config);
            try
            {
                Call(draft, "Load", source);
                Assert.That(draft.Config.Modifiers[0], Is.Not.SameAs(config.Modifiers[0]));
                ((EntranceWalkwayModifierConfig)draft.Config.Modifiers[0]).Headroom = 7;
                Assert.That(((EntranceWalkwayModifierConfig)config.Modifiers[0]).Headroom, Is.EqualTo(5));
                Call(draft, "Apply");
                config = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
                Assert.That(((EntranceWalkwayModifierConfig)config.Modifiers[0]).Headroom, Is.EqualTo(7));
                ((EntranceWalkwayModifierConfig)draft.Config.Modifiers[0]).Enabled = false;
                Call(draft, "Load", source);
                Assert.That(((EntranceWalkwayModifierConfig)draft.Config.Modifiers[0]).Enabled, Is.True);
                ((EntranceWalkwayModifierConfig)draft.Config.Modifiers[0]).MaxCandidates = 12;
                ((EntranceWalkwayModifierConfig)config.Modifiers[0]).Headroom = 6;
                Assert.Throws<InvalidOperationException>(() => Call(draft, "Apply"));
            }
            finally { Undo.ClearUndo(source); UnityEngine.Object.DestroyImmediate(draft); UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void FingerprintTracksOrderParametersAndMissingEntriesAreRejected()
        {
            var config = new ExpeditionFlowConfig(); string original = config.Fingerprint();
            ((EntranceWalkwayModifierConfig)config.Modifiers[0]).Enabled = false;
            Assert.That(config.Fingerprint(), Is.Not.EqualTo(original));
            config.Modifiers.Add(new EntranceWalkwayModifierConfig { Headroom = 7 });
            string ordered = config.Fingerprint(); config.Modifiers.Reverse();
            Assert.That(config.Fingerprint(), Is.Not.EqualTo(ordered));
            config.Modifiers[0] = null; Assert.Throws<InvalidOperationException>(() => config.Validate());
            config.Modifiers.Clear(); Assert.DoesNotThrow(() => config.Validate());
            config.ModifierSchemaVersion = 0;
            Assert.That(config.CopyModifiers().Single(), Is.TypeOf<EntranceWalkwayModifierConfig>());
        }
    }
}
