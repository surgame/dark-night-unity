using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkNights.View.Lighting;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DarkNights.Entry
{
    /// <summary>单个光照预设的独立 Addressables 租约；成功、失败与取消均归还自身句柄，覆盖派生光效实例的使用期。</summary>
    public sealed class LightProfileLease : IDisposable
    {
        private AsyncOperationHandle<LightProfile> handle;
        private bool disposed;
        public LightProfile Profile { get; private set; }

        public static async UniTask<LightProfileLease> Load(string guid, CancellationToken cancellationToken)
        {
            var lease = new LightProfileLease();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                lease.handle = Addressables.LoadAssetAsync<LightProfile>(guid);
                lease.Profile = await lease.handle.ToUniTask(cancellationToken: cancellationToken, autoReleaseWhenCanceled: false);
                if (lease.Profile == null) throw new InvalidOperationException("光照预设加载失败：" + guid);
                lease.Profile.Freeze(); return lease;
            }
            catch { lease.Dispose(); throw; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (handle.IsValid()) Addressables.Release(handle);
            Profile = null;
        }
    }
}
