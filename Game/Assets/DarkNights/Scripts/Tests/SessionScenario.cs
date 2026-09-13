using System;
using System.Collections.Generic;
using DarkNights.Core.Config;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Save;

namespace DarkNights.Tests
{
    /// <summary>
    /// 权威会话回归的可信连接及请求构造器，复用正式服务而不模拟网络认证成功。
    /// 所有驱动在单线程执行，测试的 Ready 仅验证会话边界，不能作为真实投影握手验收。
    /// </summary>
    public static class SessionScenario
    {
        public static SessionAuthority Open(GameCatalog catalog, LevelLayout layout,
            out SessionConnection host, out SessionConnection guest)
        {
            var session = Create(catalog, layout);
            host = session.Connect(0);
            guest = session.Connect(1);
            session.AcknowledgeReady(host, session.Epoch, session.Revision);
            session.AcknowledgeReady(guest, session.Epoch, session.Revision);
            return session;
        }

        public static SessionAuthority Create(GameCatalog catalog, LevelLayout layout) =>
            UnifiedSessionScope.Current.NewAuthority(catalog, layout);

        public static ObjectSession World(GameCatalog catalog, LevelLayout layout) =>
            UnifiedSessionScope.Current.NewWorld(catalog, layout);

        public static ObjectWorldSaveJson Codec(GameCatalog catalog, LevelLayout layout) =>
            UnifiedSessionScope.Current.Codec(catalog, layout);

        public static SessionRequest Request(SessionAuthority session, SessionOperation operation, long sequence,
            IReadOnlyList<int> actors = null, int target = 0, float x = 0, string kind = "", int value = 0) =>
            new SessionRequest(operation, SessionAuthority.ProtocolVersion, session.Epoch, session.PolicyRevision,
                sequence, actors, target, x, kind, value);

        public static SessionReceipt Execute(SessionAuthority session, SessionConnection connection, SessionRequest request)
        {
            var receipt = session.Submit(connection, request);
            if (receipt.Code != SessionResultCode.Pending) return receipt;
            session.Tick();
            return session.Submit(connection, request);
        }

        public static bool Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return true; }
            return false;
        }
    }
}
