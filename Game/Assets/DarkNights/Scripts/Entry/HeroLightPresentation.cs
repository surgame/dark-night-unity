using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using DarkNights.View.Lighting;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.Entry
{
    /// <summary>正式会话的照明装配桥；复用真实角色上下文创建工具，把冻结投影传入 View，切世界先撤销注册。</summary>
    public sealed class HeroLightPresentation : MonoBehaviour
    {
        private SessionNetwork network;
        private SessionEntityViews entities;
        private GameInputActions input;
        private Terrain.RandomLevelEntry terrain;
        private readonly Dictionary<int,(ActorView Actor,FlashlightView Tool)> tools = new Dictionary<int,(ActorView,FlashlightView)>();
        private readonly List<LightEmitterData> emitters = new List<LightEmitterData>(16);
        private readonly List<LightEffect> effects = new List<LightEffect>(16);
        private readonly LightFillComposer fills = new LightFillComposer();
        private readonly List<Task> retiring = new List<Task>();
        private readonly HashSet<int> wanted = new HashSet<int>();
        private ExplorationLightField field;
        private View.Terrain.CaveVisualSource boundSource;
        private int epoch;
        private bool failed;
        private static readonly LightEmissionRules DeviceLight = new LightEmissionRules(8.375f,90,.45f,.25f,0,1,.78f,.48f);
        public ExplorationLightSettings Settings => field?.Settings;
        public int SourceCount => field?.SourceCount ?? 0;
        public Task Retirement { get; private set; } = Task.CompletedTask;

        public void Initialize(SessionNetwork session, SessionEntityViews views, GameInputActions actions, Terrain.RandomLevelEntry entry)
        {
            network = session; entities = views; input = actions; terrain = entry;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            var preview = terrain?.Preview;
            if (preview == null || camera != preview.ViewCamera) { Shader.SetGlobalFloat("_DNLightingActive",0); return; }
            if (failed || !network.Client.Ready) { ExplorationLightField.Suspend(); return; }
            try { Present(camera,preview); }
            catch (Exception error) { failed = true; ExplorationLightField.Suspend(); Debug.LogException(error,this); }
        }

        private void Present(Camera camera, View.Terrain.TerrainPreview preview)
        {
            var frame = network.Client.Replica.Current;
            if (frame == null || preview.LightingSource == null) { ExplorationLightField.Suspend(); return; }
            if (epoch != frame.Epoch) { ClearTools(); epoch = frame.Epoch; }
            if (boundSource != preview.LightingSource)
            {
                if (field != null) retiring.Add(field.RetireAsync());
                field = null; boundSource = preview.LightingSource;
            }
            retiring.RemoveAll(task => task.IsCompletedSuccessfully);
            emitters.Clear(); wanted.Clear();
            foreach (var observed in frame.World.Actors)
            {
                var actor = entities.Visual(observed.Id) as ActorView;
                var displayed = (entities.Presentation(observed.Id) as ActorPresentationBehaviour)?.Current ?? observed;
                if (actor == null || displayed.LightDefinition == "" || displayed.Hp <= 0) continue;
                if (!network.ObjectResources.Equipment.HasLight(displayed.LightDefinition) || actor.LightAnchor == null)
                    throw new InvalidOperationException("角色照明道具能力或明确挂点缺失。");
                wanted.Add(displayed.Id);
                if (!tools.TryGetValue(displayed.Id,out var binding) || binding.Actor != actor || binding.Tool.Owner.Definition.Guid.ToString() != displayed.LightDefinition)
                {
                    Remove(displayed.Id);
                    var definition = network.ObjectResources.Equipment.Resolve(displayed.LightDefinition);
                    var created = network.ObjectResources.Create(definition,actor.Owner.SessionContext,actor.LightAnchor);
                    try
                    {
                        var tool = created as FlashlightView ?? throw new InvalidOperationException("手电主视图类型非法。");
                        if (tool.Owner.GetBehaviour<FlashlightToolBehaviour>()?.Ready != true) throw new InvalidOperationException("手电 Behaviour 未装配。");
                        tool.transform.localPosition = Vector3.zero; tool.transform.localScale = Vector3.one;
                        tool.Owner.Activate(); binding = (actor,tool); tools.Add(displayed.Id,binding);
                        tool.BindLight(this, actor.LightAnchor, actor.LightFillAnchor, actor.LightTargets);
                    }
                    catch { EntityViewFactory.Release(created); throw; }
                }
                float angle = displayed.LightAimAngle;
                bool local = displayed.ControllerSlot == network.Client.PlayerSlot && displayed.ControlLease > 0;
                if (local && input.HeroMode && !frame.Paused && input.ReadHero().Allowed)
                {
                    Vector3 pointer = camera.ScreenToWorldPoint(new Vector3(input.Pointer.x,input.Pointer.y,camera.WorldToScreenPoint(Vector3.zero).z));
                    Vector3 aim = pointer - actor.LightAnchor.position;
                    if (aim.sqrMagnitude > .0001f) angle = Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg;
                }
                binding.Tool.Present(angle,true,local); angle = binding.Tool.DisplayedAngle;
                bool boarded = false;
                if (frame.World.Expedition != null) foreach (var crew in frame.World.Expedition.Crew)
                    if (crew.Id == displayed.Id) { boarded = crew.Boarded; break; }
                binding.Tool.SetLight(displayed.LightEnabled && !boarded);
            }
            var removed = new List<int>();
            foreach (int id in tools.Keys) if (!wanted.Contains(id)) removed.Add(id);
            foreach (int id in removed) Remove(id);
            LightEffect.Collect(this, camera, effects);
            foreach (var effect in effects)
            {
                field = field ?? new ExplorationLightField(effect.Environment.LightingShader, Core.Logic.Lighting.LightWallDistance.Build);
                if (effect.IsOn && effect.Environment.isActiveAndEnabled) emitters.Add(effect.Environment.Sample(preview));
            }
            fills.Apply(effects, field?.Settings.NearStrength ?? 1,
                new Vector2(preview.transform.lossyScale.x, preview.transform.lossyScale.y));
            AddDeviceLights(frame.World);
            field?.Render(camera,preview,emitters);
        }

        private void AddDeviceLights(Core.ViewData.WorldViewData world)
        {
            if (world.Expedition == null) return;
            foreach (var building in world.Buildings)
            {
                if (building.Kind != "ship" && building.Kind != "lamp") continue;
                foreach (var device in world.Expedition.Devices)
                    if (device.Id == building.Id && device.Powered && entities.Visual(building.Id) is EntityView visual)
                    {
                        emitters.Add(new LightEmitterData(visual.transform.position+Vector3.up*.16f,0,DeviceLight,false));
                    }
            }
        }

        private void Remove(int id)
        {
            if (!tools.TryGetValue(id,out var binding)) return;
            tools.Remove(id); EntityViewFactory.Release(binding.Tool);
        }
        private void ClearTools()
        { foreach (var binding in tools.Values) EntityViewFactory.Release(binding.Tool); tools.Clear(); }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            ClearTools(); boundSource = null; ExplorationLightField.Suspend();
            fills.Clear(); effects.Clear();
            if (field != null) retiring.Add(field.RetireAsync()); field = null;
            Retirement = RetirePendingAsync();
        }
        private async Task RetirePendingAsync()
        {
            try { await Task.WhenAll(retiring); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
