using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Entities;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Logic.Systems
{
    /// <summary>
    /// 推进单关卡的白天准备、定序刷怪与清场奖励。每夜有独立生成游标，所有敌人处理完才推进，第三夜清场通过会话触发胜利。
    /// </summary>
    public sealed class WaveDirector
    {
        private readonly GameSession session;

        public WaveDirector(GameSession session)
        {
            this.session = session;
        }

        public int Index { get; internal set; }
        public WavePhase Phase { get; internal set; } = WavePhase.Day;
        public double DayRemaining { get; internal set; }
        public double SpawnElapsed { get; internal set; }
        public int NextSpawn { get; internal set; }
        public WaveDefinition Current => session.Catalog.Level.Waves[Index];

        internal void Reset()
        {
            Index = 0;
            Phase = WavePhase.Day;
            DayRemaining = Current.DaySeconds;
            SpawnElapsed = 0;
            NextSpawn = 0;
        }

        public void StartNight()
        {
            if (Phase != WavePhase.Day || session.Mode != SessionMode.Playing)
                return;
            Phase = WavePhase.Night;
            DayRemaining = 0;
            NextSpawn = 0;
            SpawnElapsed = Current.SpawnInterval;
            session.Feedback.ShowBanner($"第 {Index + 1} 夜 · 墓地苏醒", "敌人从东侧来袭，守住酒馆。");
            session.Feedback.Notify("夜袭开始。G选择守卫，右键部署到东侧防线。", true);
        }

        internal void Tick(double delta)
        {
            if (Phase == WavePhase.Day)
            {
                DayRemaining = Math.Max(0, DayRemaining - delta);
                if (DayRemaining <= 0)
                    StartNight();
                return;
            }
            SpawnElapsed += delta;
            while (NextSpawn < Current.Enemies.Count && SpawnElapsed >= Current.SpawnInterval)
            {
                SpawnElapsed -= Current.SpawnInterval;
                float x = session.Layout.SpawnX + session.Random.RandfRange(-5, 5);
                session.Lifecycle.SpawnActor(Current.Enemies[NextSpawn], x, true);
                NextSpawn++;
            }
            if (NextSpawn != Current.Enemies.Count || session.World.EnemyCount != 0)
                return;
            session.Economy.Credit(Current.Reward);
            if (Index == session.Catalog.Level.Waves.Count - 1)
            {
                session.Finish(true);
                return;
            }
            Index++;
            Phase = WavePhase.Day;
            DayRemaining = Current.DaySeconds;
            NextSpawn = 0;
            SpawnElapsed = 0;
            session.Feedback.ShowBanner($"第 {Index + 1} 日 · 重整营地", "防守补给已到达。修缮建筑，补充守卫。");
            session.Feedback.PlaySound("snd_training_complete");
        }
    }
}
