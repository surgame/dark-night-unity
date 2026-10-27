using FishNet.Broadcast;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 定义兼容检查之后的房间票据挑战；只传随机nonce及持有证明，不传密码或临时票据密钥。
    /// 它不授予游戏槽位，仍由原SessionServer在可信FishNet连接上分配身份和Ready租约。
    /// </summary>
    public struct RoomAuthMessage : IBroadcast
    {
        public byte Stage;
        public string Nonce;
        public string Peer;
        public string Proof;
        public string Error;
    }
}
