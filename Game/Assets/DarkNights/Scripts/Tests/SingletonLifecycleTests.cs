using GameCore.Interactions;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>
    /// 验证实际 YYGC Interaction Sessions 对象的池化重入，不重造并列单例或操作产品 UI。
    /// 每轮退出必须清空 Instance 和会话，重新装配同一实例时必须恢复可用服务。
    /// </summary>
    public sealed class SingletonLifecycleTests
    {
        [Test]
        public void InteractionSingletonRegistersAgainAfterPooledDespawn()
        {
            Assert.That(YYInteractionSessionService.Instance, Is.Null, "Run this regression outside Play.");
            var service = new YYInteractionSessionService();
            try
            {
                for (int round = 0; round < 3; round++)
                {
                    service.Initialize(default);
                    Assert.That(YYInteractionSessionService.Instance, Is.SameAs(service));
                    using (service.Begin(new YYInteractionSessionDescriptor { Kind = "regression", Owner = "SingletonLifecycleTests" }))
                        Assert.That(service.ActiveSessions.Count, Is.EqualTo(1));
                    service.OnDespawn();
                    Assert.That(YYInteractionSessionService.Instance, Is.Null);
                    Assert.That(service.ActiveSessions.Count, Is.Zero);
                }
            }
            finally { service.Dispose(); }
        }
    }
}
