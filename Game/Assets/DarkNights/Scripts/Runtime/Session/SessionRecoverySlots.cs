using System;
using System.Security.Cryptography;

namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 单个房间拥有的四槽位恢复凭据，服务端随机签发并在成功重连时轮换，断线保留最多 120 秒。
    /// 凭据仅绑定本房间槽位，不是平台身份；活跃槽位不能被抢占，过期凭据不能恢复原连接权限。
    /// </summary>
    public sealed class SessionRecoverySlots
    {
        public const double RetentionSeconds = 120;
        private readonly string[] tokens = new string[4];
        private readonly bool[] active = new bool[4];
        private readonly double[] expires = new double[4];

        public int Claim(bool host, string token, double now)
        {
            if (host)
            {
                if (active[0]) return -1;
                active[0] = true;
                return 0;
            }
            if (token == null || token.Length > 64) return -1;
            for (int i = 1; i < 4; i++)
                if (!active[i] && now >= expires[i]) tokens[i] = null;
            int slot = -1;
            for (int i = 1; i < 4; i++)
            {
                if (active[i]) continue;
                if (token.Length == 0 ? tokens[i] == null : tokens[i] == token) { slot = i; break; }
            }
            if (slot < 0) return -1;
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            tokens[slot] = Convert.ToBase64String(bytes);
            active[slot] = true;
            return slot;
        }

        public string Token(int slot) => tokens[slot] ?? "";

        public void Release(int slot, double now)
        {
            if (!active[slot]) return;
            active[slot] = false;
            expires[slot] = now + RetentionSeconds;
        }
    }
}
