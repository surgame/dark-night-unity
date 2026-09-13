using System;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 会话的三夜阶段能力，白天、刷怪顺序、清场奖励与横幅保留冻结规则。
    /// 只在模拟的最后阶段执行，随机采样由同一会话状态提供，敌人经 YYGC 工厂创建。
    /// </summary>
    public sealed partial class WaveBehaviour : SessionStateBehaviour<WaveState>
    {
        private WaveDefinition Rules => Session.Catalog.Level.Waves[Current.Index];

        internal void Prepare() => PrepareState(new WaveState
        {
            Phase = WavePhase.Day, DayRemaining = Session.Catalog.Level.Waves[0].DaySeconds
        });

        internal bool StartNight()
        {
            if (Current.Phase != WavePhase.Day || Session.Camp.Read().Mode != SessionMode.Playing) return false;
            WaveState state = Edit();
            state.Phase = WavePhase.Night;
            state.DayRemaining = 0;
            state.NextSpawn = 0;
            state.SpawnElapsed = Rules.SpawnInterval;
            string title = "第 " + (state.Index + 1) + " 夜 · 墓地苏醒";
            Session.Mutations.AfterCommit(() => Session.Feedback.ShowBanner(title, "敌人从东侧来袭，守住酒馆。"));
            Session.Notify("夜袭开始。G选择守卫，右键部署到东侧防线。", true);
            return true;
        }

        internal void Tick(double delta)
        {
            WaveState state = Edit();
            if (state.Phase == WavePhase.Day)
            {
                state.DayRemaining = Math.Max(0, state.DayRemaining - delta);
                if (state.DayRemaining <= 0) StartNight();
                return;
            }
            state.SpawnElapsed += delta;
            while (state.NextSpawn < Rules.Enemies.Count && state.SpawnElapsed >= Rules.SpawnInterval)
            {
                state.SpawnElapsed -= Rules.SpawnInterval;
                float x = Session.Layout.SpawnX + Session.Camp.RandomFloat(-5, 5);
                Session.Lifecycle.SpawnActor(Rules.Enemies[state.NextSpawn], x, true);
                state.NextSpawn++;
            }
            if (state.NextSpawn != Rules.Enemies.Count || Session.Index.EnemyCount != 0) return;
            Session.Economy.Credit(Rules.Reward);
            if (state.Index == Session.Catalog.Level.Waves.Count - 1) { Session.Camp.Finish(true); return; }
            state.Index++;
            state.Phase = WavePhase.Day;
            state.DayRemaining = Rules.DaySeconds;
            state.NextSpawn = 0;
            state.SpawnElapsed = 0;
            string title = "第 " + (state.Index + 1) + " 日 · 重整营地";
            Session.Mutations.AfterCommit(() =>
            {
                Session.Feedback.ShowBanner(title, "防守补给已到达。修缮建筑，补充守卫。");
                Session.Feedback.PlaySound("snd_training_complete");
            });
        }
    }
}
