using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DarkNights.Runtime.Framework
{
    /// <summary>
    /// 并发准备启动所需的独立资源租约，全部成功后按输入顺序移交所有权。
    /// 失败取消同批请求并等待每个请求收尾，包含忽略取消而晚成功的租约；不创建或激活实例。
    /// </summary>
    public static class StartupResourceBatch
    {
        public static async UniTask<TLease[]> Load<TSource, TLease>(IReadOnlyList<TSource> sources,
            Func<TSource, CancellationToken, UniTask<TLease>> prepare, CancellationToken cancellationToken)
            where TLease : class, IDisposable
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            if (prepare == null) throw new ArgumentNullException(nameof(prepare));
            cancellationToken.ThrowIfCancellationRequested();
            if (sources.Count == 0) return Array.Empty<TLease>();
            var leases = new TLease[sources.Count];
            var failures = new Exception[sources.Count];
            var tasks = new UniTask[sources.Count];
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                for (int i = 0; i < sources.Count; i++) tasks[i] = PrepareOne(i);
                // Each wrapper captures its failure, so WhenAll waits for every owner before cleanup.
                await UniTask.WhenAll(tasks);
                Exception failure = FirstFailure(failures);
                if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
                cancellationToken.ThrowIfCancellationRequested();
                return leases;
            }
            catch (Exception failure)
            {
                var errors = new List<Exception> { failure };
                foreach (TLease lease in leases)
                {
                    if (lease == null) continue;
                    try { lease.Dispose(); }
                    catch (Exception releaseError) { errors.Add(releaseError); }
                }
                if (errors.Count == 1) throw;
                throw new AggregateException("Startup resource cleanup failed.", errors);
            }

            async UniTask PrepareOne(int index)
            {
                try
                {
                    leases[index] = await prepare(sources[index], cancellation.Token);
                    if (leases[index] == null) throw new InvalidOperationException("Resource preparation returned no lease.");
                }
                catch (Exception failure)
                {
                    failures[index] = failure;
                    try { cancellation.Cancel(); }
                    catch (Exception cancelError) { failures[index] = new AggregateException(failure, cancelError); }
                }
            }
        }

        private static Exception FirstFailure(IReadOnlyList<Exception> failures)
        {
            foreach (Exception failure in failures)
                if (failure != null && !(failure is OperationCanceledException)) return failure;
            foreach (Exception failure in failures)
                if (failure != null) return failure;
            return null;
        }
    }
}
