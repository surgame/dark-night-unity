using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DarkNights.Runtime.Framework;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>
    /// 用独立完成源验证并发启动、所有权顺序以及失败／取消后的晚完成释放。
    /// 不依赖时长阈值或Unity导入速度；租约计数直接验证收尾，不复制批处理实现。
    /// </summary>
    public sealed class StartupResourceBatchTests
    {
        [Test]
        public async Task StartsAllRequestsAndTransfersLeasesInInputOrder()
        {
            var sources = Sources(3);
            var owned = new[] { new Lease(), new Lease(), new Lease() };
            int started = 0;
            var pending = StartupResourceBatch.Load(new[] { 0, 1, 2 },
                (index, token) => { started++; return sources[index].Task; }, CancellationToken.None);
            Assert.That(started, Is.EqualTo(3));
            Assert.That(pending.Status, Is.EqualTo(UniTaskStatus.Pending));
            sources[2].TrySetResult(owned[2]); sources[0].TrySetResult(owned[0]); sources[1].TrySetResult(owned[1]);
            Lease[] result = await pending;
            Assert.That(result, Is.EqualTo(owned));
            Assert.That(owned.Select(lease => lease.Released), Is.All.EqualTo(0));
            foreach (Lease lease in result) lease.Dispose();
            Assert.That(owned.Select(lease => lease.Released), Is.All.EqualTo(1));
        }

        [Test]
        public async Task FailureWaitsForLateSuccessAndCancelsSiblings()
        {
            var sources = Sources(3);
            var owned = new[] { new Lease(), new Lease() };
            var cause = new InvalidOperationException("failed asset");
            CancellationToken sibling = default;
            var pending = StartupResourceBatch.Load(new[] { 0, 1, 2 },
                (index, token) => { if (index == 2) sibling = token; return sources[index].Task; }, CancellationToken.None);
            sources[0].TrySetResult(owned[0]); sources[1].TrySetException(cause);
            Assert.That(sibling.IsCancellationRequested, Is.True);
            Assert.That(pending.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(owned[0].Released, Is.Zero);
            sources[2].TrySetResult(owned[1]);
            Assert.That(await Failure(pending), Is.SameAs(cause));
            Assert.That(owned.Select(lease => lease.Released), Is.All.EqualTo(1));
        }

        [Test]
        public async Task ExternalCancellationReleasesRequestsThatIgnoreCancellation()
        {
            using var cancellation = new CancellationTokenSource();
            var sources = Sources(2);
            var owned = new[] { new Lease(), new Lease() };
            var pending = StartupResourceBatch.Load(new[] { 0, 1 },
                (index, token) => sources[index].Task, cancellation.Token);
            cancellation.Cancel();
            Assert.That(pending.Status, Is.EqualTo(UniTaskStatus.Pending));
            sources[1].TrySetResult(owned[1]); sources[0].TrySetResult(owned[0]);
            Assert.That(await Failure(pending), Is.InstanceOf<OperationCanceledException>());
            Assert.That(owned.Select(lease => lease.Released), Is.All.EqualTo(1));
        }

        [Test]
        public async Task SynchronousFailureStillObservesLaterRequests()
        {
            var source = new UniTaskCompletionSource<Lease>();
            var owned = new Lease();
            var cause = new InvalidOperationException("synchronous factory failure");
            int started = 0;
            var pending = StartupResourceBatch.Load(new[] { 0, 1 }, (index, token) =>
            {
                started++;
                if (index == 0) throw cause;
                return source.Task;
            }, CancellationToken.None);
            Assert.That(started, Is.EqualTo(2));
            Assert.That(pending.Status, Is.EqualTo(UniTaskStatus.Pending));
            source.TrySetResult(owned);
            Assert.That(await Failure(pending), Is.SameAs(cause));
            Assert.That(owned.Released, Is.EqualTo(1));
        }

        [Test]
        public async Task CleanupContinuesAfterOneLeaseThrows()
        {
            var sources = Sources(3);
            var owned = new[] { new Lease(true), new Lease() };
            var cause = new InvalidOperationException("failed asset");
            var pending = StartupResourceBatch.Load(new[] { 0, 1, 2 },
                (index, token) => sources[index].Task, CancellationToken.None);
            sources[0].TrySetResult(owned[0]); sources[1].TrySetResult(owned[1]); sources[2].TrySetException(cause);
            var failure = await Failure(pending) as AggregateException;
            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.InnerExceptions.First(), Is.SameAs(cause));
            Assert.That(owned.Select(lease => lease.Released), Is.All.EqualTo(1));
        }

        [Test]
        public async Task CancelledBeforeStartDoesNotInvokeFactory()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            int started = 0;
            var pending = StartupResourceBatch.Load(new[] { 0 },
                (index, token) => { started++; return UniTask.FromResult(new Lease()); }, cancellation.Token);
            Assert.That(await Failure(pending), Is.InstanceOf<OperationCanceledException>());
            Assert.That(started, Is.Zero);
        }

        [Test]
        public async Task NullLeaseFailsAndReleasesSuccessfulSiblings()
        {
            var owned = new Lease();
            var pending = StartupResourceBatch.Load(new[] { 0, 1 },
                (index, token) => UniTask.FromResult(index == 0 ? owned : null), CancellationToken.None);
            Assert.That(await Failure(pending), Is.InstanceOf<InvalidOperationException>());
            Assert.That(owned.Released, Is.EqualTo(1));
        }

        [Test]
        public async Task EmptyBatchDoesNotInvokeFactory()
        {
            int started = 0;
            Lease[] result = await StartupResourceBatch.Load(Array.Empty<int>(),
                (index, token) => { started++; return UniTask.FromResult(new Lease()); }, CancellationToken.None);
            Assert.That(result, Is.Empty);
            Assert.That(started, Is.Zero);
        }

        private static UniTaskCompletionSource<Lease>[] Sources(int count) => Enumerable.Range(0, count)
            .Select(_ => new UniTaskCompletionSource<Lease>()).ToArray();

        private static async Task<Exception> Failure(UniTask<Lease[]> task)
        {
            try { await task; return null; }
            catch (Exception exception) { return exception; }
        }

        /// <summary>仅测试使用的可观测租约；可注入释放失败以验证其它资源仍会收尾。</summary>
        private sealed class Lease : IDisposable
        {
            private readonly bool failRelease;
            public int Released { get; private set; }
            public Lease(bool failRelease = false) { this.failRelease = failRelease; }
            public void Dispose()
            {
                Released++;
                if (failRelease) throw new InvalidOperationException("release failed");
            }
        }
    }
}
