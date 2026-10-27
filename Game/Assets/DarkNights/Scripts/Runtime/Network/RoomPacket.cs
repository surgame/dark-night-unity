using System;
using System.Text;
using UnityEngine;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 仅供LAN发现与开局前准入的有界报文，不承载世界、物品或游戏命令。
    /// 所有接收先限制字节与字段长度；签名覆盖除Proof之外的全部已知字段，业务协议仍走原FishNet链。
    /// </summary>
    [Serializable]
    internal sealed class RoomPacket
    {
        internal const int MaximumBytes = 4096;
        public int Version = 1;
        public string Type = "", Room = "", Request = "", Name = "", Peer = "";
        public string Nonce = "", Salt = "", Proof = "", Content = "", Error = "";
        public int Port, Players, State, Revision;
        public long Sequence;
        public bool Password, Ready;
        public string[] Names = Array.Empty<string>();
        public bool[] Readiness = Array.Empty<bool>();

        internal byte[] Encode()
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));
            if (bytes.Length > MaximumBytes) throw new FormatException("房间报文过大。");
            return bytes;
        }

        internal static RoomPacket Decode(byte[] bytes)
        {
            if (bytes.Length == 0 || bytes.Length > MaximumBytes) return null;
            var value = JsonUtility.FromJson<RoomPacket>(Encoding.UTF8.GetString(bytes));
            if (value == null || value.Version != 1 || value.Port < 0 || value.Port > 65535 ||
                value.Players < 0 || value.Players > 4 || value.Sequence < 0 ||
                value.Names == null || value.Readiness == null || value.Names.Length > 4 ||
                value.Names.Length != value.Readiness.Length) return null;
            foreach (string field in new[] { value.Type, value.Room, value.Request, value.Name,
                value.Peer, value.Nonce, value.Salt, value.Proof, value.Content, value.Error })
                if (field == null || field.Length > 128) return null;
            foreach (string name in value.Names) if (name == null || name.Length > 32) return null;
            return value;
        }

        internal string SigningText()
        {
            var text = new StringBuilder();
            foreach (string field in new[] { Type, Room, Request, Name, Peer, Nonce, Salt, Content, Error })
                text.Append(field.Length).Append(':').Append(field);
            text.Append('|').Append(Version).Append('|').Append(Port).Append('|').Append(Players)
                .Append('|').Append(State).Append('|').Append(Revision).Append('|').Append(Sequence)
                .Append('|').Append(Password ? 1 : 0).Append('|').Append(Ready ? 1 : 0);
            for (int i = 0; i < Names.Length; i++)
                text.Append('|').Append(Names[i].Length).Append(':').Append(Names[i]).Append(Readiness[i] ? '1' : '0');
            return text.ToString();
        }
    }
}
