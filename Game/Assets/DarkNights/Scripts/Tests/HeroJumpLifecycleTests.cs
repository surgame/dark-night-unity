using DarkNights.Core.Config;
using DarkNights.Runtime.Objects;
using MemoryPack;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>验证跳跃临时量在 YYGC 状态事务中复制、归池清除，并从序列化合同排除；策略配置只允许定义的值。</summary>
    public sealed class HeroJumpLifecycleTests
    {
        [Test]
        public void JumpRuntimeStateCopiesButDoesNotSerializeAndResetsWithPool()
        {
            var source = new ActorState();
            TerrainMotionTestMap.Set(source, nameof(ActorState.JumpBufferRemaining), .08d);
            TerrainMotionTestMap.Set(source, nameof(ActorState.JumpAscending), true);
            var copy = new ActorState();
            copy.CopyFrom(source);
            Assert.That(copy.JumpBufferRemaining, Is.EqualTo(.08d));
            Assert.That(copy.JumpAscending, Is.True);
            var restored = MemoryPackSerializer.Deserialize<ActorState>(MemoryPackSerializer.Serialize(source));
            Assert.That(restored.JumpBufferRemaining, Is.Zero);
            Assert.That(restored.JumpAscending, Is.False);
            copy.OnReturnToPool();
            Assert.That(copy.JumpBufferRemaining, Is.Zero);
            Assert.That(copy.JumpAscending, Is.False);
        }

        [Test]
        public void UndefinedJumpStrategyIsRejected()
        {
            Assert.Throws<System.ArgumentException>(() => TerrainMotionTestMap.Rules((HeroJumpStrategy)100));
            Assert.That(TerrainMotionTestMap.Rules().JumpStrategy, Is.EqualTo(HeroJumpStrategy.Fixed));
        }
    }
}
