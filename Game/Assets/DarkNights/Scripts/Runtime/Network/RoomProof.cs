using System;
using System.Security.Cryptography;
using System.Text;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 房间准入的单次挑战与临时票据计算；不发送明文密码，不写盘，不把发现报文当作授权。
    /// 票据绑定房间和随机挑战，实际FishNet连接还要重新证明持有票据；LAN传输并不因此等同于TLS。
    /// </summary>
    internal static class RoomProof
    {
        internal static string Random(int length = 32)
        {
            var bytes = new byte[length];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        internal static byte[] Key(string password, string salt)
        {
            byte[] bytes = Convert.FromBase64String(salt);
            if (bytes.Length != 16 || password == null || password.Length > 64)
                throw new FormatException("房间密码或盐无效。");
            using var derive = new Rfc2898DeriveBytes(password, bytes, 100000, HashAlgorithmName.SHA256);
            return derive.GetBytes(32);
        }

        internal static byte[] Ticket(byte[] key, string room, string request, string nonce, string peer)
        {
            using var mac = new HMACSHA256(key);
            return mac.ComputeHash(Encoding.UTF8.GetBytes("ticket|" + room + "|" + request + "|" + nonce + "|" + peer));
        }

        internal static string Sign(byte[] key, string text)
        {
            using var mac = new HMACSHA256(key);
            return Convert.ToBase64String(mac.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        internal static bool Verify(byte[] key, string text, string proof)
        {
            if (key == null || string.IsNullOrEmpty(proof) || proof.Length > 64) return false;
            try
            {
                byte[] actual = Convert.FromBase64String(proof);
                byte[] expected = Convert.FromBase64String(Sign(key, text));
                return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException) { return false; }
        }

        internal static string Name(string value)
        {
            var result = new StringBuilder();
            foreach (char c in value ?? "")
                if (!char.IsControl(c) && result.Length < 32) result.Append(c);
            string name = result.ToString().Trim();
            return name.Length == 0 ? "灰松谷远征" : name;
        }
    }
}
