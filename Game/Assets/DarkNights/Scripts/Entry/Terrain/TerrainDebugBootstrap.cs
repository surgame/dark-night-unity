using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.View.Terrain;
using GameCore.Objects.Definition;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkNights.Entry.Terrain
{
    /// <summary>
    /// 独立离线地图调试入口；当前洞穴工作台复用正式星球生成流水线，不启动营地会话或波次。
    /// 参数防抖后在后台生成，主线程完成真实 DualGrid 可见页才替换旧预览；退出取消并释放临时表现。
    /// </summary>
    public sealed class TerrainDebugBootstrap : MonoBehaviour
    {
        public ARDMapDefinition Definition;
        public CaveTerrainStyle CaveStyle;
        public ObjectDefinition MapAssemblySource;
        public string PlanetId = "";
        public string MapConfigBaseline { get; private set; }
        public TextAsset BalanceJson;
        public TextAsset LevelJson;
        public DarkNights.Runtime.Terrain.CaveWorkshopSession Workshop { get; private set; }
        public TerrainDebugFlyer Flyer;
        public TerrainGenerationSettings Settings = new TerrainGenerationSettings();
        public bool LiveRegenerate = true;
        public TerrainBlueprint Blueprint { get; private set; }
        public TerrainPreview Preview { get; private set; }
        public bool Generating { get; private set; }
        public int Generation { get; private set; }
        public string Status { get; private set; } = "准备生成房间地图…";
        public string LastError { get; private set; }
        public CaveStyleDraft StyleDraft { get; set; }
        private BackgroundBakeDescriptor backgroundReference;
        private bool stylePending;
        private float styleDue;
        private CancellationTokenSource lifetime;
        private string observedSettings;
        private bool pending;
        private float due;
        private int request;

        private void OnEnable()
        {
            if (MapAssemblySource == null) throw new InvalidOperationException("工作台必须绑定正式 WorldSession 地图配置。");
            var flow = MapAssemblySource.SharedConfigs.OfType<ExpeditionFlowConfig>().SingleOrDefault();
            Settings = flow?.FreezeCaveMap() ?? throw new InvalidOperationException("工作台缺少共用洞穴地图配置。");
            MapConfigBaseline = JsonUtility.ToJson(flow.CaveMap);
            lifetime = new CancellationTokenSource();
            observedSettings = JsonUtility.ToJson(Settings);
            RequestRegenerate();
        }

        /// <summary>主线程冻结资产输入；返回的后台任务与正式航程复用完整星球生成，不再跳过天空、平台或入口阶段。</summary>
        public Func<TerrainBlueprint> CaptureMapGenerator()
        {
            var settings = Settings.CopyValidated();
            if (settings.ResourceProfile != TerrainGenerationSettings.CaveExplorationProfile)
                throw new InvalidOperationException("正式工作台必须使用洞穴资源方案。");
            var config = MapAssemblySource.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            var planet = config.PreviewPlanet(PlanetId);
            var modifiers = config.FreezeModifiers();
            return () => PlanetTerrainGenerator.GenerateCandidate(planet, settings.Seed,
                "00000000000000000000000000000001", settings, pipeline: modifiers).Blueprint();
        }

        public void AcceptMapConfigSave() => MapConfigBaseline = JsonUtility.ToJson(Settings);

        public void RequestRegenerate()
        {
            pending = true; due = Time.unscaledTime; request++;
        }

        /// <summary>只替换表现宿主，保留当前权威格子、人物与初始背景参考；调参不重置已拆填的地图。</summary>
        public void RequestStyleRefresh()
        { stylePending = true; styleDue = Time.unscaledTime + .4f; }

        public void NewSeed()
        {
            Settings.Seed = "DEBUG-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            RequestRegenerate();
        }

        public void VisitRoom(int index)
        {
            if (Blueprint == null || index < 0 || index >= Blueprint.Rooms.Count) return;
            var room = Blueprint.Rooms[index];
            Workshop?.Teleport(room.X, -room.Y);
            Flyer.Teleport(new Vector2(room.X, -room.Y));
        }

        private void Update()
        {
            string current = JsonUtility.ToJson(Settings);
            if (current != observedSettings)
            {
                observedSettings = current;
                if (LiveRegenerate) { RequestRegenerate(); due = Time.unscaledTime + .35f; }
            }
            var keyboard = Keyboard.current;
            if (Flyer != null && !Flyer.InputBlocked && keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) NewSeed();
                if (keyboard.fKey.wasPressedThisFrame) VisitRoom(0);
            }
            if (pending && !Generating && Time.unscaledTime >= due) GenerateAsync(false);
            else if (stylePending && !Generating && Preview != null && Time.unscaledTime >= styleDue) GenerateAsync(true);
        }

        private async void GenerateAsync(bool appearanceOnly)
        {
            if (!appearanceOnly) pending = false;
            stylePending = false; Generating = true; LastError = null;
            int version = request;
            CancellationToken token = lifetime.Token;
            TerrainPreview candidate = null;
            DarkNights.Runtime.Terrain.CaveWorkshopSession workshop = null;
            CaveTerrainStyle capturedStyle = null;
            CaveBackgroundStyle capturedBackground = null;
            try
            {
                if (Definition == null || Flyer == null || Flyer.ViewCamera == null)
                    throw new InvalidOperationException("Debug Bootstrap 缺少明确的地形、角色或镜头引用。");
                var settings = Settings.CopyValidated();
                Status = "生成中：" + settings.Seed;
                var generate = appearanceOnly ? null : CaptureMapGenerator();
                var blueprint = appearanceOnly ? Blueprint : await Task.Run(generate, token);
                token.ThrowIfCancellationRequested();
                if (version != request) return;
                var root = new GameObject("Generated room terrain");
                root.transform.SetParent(transform, false);
                candidate = root.AddComponent<TerrainPreview>();
                candidate.ViewCamera = Flyer.ViewCamera;
                capturedStyle = StyleDraft?.Capture(out capturedBackground);
                var presentationStyle = capturedStyle != null ? capturedStyle : CaveStyle;
                var reference = appearanceOnly ? backgroundReference : null;
                if (CaveStyle != null)
                {
                    if (!appearanceOnly)
                    {
                        var game = DarkNights.Runtime.Config.GameCatalogJson.Parse(BalanceJson.text, LevelJson.text);
                        var profile = MapAssemblySource.SharedConfigs.OfType<DarkNights.Runtime.Terrain.TerrainProfileConfig>().Single();
                        var tools = MapAssemblySource.SharedConfigs.OfType<DarkNights.Runtime.Objects.HandheldConfig>().Single();
                        var terrainRules = profile.Freeze(Definition);
                        workshop = new DarkNights.Runtime.Terrain.CaveWorkshopSession(blueprint, terrainRules.Business.Gameplay, game,
                            terrainRules, tools);
                        reference = new BackgroundBakeDescriptor(workshop.Map.World.WorldId.ToString().Replace("-", ""),
                            blueprint.Settings.Seed, blueprint.CopyMaterials(), blueprint.CopyShapes());
                    }
                    var authority = appearanceOnly ? Workshop.Map : workshop.Map;
                    candidate.ShowCaveReplica(Definition, presentationStyle, new TerrainReplicaSource(authority), authority.World, reference);
                }
                else candidate.ShowBlueprint(Definition, blueprint);
                float deadline = Time.realtimeSinceStartup + 30;
                while (!candidate.Ready)
                {
                    token.ThrowIfCancellationRequested();
                    if (version != request) return;
                    if (candidate.LastError != null) throw candidate.LastError;
                    if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("地图可见页未在 30 秒内就绪：" + candidate.PresentationWaitReason);
                    await Task.Yield();
                }
                token.ThrowIfCancellationRequested();
                if (version != request) return;
                Release(Preview);
                if (!appearanceOnly) { Workshop?.Dispose(); Workshop = workshop; workshop = null; backgroundReference = reference; }
                Blueprint = blueprint; Preview = candidate; candidate = null;
                Preview.SetMinerals(blueprint.Deposits.Select((d, i) => new DarkNights.Core.ViewData.MineralDepositViewData(
                    i + 1, (d.X + .5f) * 16, d.Y, d.RoomKind, d.Rarity, d.MineralKind, 40, 1, 1,
                    d.Cells.Select(cell => new DarkNights.Core.ViewData.MineralCellViewData(cell.U, cell.V, cell.Capacity, cell.Capacity, 40, 1)).ToArray())).ToArray());
                Flyer.Ready = true;
                if (!appearanceOnly) { Generation++; VisitRoom(0); }
                Status = settings.Seed + " · " + blueprint.Rooms.Count + " 洞室 / " +
                    blueprint.Passages.Count + " 通路 · 第 " + Generation + " 次生成";
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                LastError = error.Message; Status = "生成失败：" + error.Message;
                Debug.LogException(error, this);
            }
            finally
            {
                Release(candidate); workshop?.Dispose(); Generating = false;
                CaveStyleDraft.Release(capturedStyle); CaveStyleDraft.Release(capturedBackground);
            }
        }

        private void Release(TerrainPreview preview)
        {
            if (preview == null) return;
            preview.gameObject.SetActive(false);
            Destroy(preview.gameObject);
        }

        private void OnDisable()
        {
            lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
            Workshop?.Dispose(); Workshop = null;
            pending = false; Release(Preview); Preview = null; Blueprint = null;
            stylePending = false; backgroundReference = null;
            if (Flyer != null) Flyer.Ready = false;
        }
    }
}
