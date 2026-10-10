using System.Collections.Generic;
using DarkNights.Core.Config;
using DarkNights.Editor.Lighting;
using DarkNights.Entry;
using DarkNights.Runtime.Objects;
using DarkNights.View.Lighting;
using GameCore.Objects.Definition;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkNights.Tests
{
    /// <summary>预设、Definition 覆盖和光效自动装配的合同回归；使用真实模板及临时对象，覆盖换预设、原生 Undo、上下文隔离和退休清理。</summary>
    public sealed class LightProfileTests
    {
        private LightProfile preset;
        private ObjectDefinition definition;
        private FlashlightDebugUndo history;

        [SetUp]
        public void SetUp()
        {
            preset = ScriptableObject.CreateInstance<LightProfile>();
            preset.Template = AssetDatabase.LoadAssetAtPath<GameObject>(Editor.ReusableLightContentSetup.EffectPath).GetComponent<LightEffect>();
            definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            var actual = AssetDatabase.LoadAssetAtPath<LightProfile>(LightProfileMigration.FlashlightPath);
            Assert.That(actual, Is.Not.Null, "先完成持久光照资产迁移。");
            definition.SharedConfigs.Add(new FlashlightToolConfig
            { Profile = new AssetReference(AssetDatabase.AssetPathToGUID(LightProfileMigration.FlashlightPath)) });
            history = new FlashlightDebugUndo();
        }

        [TearDown]
        public void TearDown()
        {
            history.EndGesture(); Undo.ClearUndo(definition); Undo.ClearUndo(preset);
            Object.DestroyImmediate(definition); Object.DestroyImmediate(preset);
        }

        [Test]
        public void UnoverriddenValuesFollowPresetWhileOverridesStayIndependent()
        {
            var overrides = new LightOverrides { Mask = LightOverrideMask.Intensity, Intensity = 2 };
            var first = LightProfileResolver.Resolve(preset, overrides);
            preset.Range = 20; preset.Intensity = 3;
            var second = LightProfileResolver.Resolve(preset, overrides);
            Assert.That(first.Rules.Range, Is.EqualTo(14), "已冻结参数不能随资产修改。");
            Assert.That(second.Rules.Range, Is.EqualTo(20)); Assert.That(second.Rules.Intensity, Is.EqualTo(2));
            Assert.That(preset.Intensity, Is.EqualTo(3), "物体覆盖不能反写共享预设。");
            overrides.Mask = LightOverrideMask.None;
            Assert.That(LightProfileResolver.Resolve(preset, overrides).Rules.Intensity, Is.EqualTo(3));
        }

        [Test]
        public void DefinitionEnablesOverrideFromPresetAndUndoReturnsToInheritance()
        {
            float inherited = LightDefinitionEditor.Preset(definition).Intensity;
            LightDefinitionEditor.EnableOverride(definition, "Intensity", true, history);
            Assert.That(LightDefinitionEditor.Config(definition).Overrides.Intensity, Is.EqualTo(inherited));
            LightDefinitionEditor.SetValue(definition, "Intensity", 2, history);
            Undo.PerformUndo();
            Assert.That(LightDefinitionEditor.Config(definition).Overrides.Intensity, Is.EqualTo(inherited));
            Undo.PerformUndo();
            Assert.That(LightDefinitionEditor.Config(definition).Overrides.Mask, Is.EqualTo(LightOverrideMask.None));
            Undo.PerformRedo(); Undo.PerformRedo();
            Assert.That(LightDefinitionEditor.Config(definition).Overrides.Intensity, Is.EqualTo(2));
        }

        [Test]
        public void SwitchingPresetKeepsOverridesAndStarterAndCanBeUndone()
        {
            LightDefinitionEditor.EnableOverride(definition, "Range", true, history);
            LightDefinitionEditor.SetValue(definition, "Range", 20, history);
            var before = LightDefinitionEditor.Config(definition).Profile.AssetGUID;
            var device = AssetDatabase.LoadAssetAtPath<LightProfile>(LightProfileMigration.DevicePath);
            LightDefinitionEditor.SelectPreset(definition, device, history);
            var config = LightDefinitionEditor.Config(definition);
            var resolved = LightProfileResolver.Resolve(device, config.Overrides);
            Assert.That(resolved.Rules.Range, Is.EqualTo(20));
            Assert.That(resolved.Rules.Intensity, Is.EqualTo(device.Intensity));
            Assert.That(config.Starter, Is.True);
            Undo.PerformUndo();
            Assert.That(LightDefinitionEditor.Config(definition).Profile.AssetGUID, Is.EqualTo(before));
            Assert.That(LightDefinitionEditor.Config(definition).Overrides.Range, Is.EqualTo(20));
        }

        [Test]
        public void NoOpDoesNotDropDefinitionRedo()
        {
            LightDefinitionEditor.EnableOverride(definition, "Range", true, history);
            LightDefinitionEditor.SetValue(definition, "Range", 20, history);
            Undo.PerformUndo();
            var copy = LightDefinitionEditor.Copy(LightDefinitionEditor.Config(definition));
            Assert.That(LightDefinitionEditor.Apply(definition, copy, history), Is.False);
            Undo.PerformRedo();
            Assert.That(LightDefinitionEditor.Config(definition).Overrides.Range, Is.EqualTo(20));
        }

        [Test]
        public void EachLightKeepsItsOwnShadowAndFeatherParameters()
        {
            var overrides = new LightOverrides { Mask = LightOverrideMask.SoftShadows | LightOverrideMask.ConeFeather,
                SoftShadows = false, ConeFeather = .2f };
            var inherited = preset.Freeze();
            var changed = LightProfileResolver.Resolve(preset, overrides);
            Assert.That(inherited.Rules.SoftShadows, Is.True);
            Assert.That(changed.Rules.SoftShadows, Is.False);
            Assert.That(changed.Rules.ConeFeather, Is.EqualTo(.2f));
            Assert.That(changed.Rules.Fingerprint, Is.Not.EqualTo(inherited.Rules.Fingerprint));
        }

        [Test]
        public void AutomaticBindingReusesOneInstanceAndRetiresBeforeTemplateReplacement()
        {
            var root = new GameObject("light binding probe");
            var binding = new LightEffectBinding();
            var second = Object.Instantiate(preset);
            second.Template = AssetDatabase.LoadAssetAtPath<GameObject>(Editor.ReusableLightContentSetup.HeadPath).GetComponent<LightEffect>();
            var active = new List<LightEffect>();
            try
            {
                root.transform.position = new Vector3(3, 4, 0);
                root.transform.rotation = Quaternion.Euler(0, 0, 90);
                var frozen = preset.Freeze();
                binding.Bind(frozen, preset, root.transform, root.transform, root.transform, System.Array.Empty<SpriteRenderer>());
                binding.SetOn(true); var first = binding.Effect;
                binding.Bind(frozen, preset, root.transform, root.transform, root.transform, System.Array.Empty<SpriteRenderer>());
                Assert.That(binding.Effect, Is.SameAs(first));
                Assert.That(root.GetComponentsInChildren<LightEffect>(), Has.Length.EqualTo(1));
                Assert.That(first.Environment.Sample(null).Angle, Is.EqualTo(90).Within(.001));
                LightEffect.Collect(second, null, active); Assert.That(active, Is.Empty);
                binding.Bind(second.Freeze(), second, root.transform, root.transform, root.transform, System.Array.Empty<SpriteRenderer>());
                Assert.That(first == null, Is.True);
                Assert.That(root.GetComponentsInChildren<LightEffect>(), Has.Length.EqualTo(1));
                LightEffect.Collect(preset, null, active); Assert.That(active, Is.Empty);
                binding.Dispose();
                LightEffect.Collect(second, null, active); Assert.That(active, Is.Empty);
                Assert.That(root.GetComponentsInChildren<LightEffect>(), Is.Empty);
            }
            finally { binding.Dispose(); Object.DestroyImmediate(root); Object.DestroyImmediate(second); }
        }

        [Test]
        public void MigratedDefinitionUsesPresetAndPrefabContainsOnlyTheMount()
        {
            LightProfileMigration.Validate();
            var actual = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(Editor.FlashlightContentSetup.DefinitionPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(actual.PrefabRef.AssetGUID));
            Assert.That(prefab.GetComponentsInChildren<LightEffect>(true), Is.Empty);
            Assert.That(prefab.GetComponent<View.FlashlightView>().Emitter.localPosition, Is.EqualTo(new Vector3(.05f, 0, 0)));
            using (var serialized = new SerializedObject(actual))
                Assert.That(serialized.FindProperty("SharedConfigs").arraySize, Is.GreaterThan(0));
        }
    }
}
