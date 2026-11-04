using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using DarkNights.View.Terrain;
using GameCore.Interactions;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkNights.Entry.Terrain
{
    /// <summary>
    /// 干净角色白模的场景接线：原生平地或入口轮廓及Worker实例是作者来源，Bootstrap只提供现有服务。
    /// 输入交给GameInputActions，状态交给本地WhiteboxSession，表现只读冻结角色副本。
    /// 不加载正式地图、不连接网络、不访问玩家存档；停止Play后保留原始场景布局。
    /// </summary>
    public sealed class WhiteboxEntry : MonoBehaviour
    {
        public ObjectDefinition SessionDefinition;
        public ObjectInstance SessionInstance;
        public ScenePlacement HeroPlacement;
        public SpriteRenderer Ground;
        public WhiteboxTerrainAuthoring TerrainModule;
        public float TerrainCameraHalfHeight = 2.2f;
        public Camera ViewCamera;
        public PlayerInput InputPlayer;
        public WhiteboxSession Session { get; private set; }
        private ObjectSessionResources resources;
        private GameInputActions actions;
        private ActorPresentationBehaviour presentation;
        private double accumulator;
        private bool jumpPending;
        private float animationWalkSpeed;
        private ActorViewData previousHero;

        public async UniTask Initialize(GameCatalog catalog, CancellationToken token)
        {
            animationWalkSpeed = catalog.Balance.HeroControl.WalkSpeed;
            if (Session != null || SessionDefinition == null || SessionInstance == null || HeroPlacement == null ||
                HeroPlacement.Loader == null || HeroPlacement.View == null || (Ground == null && TerrainModule == null) ||
                ViewCamera == null || InputPlayer == null)
                throw new InvalidOperationException("角色白模缺少显式场景绑定。");
            ObjectDefinition worker = HeroPlacement.Loader.ResolveDefinition();
            if (worker == null || ObjectSessionResources.Rule(worker) != "worker" ||
                (TerrainModule == null && (Math.Abs(Ground.bounds.min.x) > .001f || Math.Abs(Ground.bounds.max.y) > .001f)) ||
                Math.Abs(HeroPlacement.transform.position.y) > .001f)
                throw new InvalidOperationException("白模需要正式Worker、从X=0开始且顶面Y=0的平地及脚底出生点。");
            if (TerrainModule != null && TerrainModule.BakedFingerprint != WhiteboxTerrainBuilder.Fingerprint(TerrainModule))
                throw new InvalidOperationException("入口轮廓已改动，请先在工作台重建入口白模网格并保存。");
            if (TerrainModule != null && TerrainModule.TerrainArtwork != null &&
                (TerrainModule.BackgroundArtwork == null ||
                TerrainModule.ArtworkFingerprint != WhiteboxTerrainBuilder.Fingerprint(TerrainModule)))
                throw new InvalidOperationException("入口SVG资源与轮廓来源不一致，请重新导出对应图层后保存。");
            float width = (TerrainModule == null ? Ground.bounds.size.x : TerrainModule.Width) * EntityView.PixelsPerUnit;
            float spawn = HeroPlacement.transform.position.x * EntityView.PixelsPerUnit;
            var layout = new LevelLayout(width, 0, 0, width, spawn, spawn,
                Array.Empty<PlacementDefinition>(), Array.Empty<PlacementDefinition>(),
                new[] { new PlacementDefinition("worker", spawn, name: HeroPlacement.InitialName,
                    placementKey: HeroPlacement.PlacementKey) }, localWhitebox: true);
            ObjectSession world = null;
            try
            {
                resources = await ObjectSessionResources.Prepare(new[] { worker }, token);
                token.ThrowIfCancellationRequested();
                world = new ObjectSession(catalog, layout, resources, () => this != null, transform);
                if (TerrainModule != null)
                {
                    var profile = SessionDefinition.SharedConfigs.OfType<TerrainProfileConfig>().Single();
                    var rules = profile.Freeze();
                    world.Terrain = new SessionTerrain(world.Context, rules.Business.Gameplay,
                        WhiteboxTerrainBuilder.Build(TerrainModule), rules, profile.Definition);
                    ViewCamera.orthographicSize = TerrainCameraHalfHeight;
                }
                SessionInstance.Initialize("local-whitebox-session", SessionDefinition, session: world.Context, activate: false);
                world.Prepare(SessionInstance, new[]
                {
                    new ObjectPlacement(HeroPlacement.PlacementKey, worker, spawn, 0,
                        HeroPlacement.InitialName, HeroPlacement.Loader)
                });
                world.Activate();
                Session = new WhiteboxSession(world, HeroPlacement.PlacementKey);
                presentation = Session.HeroObject.GetBehaviour<ActorPresentationBehaviour>() ??
                    throw new InvalidOperationException("白模主角缺少正式表现能力。");
                presentation.Bind(Session.Hero.Id, 1, "worker", catalog.Balance.Units["worker"]);
                actions = gameObject.AddComponent<GameInputActions>();
                actions.Initialize(InputPlayer, YYInteractionSessionService.Instance);
                actions.SetHero(true);
                actions.Present(true);
                Application.runInBackground = true;
            }
            catch
            {
                if (Session != null) { Session.Dispose(); Session = null; }
                else world?.Dispose();
                SessionInstance.Release();
                resources?.Dispose();
                resources = null;
                throw;
            }
        }

        private void Update()
        {
            if (Session == null || actions == null) return;
            var input = actions.ReadHero();
            if (actions.ReadCamp().Menu)
            {
                Session.SetPaused(!Session.World.Paused);
                jumpPending = false;
                accumulator = 0;
                previousHero = null;
            }
            if (Session.World.Paused) return;
            if (!input.Allowed) jumpPending = false;
            else jumpPending |= input.JumpPressed;
            accumulator = Math.Min(accumulator + Time.unscaledDeltaTime, .1);
            while (accumulator >= 1d / 60)
            {
                previousHero = Session.Hero;
                Session.Step(input.Allowed ? Math.Sign(input.Move) : 0,
                    jumpPending, input.JumpHeld, input.SprintHeld);
                jumpPending = false;
                accumulator -= 1d / 60;
            }
        }

        private void LateUpdate()
        {
            if (Session == null) return;
            var hero = Session.Hero;
            var sample = ActorPresentationSample.Interpolate(Session.World.Paused ? null : previousHero,
                hero, accumulator * 60);
            presentation.Present(hero, 1, "", sample.X, sample.ActionTime, Color.white, sample.Height,
                heroWalkSpeed: animationWalkSpeed);
            float halfWidth = ViewCamera.orthographicSize * ViewCamera.aspect;
            float width = Session.World.Layout.WorldWidth / EntityView.PixelsPerUnit;
            var point = ViewCamera.transform.position;
            point.x = width <= halfWidth * 2 ? width * .5f :
                Mathf.Clamp(sample.X / EntityView.PixelsPerUnit, halfWidth, width - halfWidth);
            ViewCamera.transform.position = point;
            if (TerrainModule != null)
            {
                point.y = Mathf.Lerp(point.y, sample.Height / EntityView.PixelsPerUnit + .8f,
                    1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
                ViewCamera.transform.position = point;
            }
        }

        private void OnDestroy()
        {
            if (actions != null) actions.Present(false);
            Session?.Dispose();
            Session = null;
            SessionInstance?.Release();
            resources?.Dispose();
            resources = null;
        }
    }
}
