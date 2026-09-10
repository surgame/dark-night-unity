using System.Collections.Generic;

namespace DarkNights.Samples.LanCoop.Core
{
    /// <summary>
    /// 只在权威端创建的样板营地；单线程原子校验及扣款，库存和工位仅有此写入者。
    /// 10 金币、10 费用、一个工位都是独立测试夹具，不是 Dark Nights 平衡数值。
    /// </summary>
    public sealed class CampSession
    {
        private readonly Dictionary<int, long> sequences = new Dictionary<int, long>();
        private readonly HashSet<int> readyPlayers = new HashSet<int>();
        private int epoch = 1, revision = 1, coins = 10, purchases, occupant = -1, ticks;
        private bool paused = true;

        public CampReplica Snapshot() => new CampReplica(epoch, revision, coins, purchases, occupant, paused, ticks);

        public string Apply(int player, bool host, long sequence, int protocol, int requestEpoch,
            SampleOperation operation, int entity)
        {
            if (protocol != 1) return "ProtocolMismatch";
            if (requestEpoch != epoch) return "StaleEpoch";
            if (sequence <= 0) return "BadSequence";
            if (sequences.TryGetValue(player, out long last) && sequence <= last) return "DuplicateOrExpired";
            sequences[player] = sequence;
            if (operation == SampleOperation.Ready)
            {
                readyPlayers.Add(player);
                return "Ready";
            }
            if (!readyPlayers.Contains(player)) return "NotReady";
            if (entity != 1) return "InvalidEntity";
            switch (operation)
            {
                case SampleOperation.Buy:
                    if (coins < 10) return "InsufficientFunds";
                    coins -= 10;
                    purchases++;
                    break;
                case SampleOperation.Claim:
                    if (occupant != -1) return "Occupied";
                    occupant = player;
                    break;
                case SampleOperation.Release:
                    if (occupant != player) return "NotOccupant";
                    occupant = -1;
                    break;
                case SampleOperation.Pause:
                    if (!host) return "HostOnly";
                    paused = !paused;
                    break;
                case SampleOperation.Reset:
                    if (!host) return "HostOnly";
                    epoch++;
                    coins = 10;
                    purchases = ticks = 0;
                    occupant = -1;
                    paused = true;
                    readyPlayers.Clear();
                    break;
                default: return "UnknownOperation";
            }
            revision++;
            return "Accepted";
        }

        public void Step()
        {
            if (paused) return;
            ticks++;
            revision++;
        }

        public void RemovePlayer(int player)
        {
            readyPlayers.Remove(player);
            sequences.Remove(player);
            if (occupant != player) return;
            occupant = -1;
            revision++;
        }
    }
}
