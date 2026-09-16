using System;
using System.IO;
using System.Text.RegularExpressions;
using GameCore.PlayerInputs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace GameCore.Samples.InputActions.Tests
{
    /// <summary>
    /// 使用真实文件与 InputAction 验证设置往返、失败原子性和版本边界。
    /// 文件只位于每例独立临时目录，写入失败通过真实文件锁制造，不替换文件系统。
    /// </summary>
    public sealed class InputSettingsTests
    {
        private string directory, path;
        private InputActionAsset asset;
        private InputAction jump;
        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "yy-input-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); path = Path.Combine(directory, "settings.json");
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            jump = asset.AddActionMap("Player").AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            jump.ApplyBindingOverride(0, "<Keyboard>/j");
        }
        [TearDown]
        public void TearDown()
        {
            asset.Disable(); UnityEngine.Object.DestroyImmediate(asset);
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }

        [Test]
        public void ExistingFormatRoundTripsByBindingIdentity()
        {
            Assert.That(YYInputSettingsStore.SaveBindingOverrides(asset, path), Is.True);
            jump.RemoveAllBindingOverrides();
            Assert.That(YYInputSettingsStore.TryApplyBindingOverrides(asset, path), Is.True);
            Assert.That(jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/j"));
            Assert.That(YYInputSettingsStore.LoadOrDefault(path).Version, Is.EqualTo(1));
        }

        [TestCase("{")]
        [TestCase("{}")]
        public void InvalidNestedJsonKeepsCurrentOverrides(string malformed)
        {
            File.WriteAllText(path, JsonUtility.ToJson(new YYInputSettingsData { BindingOverridesJson = malformed }));
            LogAssert.Expect(LogType.Warning, new Regex("Failed to apply binding overrides"));
            Assert.That(YYInputSettingsStore.TryApplyBindingOverrides(asset, path), Is.False);
            Assert.That(jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/j"));
        }

        [Test]
        public void UnsupportedVersionDoesNotApplyOrRewriteFile()
        {
            string original = "{\"Version\":999,\"BindingOverridesJson\":\"{}\",\"LastSavedUtcTicks\":0}";
            File.WriteAllText(path, original);
            LogAssert.Expect(LogType.Warning, new Regex("Failed to load settings"));
            Assert.That(YYInputSettingsStore.TryApplyBindingOverrides(asset, path), Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(original));
            Assert.That(jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/j"));
        }

        [TestCase("{}")]
        [TestCase("{\"Version\":1}")]
        [TestCase("[]")]
        [TestCase("null")]
        public void MissingEnvelopeFieldsCannotClearLiveBindings(string malformed)
        {
            File.WriteAllText(path, malformed);
            LogAssert.Expect(LogType.Warning, new Regex("Failed to load settings"));
            Assert.That(YYInputSettingsStore.TryApplyBindingOverrides(asset, path), Is.False);
            Assert.That(jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/j"));
            Assert.That(File.ReadAllText(path), Is.EqualTo(malformed));
        }

        [Test]
        public void FailedAtomicReplacementKeepsPreviousBytesAndTimestamp()
        {
            Assert.That(YYInputSettingsStore.SaveBindingOverrides(asset, path), Is.True);
            byte[] original = File.ReadAllBytes(path);
            var next = new YYInputSettingsData { BindingOverridesJson = "{}", LastSavedUtcTicks = 123 };
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                LogAssert.Expect(LogType.Error, new Regex("Failed to save settings"));
                Assert.That(YYInputSettingsStore.Save(next, path), Is.False);
            }
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(next.LastSavedUtcTicks, Is.EqualTo(123));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }

        [Test]
        public void FailedClearKeepsCurrentBindings()
        {
            Assert.That(YYInputSettingsStore.SaveBindingOverrides(asset, path), Is.True);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                LogAssert.Expect(LogType.Error, new Regex("Failed to save settings"));
                Assert.That(YYInputSettingsStore.ClearBindingOverrides(asset, path), Is.False);
            }
            Assert.That(jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/j"));
        }

        [Test]
        public void ClearSavesAndRestoresDefaultBinding()
        {
            Assert.That(YYInputSettingsStore.ClearBindingOverrides(asset, path), Is.True);
            Assert.That(jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/space"));
            Assert.That(YYInputSettingsStore.LoadOrDefault(path).BindingOverridesJson, Is.Empty);
        }

        [Test]
        public void MissingFileKeepsCurrentBindings()
        {
            Assert.That(YYInputSettingsStore.TryApplyBindingOverrides(asset, path), Is.True);
            Assert.That(jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/j"));
        }

        [Test]
        public void SavedClearResetsOverridesWhenLoaded()
        {
            Assert.That(YYInputSettingsStore.Save(new YYInputSettingsData(), path), Is.True);
            Assert.That(YYInputSettingsStore.TryApplyBindingOverrides(asset, path), Is.True);
            Assert.That(jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/space"));
        }

        [Test]
        public void OversizeFileDoesNotClearCurrentBindings()
        {
            File.WriteAllText(path, new string(' ', 4 * 1024 * 1024 + 1));
            LogAssert.Expect(LogType.Warning, new Regex("Failed to load settings"));
            Assert.That(YYInputSettingsStore.TryApplyBindingOverrides(asset, path), Is.False);
            Assert.That(jump.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/j"));
        }
    }
}
