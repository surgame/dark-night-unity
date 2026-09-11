using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Commands;
using DarkNights.Core.Logic.Entities;
using DarkNights.Core.ViewData;
using DarkNights.Core.Logic.State;
using DarkNights.Core.Logic.Systems;


namespace DarkNights.Core.Logic
{
    /// <summary>
    /// 单局的组合与模拟入口，拥有状态和各项玩法服务。Advance只应用一次暂停/倍速，然后按固定顺序调用服务；节点、输入和文件读写由外层负责。
    /// </summary>
    public sealed class GameSession
    {
        public GameCatalog Catalog { get; }
        public LevelLayout Layout { get; }
        public WorldState World { get; } = new();
        public CampaignStats Stats { get; } = new();
        public SessionFeedback Feedback { get; }
        public SimulationRandom Random { get; } = new();
        public EconomyService Economy { get; }
        public EntityLifecycle Lifecycle { get; }
        public WorkOrders Work { get; }
        public UnitOrders Orders { get; }
        public ConstructionService Construction { get; }
        public TrainingService Training { get; }
        public CampCommands Camp { get; }
        public CombatService Combat { get; }
        public ProjectileSystem Projectiles { get; }
        public WaveDirector Waves { get; }
        public SessionMode Mode { get; internal set; } = SessionMode.Menu;
        public bool Paused { get; set; }
        private double speed = 1;
        public double Speed
        {
            get => speed;
            set => speed = value == 1 || value == 2 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }
        public double Elapsed { get; internal set; }
        public float GroundY => Layout.GroundY;
        public event Action<bool> ResetOccurred;
        public event Action<bool> Finished;

        public GameSession(GameCatalog catalog, LevelLayout layout, SessionFeedback feedback = null)
        {
            Feedback = feedback ?? new SessionFeedback();
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Layout.Validate(catalog);
            Economy = new(this);
            Lifecycle = new(this);
            Work = new(this);
            Orders = new(this);
            Construction = new(this);
            Training = new(this);
            Camp = new(this);
            Combat = new(this);
            Projectiles = new(this);
            Waves = new(this);
            NewGame();
        }

        public void NewGame(bool mainMenu = false)
        {
            ClearState();
            Random.Seed = Catalog.Level.Seed;
            foreach (var entry in Layout.Buildings)
                Lifecycle.SpawnBuilding(entry.Kind, entry.X, true);
            foreach (var entry in Layout.Worksites)
                Lifecycle.SpawnSite(entry.Kind, entry.X, entry.Variant);
            foreach (var entry in Layout.Actors)
                Lifecycle.SpawnActor(entry.Kind, entry.X, false, entry.Name);
            Mode = mainMenu ? SessionMode.Menu : SessionMode.Playing;
            ResetOccurred?.Invoke(mainMenu);
            if (!mainMenu)
            {
                Feedback.ShowBanner("灰松谷 · 第一天", "安排生产，训练守卫。守住三次夜袭。");
                Feedback.Notify("先安排一名工人耕作，再采集木材。东侧已有两名守卫。");
            }
        }

        internal void ClearState()
        {
            World.Clear();
            Stats.Reset();
            Feedback.Reset();
            Economy.Reset();
            Waves.Reset();
            Elapsed = 0;
            Paused = false;
            Speed = 1;
        }

        internal void NotifyRestored()
        {
            Mode = SessionMode.Playing;
            ResetOccurred?.Invoke(false);
        }

        public void Advance(double realDelta)
        {
            if (Mode != SessionMode.Playing || Paused)
                return;
            if (double.IsNaN(realDelta) || double.IsInfinity(realDelta) || realDelta < 0)
                throw new ArgumentOutOfRangeException(nameof(realDelta));
            double delta = realDelta * Speed;
            Elapsed += delta;
            Economy.Tick(delta);
            foreach (var building in World.Buildings.ToArray())
                building.Tick(delta);
            foreach (var actor in World.Actors.ToArray())
            {
                if (Mode != SessionMode.Playing)
                    return;
                actor.Tick(delta);
            }
            foreach (var site in World.Worksites.ToArray())
                site.Tick(delta);
            Projectiles.Tick(delta);
            if (Mode == SessionMode.Playing)
                Waves.Tick(delta);
        }

        public void Finish(bool won)
        {
            if (Mode != SessionMode.Playing)
                return;
            Mode = won ? SessionMode.Won : SessionMode.Lost;
            Paused = false;
            Finished?.Invoke(won);
        }


    }
}
