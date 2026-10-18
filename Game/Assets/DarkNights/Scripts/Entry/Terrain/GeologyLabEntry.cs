using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using DarkNights.View;
using DarkNights.View.Terrain;
using DarkNights.Core.ViewData;
using AnyRules.Next;
using GameCore.Interactions;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DarkNights.Entry.Terrain
{
    /// <summary>独立场景功能开发入口；Bootstrap提供现有YYGC服务，场景原生引用是布局唯一来源。仅创建本地可丢弃会话，固定步长复用正式运动、输入许可和矿镐，不装配网络或文件存储。</summary>
    public sealed class GeologyLabEntry : MonoBehaviour
    {
        public const string ScenePath = "Assets/DarkNights/Res/Scenes/Workbenches/Gameplay/CL02CollapseLab.unity";
        public ObjectDefinition SessionDefinition, WorkerDefinition, ShipDefinition;
        public ObjectInstance SessionInstance;
        public Camera ViewCamera;
        /// <summary>可选的运行期像素相机；现行样板在编辑态关闭以保护场景，初始化时启用。历史副本未绑定时沿用其原生组件启用状态。</summary>
        public PixelPerfectCamera RuntimePixelCamera;
        public PlayerInput InputPlayer;
        public UnityEngine.UI.Text Status;
        public TerrainPreview TerrainView;
        /// <summary>场景原生测试标记；只展示尚未触发的位置，不参与碰撞、采集或事件结算。历史验证副本可不绑定。</summary>
        public SpriteRenderer SupportMarker, RegionMarker;
        /// <summary>显式开启才显示黄色支撑定位标记；默认隐藏，触发后即使调试开启也不显示。</summary>
        public bool ShowSupportMarker;
        public GeologyLabBlock[] Blocks = Array.Empty<GeologyLabBlock>();
        public GeologyLabEventSlot[] EventSlots = Array.Empty<GeologyLabEventSlot>();
        public float SpawnX = 704, SpawnHeight = -32, ShipX = 192, WorldWidth = 1600;
        public GeologyLabSession Lab { get; private set; }
        private ObjectSessionResources resources;
        private ObjectDefinition localDefinition;
        private GameInputActions actions;
        private ActorPresentationBehaviour heroPresentation;
        private GeologyEventConfig[] configurations;
        private bool jumpPending, usePending;
        private double accumulator;
        private GameInputActions.HeroFrame input;
        private float aim;

        private void Awake()
        {
            foreach (var block in Blocks) if (block != null && block.Art != null) block.Art.enabled = false;
            if (SupportMarker != null) SupportMarker.enabled = false;
            if (RegionMarker != null) RegionMarker.enabled = false;
            foreach (var slot in EventSlots) if (slot != null && slot.View != null)
            {
                if (slot.View.Warning != null) slot.View.Warning.enabled = false;
                foreach (var fragment in slot.View.Fragments) if (fragment != null) fragment.enabled = false;
            }
        }

        public async UniTask Initialize(GameCatalog catalog, CancellationToken token)
        {
            if (Lab != null || SessionDefinition == null || WorkerDefinition == null || ShipDefinition == null || SessionInstance == null ||
                ViewCamera == null || InputPlayer == null || Status == null || TerrainView == null || Blocks.Length == 0 || EventSlots.Length != 2 ||
                EventSlots.Any(slot => slot == null || slot.Definition == null || slot.Instance == null || slot.View == null) ||
                EventSlots.Select(slot => slot.Definition.Key).Distinct().Count() != EventSlots.Length)
                throw new InvalidOperationException("功能场景缺少明确装配或存在重复事件。");
            if (RuntimePixelCamera != null) RuntimePixelCamera.enabled = true;
            ObjectSession world = null;
            try
            {
                resources = await ObjectSessionResources.Prepare(new[] { WorkerDefinition, ShipDefinition }, token);
                token.ThrowIfCancellationRequested();
                localDefinition = Instantiate(SessionDefinition);
                var source = SessionDefinition.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
                var flow = JsonUtility.FromJson<ExpeditionFlowConfig>(JsonUtility.ToJson(source)); flow.Enabled = false;
                localDefinition.SharedConfigs[localDefinition.SharedConfigs.FindIndex(c => c is ExpeditionFlowConfig)] = flow;
                var layout = new LevelLayout(WorldWidth, 0, 0, WorldWidth, SpawnX, SpawnX,
                    new[] { new PlacementDefinition("ship", ShipX) }, Array.Empty<PlacementDefinition>(), Array.Empty<PlacementDefinition>(),
                    randomTerrain: true, expedition: true);
                world = new ObjectSession(catalog, layout, resources, () => this != null, transform);
                var profile = localDefinition.SharedConfigs.OfType<TerrainProfileConfig>().Single();
                var cells = new byte[320 * 192]; var protection = new bool[cells.Length];
                foreach (var block in Blocks) block.Write(cells, protection);
                for (int x = 0; x < 320; x++) { cells[191 * 320 + x] = 8; protection[191 * 320 + x] = true; }
                var initial = new PlayableTerrain(Guid.NewGuid().ToString("N"), "CL02-GAMEPLAY-LAB", cells, protection,
                    new bool[cells.Length], Array.Empty<TerrainRoom>(), Array.Empty<TerrainDepositBlueprint>(), expedition: true);
                var rules = profile.Freeze();
                world.Terrain = new SessionTerrain(world.Context, rules.Business.Gameplay, initial, rules, profile.Definition);
                SessionInstance.Initialize("geology-lab-session", localDefinition, session: world.Context, activate: false);
                world.Prepare(SessionInstance, new[] { new ObjectPlacement("lab-ship", ShipDefinition, ShipX, 0, "测试参照船", null) });
                world.Activate();
                Lab = new GeologyLabSession(world, SpawnX, SpawnHeight);
                configurations = new GeologyEventConfig[EventSlots.Length];
                for (int index = 0; index < EventSlots.Length; index++)
                {
                    var slot = EventSlots[index];
                    var geology = Lab.BindEvent(slot.Instance, slot.Definition);
                    var config = configurations[index] = geology.Configuration;
                    slot.View.Bind(Enumerable.Range(0, config.RubbleCount).Select(config.Motion).ToArray(),
                        Enumerable.Range(0, config.RubbleCount).Select(config.PileShape).ToArray(), config.Collapse);
                }
                foreach (var block in Blocks) block.Art.enabled = false;
                TerrainView.ShowCaveReplica(profile.Definition, (CaveTerrainStyle)profile.CaveStyle,
                    new TerrainReplicaSource(world.Terrain.Map, ProjectGeologyCell), world.Terrain.Map.World,
                    new BackgroundBakeDescriptor(world.Terrain.Map.World.WorldId.ToString().Replace("-", ""), "CL02-CAVE", cells, new byte[cells.Length]));
                world.Terrain.Map.Changed += OnGeologyMapChanged;
                heroPresentation = Lab.HeroObject.GetBehaviour<ActorPresentationBehaviour>();
                if (heroPresentation == null) throw new InvalidOperationException("功能场景主角缺少原生表现能力。");
                heroPresentation.Bind(Lab.Hero.Id, 1, "worker", catalog.Balance.Units["worker"], null);
                actions = gameObject.AddComponent<GameInputActions>();
                actions.Initialize(InputPlayer, YYInteractionSessionService.Instance); actions.SetHero(true); actions.Present(true);
                Status.text = "CL-02 塌方 · 大小混合版\n挖断起点右侧低矮支撑岩／继续向右进入橙色区域；避让落点后用矿镐清障。\n移动／跳跃／左键沿用正式改键；Esc暂停／继续。停止并重新Play重置。";
            }
            catch
            {
                if (Lab != null) { Lab.Dispose(); Lab = null; } else world?.Dispose();
                resources?.Dispose(); resources = null;
                if (localDefinition != null) Destroy(localDefinition); localDefinition = null;
                throw;
            }
        }

        private void Update()
        {
            if (Lab == null || actions == null) return;
            input = actions.ReadHero();
            if (actions.ReadCamp().Menu)
            {
                Lab.SetPaused(!Lab.Paused); jumpPending = usePending = false; accumulator = 0;
            }
            if (Lab.Paused) return;
            jumpPending |= input.JumpPressed; usePending |= input.UsePressed;
            var tool = Lab.World.Resources.Equipment.Mining(Lab.Hero.Slot0Definition);
            Vector3 hand = new Vector3(Lab.Hero.X / 100, (Lab.Hero.Height + tool.HandHeight) / 100, 0);
            Vector3 pointer = ViewCamera.ScreenToWorldPoint(new Vector3(input.Pointer.x, input.Pointer.y, -ViewCamera.transform.position.z));
            Vector3 direction = pointer - hand;
            if (direction.sqrMagnitude > .0001f) aim = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            accumulator = Math.Min(accumulator + Time.unscaledDeltaTime, .25);
            while (accumulator >= 1d / 60)
            {
                Lab.Step(input.Allowed ? Math.Sign(input.Move) : 0, jumpPending, input.JumpHeld,
                    usePending, input.UseAllowed && input.UseHeld, aim, input.SprintHeld);
                jumpPending = usePending = false; accumulator -= 1d / 60;
            }
        }

        private void LateUpdate()
        {
            if (Lab == null) return;
            var hero = Lab.Hero;
            heroPresentation.Present(hero, 1, "", hero.X, hero.ActionTime, Color.white);
            for (int index = 0; index < EventSlots.Length; index++)
                EventSlots[index].View.Present(Lab.Events[index].Capture(), Lab.SimulationTime);
            var position = ViewCamera.transform.position;
            position.x = Mathf.Clamp(hero.X / 100, 3.2f, WorldWidth / 100 - 3.2f);
            ViewCamera.transform.position = position;
            string[] phases = { "待触发", "前兆：避让", "落石", "等待落点清空", "土石堆：矿镐清障", "已清障", "已退休" };
            if (SupportMarker != null) SupportMarker.enabled = ShowSupportMarker && Lab.Events[0].Capture().Phase == 0;
            if (RegionMarker != null) RegionMarker.enabled = Lab.Events[1].Capture().Phase == 0;
            string reason = (Lab.Events[0].Capture().Reason + " " + Lab.Events[1].Capture().Reason).Trim();
            Status.text = "CL-02 塌方 · 大小混合版" + (Lab.Paused ? "（已暂停）" : "") +
                "\n支撑触发：" + phases[Lab.Events[0].Capture().Phase] + "　区域触发：" + phases[Lab.Events[1].Capture().Phase] +
                "\n" + SupportHint() + "　" + RegionHint() +
                "\n" + (reason.Length > 0 ? reason : !Lab.Paused && !input.Allowed ?
                    "先点击Game视图获得操作焦点。" : "挖断起点右侧支撑岩或走进橙色区才触发；随后避让／清障。Esc暂停，重新Play重置。");
        }

        private string SupportHint()
        {
            var support = configurations[0].Support;
            var map = Lab.World.Terrain.Map;
            if (map.Read(support).Cell.IsEmpty) return "支撑岩已挖断";
            var target = Lab.MiningTarget;
            bool aimed = input.UseAllowed && target.Present && target.U == support.U && target.V == support.V;
            var state = map.Query(support);
            var tool = Lab.World.Resources.Equipment.Mining(Lab.Hero.Slot0Definition);
            int damage = Math.Max(1, map.Rules.PickaxeDamage(state.Sample.Cell.TileId, tool.Damage));
            int hits = (state.State.Durability + damage - 1) / damage;
            return "支撑岩 " + state.State.Durability + "/" + state.Definition.MaximumDurability +
                "（还需" + hits + "击）·" + (aimed ? "已瞄准，按住左键" : "未瞄准支撑岩");
        }

        private string RegionHint()
        {
            if (Lab.Events[1].Capture().Phase != 0) return "橙色区域已触发";
            var region = configurations[1];
            float left = (region.RegionU - .5f) * PlayableTerrain.CellPixels;
            float right = left + region.RegionWidth * PlayableTerrain.CellPixels;
            if (Lab.Hero.X < left) return "橙色区：向右 " + ((left - Lab.Hero.X) / 100).ToString("F1") + "m";
            if (Lab.Hero.X >= right) return "橙色区：向左 " + ((Lab.Hero.X - right) / 100).ToString("F1") + "m";
            return "橙色区：请回到通道地面";
        }

        private void OnDestroy()
        {
            if (Lab != null) Lab.World.Terrain.Map.Changed -= OnGeologyMapChanged;
            if (Lab != null) { Lab.Dispose(); Lab = null; }
            SessionInstance?.Release(); resources?.Dispose(); resources = null;
            if (localDefinition != null) Destroy(localDefinition);
        }

        private void OnGeologyMapChanged(GridChangeSet change) => TerrainView.NotifyReplicaChanged();

        private GridCell ProjectGeologyCell(CellCoord position, GridCell cell)
        {
            if (cell.IsEmpty) return cell;
            for (int index = 0; index < configurations.Length; index++)
            {
                var config = configurations[index];
                int left = config.LandingU + (config.Width - config.RubbleWidth) / 2;
                if (position.U < left || position.U >= left + config.RubbleWidth ||
                    position.V < config.LandingV || position.V >= config.LandingV + 6) continue;
                uint mask = Lab.Events[index].Capture().SoilMask;
                for (int i = 0; i < config.RubbleCount; i++)
                    if ((mask & 1u << i) != 0 && config.Landing(i).Equals(position)) return default;
            }
            return cell;
        }
    }
}
