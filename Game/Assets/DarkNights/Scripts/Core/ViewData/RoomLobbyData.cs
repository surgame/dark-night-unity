using System;
using System.Collections.Generic;

namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 开局前队伍的只读展示副本；房主服务唯一维护成员与准备状态，视图不能以此分配游戏身份。
    /// 构造时复制所有成员数据，避免网络后续变更污染已经显示的同一修订。
    /// </summary>
    public sealed class RoomLobbyData
    {
        public string Name { get; }
        public bool IsHost { get; }
        public bool CanStart { get; }
        public IReadOnlyList<string> Players { get; }
        public IReadOnlyList<bool> Ready { get; }
        public bool LocalReady { get; }

        public RoomLobbyData(string name, bool isHost, bool canStart,
            string[] players, bool[] ready, bool localReady)
        {
            Name = name; IsHost = isHost; CanStart = canStart; LocalReady = localReady;
            Players = Array.AsReadOnly((string[])players.Clone());
            Ready = Array.AsReadOnly((bool[])ready.Clone());
        }
    }
}
