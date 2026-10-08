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
        private readonly List<FlashlightEmitterData> emitters = new List<FlashlightEmitterData>(16);
        private readonly List<Task> retiring = new List<Task>();
        private readonly HashSet<int> wanted = new HashSet<int>();
        private ExplorationLightField field;
        private View.Terrain.CaveVisualSource boundSource;
        private int epoch;
        private bool failed;
        private static readonly FlashlightRules DeviceLight = new FlashlightRules(8.375f,90,.45f,.25f,0,1,.78f,.48f);
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
                var rules = network.ObjectResources.Equipment.Light(displayed.LightDefinition);
                if (rules == null || actor.LightAnchor == null) throw new InvalidOperationException("角色手电能力或明确挂点缺失。");
                wanted.Add(displayed.Id);
                if (!tools.TryGetValue(displayed.Id,out var binding) || binding.Actor != actor || binding.Tool.Owner.Definition.Guid.ToString() != displayed.LightDefinition)
                {
                    Remove(displayed.Id);
                    var definition = network.ObjectResources.Equipment.Resolve(displayed.LightDefinition);
                    var created = network.ObjectResources.Create(definition,actor.Owner.SessionContext,actor.LightAnchor);
                    try
                    {
                        var tool = created as FlashlightView ?? throw new InvalidOperationException("手电主视图类型非法。");
                        if (tool.Owner.GetBehaviour<FlashlightToolBehaviour>()?.Rules == null) throw new InvalidOperationException("手电 Behaviour 未装配。");
                        tool.transform.localPosition = Vector3.zero; tool.transform.localScale = Vector3.one;
                        tool.Owner.Activate(); binding = (actor,tool); tools.Add(displayed.Id,binding);
                    }
                    catch { EntityViewFactory.Release(created); throw; }
                }
                field = field ?? new ExplorationLightField(binding.Tool.LightingShader,Core.Logic.Lighting.LightWallDistance.Build);
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
                if (displayed.LightEnabled && !boarded)
                    emitters.Add(new FlashlightEmitterData(SafeEmitter(preview,actor.LightAnchor.position,binding.Tool.EmitterPosition),
                        angle,rules,nearPosition:actor.transform.position+Vector3.up*.22f));
            }
            var removed = new List<int>();
            foreach (int id in tools.Keys) if (!wanted.Contains(id)) removed.Add(id);
            foreach (int id in removed) Remove(id);
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
                        emitters.Add(new FlashlightEmitterData(visual.transform.position+Vector3.up*.16f,0,DeviceLight,false));
                    }
            }
        }

        private void Remove(int id)
        {
            if (!tools.TryGetValue(id,out var binding)) return;
            tools.Remove(id); EntityViewFactory.Release(binding.Tool);
        }
        private static Vector3 SafeEmitter(View.Terrain.TerrainPreview preview,Vector3 mount,Vector3 emitter)
        {
            Vector3 previous=mount;
            for (int n=0;n<=8;n++)
            {
                Vector3 sample=Vector3.Lerp(mount,emitter,n/8f);
                Vector3 local=preview.transform.InverseTransformPoint(sample);
                float x=local.x+.5f,y=-local.y+.5f;
                byte cell=preview.LightingSource.LightingCell(Mathf.FloorToInt(x),Mathf.FloorToInt(y));
                if ((cell&128)==0 || (cell&64)!=0 && Core.Logic.Terrain.TerrainShapeGeometry.Contains(
                    (Core.Config.Terrain.TerrainCellShape)(cell&15),x-Mathf.Floor(x),1-(y-Mathf.Floor(y)))) return previous;
                previous=sample;
            }
            return previous;
        }
        private void ClearTools()
        { foreach (var binding in tools.Values) EntityViewFactory.Release(binding.Tool); tools.Clear(); }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            ClearTools(); boundSource = null; ExplorationLightField.Suspend();
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
