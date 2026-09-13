using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace DarkNights.Tests
{
    /// <summary>
    /// 为 EditMode 集成测试临时取消 AssetDatabaseProvider 的模拟加载延迟，结束后恢复原值。
    /// 后台编辑器的 unscaledTime 可能停止，不能用它等待本地资源；仍使用真实 Addressables 及工厂装配。
    /// </summary>
    public sealed class EditorAssetLoading : IDisposable
    {
        private readonly (AssetDatabaseProvider Provider, float Delay)[] providers;
        private readonly CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        public CancellationToken CancellationToken => timeout.Token;

        public EditorAssetLoading()
        {
            if (Application.isPlaying)
            {
                providers = Array.Empty<(AssetDatabaseProvider, float)>();
                return;
            }
            var initialization = Addressables.InitializeAsync(autoReleaseHandle: false);
            try { initialization.WaitForCompletion(); }
            finally { Addressables.Release(initialization); }
            providers = Addressables.ResourceManager.ResourceProviders.OfType<AssetDatabaseProvider>()
                .Select(provider => (provider, provider.GetLoadDelay())).ToArray();
            foreach (var entry in providers) entry.Provider.SetLoadDelay(0);
        }

        public void Dispose()
        {
            foreach (var entry in providers) entry.Provider.SetLoadDelay(entry.Delay);
            timeout.Dispose();
        }
    }
}
