using System;
using DarkNights.Core.Logic.State;
using GameCore.Objects.NetworkStates;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 由正式会话 ObjectDefinition 装配的 YYGC 状态行为。
    /// 仅在服务端把已结算的会话元数据复制到可靠投影，不创建或推进 GameSession，也不接受客户端直接写状态。
    /// </summary>
    public sealed class WorldSessionBehaviour : StatefulBehaviour<SessionStatusState>
    {
        protected override void Initialize()
        {
        }

        public void Publish(int protocol, int epoch, int revision, int policyRevision,
            CampControlMode controlMode, int readyCount, int playerCount, bool paused, int speedMultiplier)
        {
            if (protocol <= 0 || epoch <= 0 || revision < 0 || policyRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision), "Session revisions and protocol are invalid.");
            if (readyCount < 0 || playerCount < readyCount || playerCount > 4)
                throw new ArgumentOutOfRangeException(nameof(readyCount), "Ready/player counts are invalid.");
            if (controlMode != CampControlMode.SharedCamp && controlMode != CampControlMode.HostOnly)
                throw new ArgumentOutOfRangeException(nameof(controlMode), "Unknown camp control mode.");
            if (speedMultiplier != 1 && speedMultiplier != 2)
                throw new ArgumentOutOfRangeException(nameof(speedMultiplier), "Only the preserved 1x and 2x speeds are valid.");

            using (var mutation = MutateState())
            {
                SessionStatusState value = mutation.Value;
                if (value == null) return;
                value.Protocol = protocol;
                value.Epoch = epoch;
                value.Revision = revision;
                value.PolicyRevision = policyRevision;
                value.ControlMode = controlMode;
                value.ReadyCount = readyCount;
                value.PlayerCount = playerCount;
                value.Paused = paused;
                value.SpeedMultiplier = speedMultiplier;
            }
        }
    }
}
