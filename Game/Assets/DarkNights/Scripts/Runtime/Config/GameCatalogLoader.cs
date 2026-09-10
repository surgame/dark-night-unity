using System.Threading;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkNights.Runtime.Config
{
    /// <summary>
    /// 在启动期间通过 Addressables 加载两份规则文本，转换为独立的不可变目录后释放资源句柄。
    /// 不保留 TextAsset 或会话状态，失败和取消也释放已取得的引用；不创建另一套资源管理器。
    /// </summary>
    public static class GameCatalogLoader
    {
        public const string BalanceAddress = "dark_nights.config.balance";
        public const string LevelAddress = "dark_nights.config.pinewatch";

        public static async UniTask<GameCatalog> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var texts = await UniTask.WhenAll(ReadText(BalanceAddress, cancellationToken), ReadText(LevelAddress, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            return GameCatalogJson.Parse(texts.Item1, texts.Item2);
        }

        private static async UniTask<string> ReadText(string address, CancellationToken cancellationToken)
        {
            var handle = Addressables.LoadAssetAsync<TextAsset>(address);
            try
            {
                var asset = await handle.ToUniTask(cancellationToken: cancellationToken);
                return asset.text;
            }
            finally
            {
                // 各请求独立释放，WhenAll 中另一个请求失败不会提前释放仍在等待的句柄。
                if (handle.IsValid()) Addressables.Release(handle);
            }
        }
    }
}
