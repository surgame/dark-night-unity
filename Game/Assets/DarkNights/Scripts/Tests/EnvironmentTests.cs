using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DarkNights.Editor;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>
    /// 验证已有环境会在写入前拒绝初始化，以及正式规则的 Addressable 和启动注册完整。
    /// 对仓库内已有资源取哈希，检查失败路径不会保存场景或修改人工资产。
    /// </summary>
    public sealed class EnvironmentTests
    {
        [Test]
        public void InitializationRejectsExistingAssetsWithoutWrites()
        {
            string[] paths = Directory.GetFiles("Assets", "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".prefab") || path.EndsWith(".unity") || path.EndsWith(".asset")).ToArray();
            string[] before = paths.Select(Hash).ToArray();
            Assert.Throws<InvalidOperationException>(() => DarkNightsEnvironmentSetup.Initialize());
            Assert.That(paths.Select(Hash).ToArray(), Is.EqualTo(before));
        }

        [Test]
        public void FormalConfigurationIsRegistered()
        {
            Assert.DoesNotThrow(GameContentSetup.Validate);
        }

        private static string Hash(string path)
        {
            using (var hash = SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));
        }
    }
}
