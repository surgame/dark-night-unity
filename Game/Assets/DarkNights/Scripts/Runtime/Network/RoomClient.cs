using System;
using System.Net;
using DarkNights.Core.ViewData;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 单个LAN加入尝试的客户端控制；挑战、密码派生与临时票据仅驻内存，取消即丢弃。
    /// 收到带票据签名的开始通知才请求原FishNet连接，准备操作靠后续心跳重试，不伪造成功或进度。
    /// </summary>
    internal sealed class RoomClient : IDisposable
    {
        private readonly RoomSocket socket;
        private readonly string content;
        private readonly IPEndPoint endpoint;
        private string request, password, nonce = "", salt = "";
        private byte[] passwordKey;
        private double began, seen, nextSend;
        private int revision = -1;
        private long sequence;
        private RoomPacket current;
        internal string RoomId { get; private set; }
        internal string PeerId { get; private set; } = "";
        internal byte[] Key { get; private set; }
        internal string Name { get; private set; }
        internal string Address => endpoint.Address.ToString();
        internal string Stage { get; private set; } = "Joining";
        internal bool DesiredReady { get; private set; }
        internal bool ChallengeReceived => nonce.Length != 0;
        internal event Action Changed, PasswordRequired, StartRequested;
        internal event Action<string> Failed;

        internal RoomClient(RoomSocket socket, RoomListingData listing, string content, string password, double now)
        {
            this.socket = socket; this.content = content; this.password = password ?? "";
            RoomId = listing.Id; Name = listing.Name;
            endpoint = new IPEndPoint(IPAddress.Parse(listing.Address), SessionRoomService.DiscoveryPort);
            Restart(now);
        }

        private void Restart(double now)
        {
            if (passwordKey != null) Array.Clear(passwordKey, 0, passwordKey.Length);
            passwordKey = null;
            request = RoomProof.Random(16); nonce = ""; nextSend = 0;
            began = seen = now; Stage = "Joining";
        }

        internal void SubmitPassword(string value, double now)
        {
            password = value ?? "";
            if (password.Length > 64) throw new ArgumentException("密码不能超过64个字符。");
            Restart(now);
        }

        internal void Poll(double now)
        {
            if (Stage == "Failed" || Stage == "Password" || Stage == "Starting") return;
            if (now - (Stage == "Lobby" ? seen : began) > (Stage == "Lobby" ? 10 : 15))
            { Reject("Timeout"); return; }
            if (now < nextSend) return;
            nextSend = now + .8;
            if (Stage == "Lobby") { Send("status"); return; }
            if (nonce.Length == 0)
                socket.Send(new RoomPacket { Type = "join", Room = RoomId, Request = request, Content = content }, endpoint);
            else socket.Send(new RoomPacket { Type = "prove", Room = RoomId, Request = request,
                Proof = RoomProof.Sign(passwordKey, "join|" + RoomId + "|" + request + "|" + nonce) }, endpoint);
        }

        internal void Receive(RoomPacket packet, IPEndPoint source, double now)
        {
            if (!endpoint.Equals(source) || Stage == "Failed") return;
            if (packet.Type == "rejected" && packet.Request == request)
            {
                if (packet.Error == "Password")
                { Stage = "Password"; password = ""; PasswordRequired?.Invoke(); Changed?.Invoke(); }
                else Reject(packet.Error);
                return;
            }
            if (packet.Type == "challenge" && packet.Request == request && Stage == "Joining")
            {
                if (nonce.Length != 0) return;
                RoomId = packet.Room; Name = RoomProof.Name(packet.Name);
                nonce = packet.Nonce; salt = packet.Salt;
                if (packet.Password && password.Length == 0)
                { Stage = "Password"; PasswordRequired?.Invoke(); Changed?.Invoke(); return; }
                passwordKey = RoomProof.Key(password, salt); password = "";
                nextSend = 0;
                Changed?.Invoke();
                return;
            }
            if (packet.Room != RoomId) return;
            if (packet.Type == "accepted" && packet.Request == request && passwordKey != null)
            {
                byte[] ticket = RoomProof.Ticket(passwordKey, RoomId, request, nonce, packet.Peer);
                if (!RoomProof.Verify(ticket, packet.SigningText(), packet.Proof)) return;
                if (Key != null) Array.Clear(Key, 0, Key.Length);
                Key = ticket; PeerId = packet.Peer; Stage = "Lobby";
                Array.Clear(passwordKey, 0, passwordKey.Length); passwordKey = null;
            }
            if (Key == null || packet.Peer != PeerId || !RoomProof.Verify(Key, packet.SigningText(), packet.Proof)) return;
            seen = now;
            if (packet.Type == "closed") { Reject("Closed"); return; }
            if (packet.Type != "status" && packet.Type != "accepted" || packet.Revision < revision) return;
            revision = packet.Revision; current = packet; Name = RoomProof.Name(packet.Name);
            Changed?.Invoke();
            if (packet.State == 1 && Stage != "Starting")
            { Stage = "Starting"; StartRequested?.Invoke(); }
        }

        internal RoomLobbyData Snapshot() => current == null ? null :
            new RoomLobbyData(Name, false, false, current.Names, current.Readiness, DesiredReady);

        internal void SetReady(bool value)
        {
            if (Stage != "Lobby") return;
            DesiredReady = value; Send("ready"); Changed?.Invoke();
        }

        private void Send(string type)
        {
            if (Key == null) return;
            var packet = new RoomPacket { Type = type, Room = RoomId, Peer = PeerId,
                Sequence = ++sequence, Ready = DesiredReady };
            packet.Proof = RoomProof.Sign(Key, packet.SigningText());
            socket.Send(packet, endpoint);
        }

        private void Reject(string reason) { Stage = "Failed"; password = ""; Failed?.Invoke(reason); }

        public void Dispose()
        {
            try { if (Stage == "Lobby" || Stage == "Starting") Send("leave"); }
            catch (System.Net.Sockets.SocketException) { }
            password = "";
            if (passwordKey != null) Array.Clear(passwordKey, 0, passwordKey.Length);
            if (Key != null) Array.Clear(Key, 0, Key.Length);
            passwordKey = Key = null; Stage = "Failed";
        }
    }
}
