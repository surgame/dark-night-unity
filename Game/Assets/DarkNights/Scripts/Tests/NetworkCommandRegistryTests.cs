using System;
using System.Linq;
using System.Reflection;
using DarkNights.Runtime.Network;
using GameCore.Editor.NetworkCommands;
using GameCore.NetworkCommands;
using GameCore.Objects.NetworkStates;
using NUnit.Framework;
using UnityEditor;

namespace DarkNights.Tests
{
    /// <summary>
    /// 验证宿主和已安装包的命令均能经真实脚本发现并进入生成注册表。
    /// 只读制作资产，测试结束恢复注册状态；独立 LAN Sample 保持自己的注册边界。
    /// </summary>
    public sealed class NetworkCommandRegistryTests
    {
        [Test]
        public void TerrainCommandScriptResolvesCommandInsteadOfSiblingResult()
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                "Packages/com.tsgame.anyrules.yygc/Runtime/TerrainEditCommand.cs");
            Assert.That(script, Is.Not.Null);
            var resolver = typeof(NetworkCommandInterfaceGenerator).GetMethod(
                "GetTypeFromMonoScript", BindingFlags.NonPublic | BindingFlags.Static);
            var resolved = (Type)resolver.Invoke(null, new object[] { script });
            Assert.That(resolved?.FullName, Is.EqualTo("AnyRules.Next.FishNet.TerrainEditCommand"));
            Assert.That(typeof(INetworkCommand).IsAssignableFrom(resolved), Is.True);
        }

        [Test]
        public void GeneratedRegistryCoversInstalledCommandsAndPreservesGameTags()
        {
            var previous = GenericTypeRegistry<INetworkCommand>.RegisteredTypes
                .ToDictionary(type => type, type => GenericTypeRegistry<INetworkCommand>.GetId(type));
            bool initialized = GenericTypeRegistry<INetworkCommand>.IsInitialized;
            try
            {
                GenericTypeRegistry<INetworkCommand>.Reset();
                var registry = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("YYGC.Generated.NetworkCommandGeneratedRegistry"))
                    .Single(type => type != null);
                registry.GetMethod("RegisterTypes", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, null);
                GenericTypeRegistry<INetworkCommand>.MarkInitialized();
                var expected = GenericTypeRegistry<INetworkCommand>.DiscoverSupportedTypes()
                    .Where(type => type.Assembly.GetName().Name != "DarkNights.Samples.LanCoop.Runtime");
                Assert.That(GenericTypeRegistry<INetworkCommand>.RegisteredTypes, Is.EquivalentTo(expected));
                Assert.That(GenericTypeRegistry<INetworkCommand>.GetId(typeof(SetReadyCommand)), Is.Zero);
                Assert.That(GenericTypeRegistry<INetworkCommand>.GetId(typeof(SessionCommand)), Is.EqualTo(1));
                Assert.That(GenericTypeRegistry<INetworkCommand>.GetId(typeof(HeroInputCommand)), Is.EqualTo(2));
            }
            finally
            {
                GenericTypeRegistry<INetworkCommand>.Reset();
                foreach (var item in previous) GenericTypeRegistry<INetworkCommand>.Register(item.Key, item.Value);
                if (initialized) GenericTypeRegistry<INetworkCommand>.MarkInitialized();
            }
        }
    }
}
