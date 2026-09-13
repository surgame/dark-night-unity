using System;
using DarkNights.Core.Logic;
using DarkNights.Core.Logic.State;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 会话对象的时间、序号和随机状态能力；它只推进自己的 State，不保存实体集合。
    /// ID 分配发生在可取消的同步草稿中，装配失败不会消耗编号。
    /// </summary>
    public sealed partial class CampSimulationBehaviour : SessionStateBehaviour<CampSimulationState>
    {
        internal void Prepare()
        {
            var random = new SimulationRandom { Seed = Session.Catalog.Level.Seed };
            PrepareState(new CampSimulationState
            {
                Mode = SessionMode.Playing, Speed = 1, NextEntityId = 1,
                RandomSeed = random.Seed, RandomState = random.State
            });
        }

        internal int Allocate(int requested = 0)
        {
            CampSimulationState state = Edit();
            int id = requested == 0 ? state.NextEntityId : requested;
            if (id < 1 || id > 1000000) throw new InvalidOperationException("Entity ID limit exceeded.");
            state.NextEntityId = Math.Max(state.NextEntityId, checked(id + 1));
            return id;
        }

        internal double BeginStep(double seconds)
        {
            if (Current.Mode != SessionMode.Playing || Current.Paused) return 0;
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            double delta = seconds * Current.Speed;
            Edit().Elapsed += delta;
            return delta;
        }
    }
}
