using System;
using System.Net;
using System.Net.Sockets;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 仅服务房间发现和开局前控制的IPv4 UDP套接字，主线程按有限预算读取，不创建后台轮询或游戏同步通道。
    /// 释放时关闭本实例端口；错误报文静默丢弃，不把远端输入送入无界日志。
    /// </summary>
    internal sealed class RoomSocket : IDisposable
    {
        private UdpClient socket;
        internal RoomSocket(int port = 0)
        {
            socket = new UdpClient(AddressFamily.InterNetwork);
            try
            {
                socket.ExclusiveAddressUse = true;
                socket.Client.ReceiveBufferSize = 65536;
                socket.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                socket.EnableBroadcast = true;
            }
            catch { socket.Dispose(); socket = null; throw; }
        }

        internal void Poll(Action<RoomPacket, IPEndPoint> receive)
        {
            for (int i = 0; i < 16 && socket != null && socket.Available > 0; i++)
            {
                IPEndPoint endpoint = null;
                byte[] bytes = socket.Receive(ref endpoint);
                try
                {
                    RoomPacket packet = RoomPacket.Decode(bytes);
                    if (packet != null) receive(packet, endpoint);
                }
                catch (ArgumentException) { }
                catch (FormatException) { }
            }
        }

        internal void Send(RoomPacket packet, IPEndPoint endpoint)
        {
            if (socket == null) return;
            byte[] bytes = packet.Encode();
            socket.Send(bytes, bytes.Length, endpoint);
        }

        public void Dispose() { socket?.Dispose(); socket = null; }
    }
}
