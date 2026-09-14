using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.View;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Entry
{
    /// <summary>
    /// 将世界表现事件、箭矢和本地输入反馈接入原生 Prefab；指令圈独立池化，不进入事件游标或网络。
    /// 工厂创建跨 await 检查连接代次和 epoch，旧世界结果立即释放，重复快照不重复播放。
    /// </summary>
    public sealed class SessionEffects : MonoBehaviour
    {
        private readonly PresentationCursor cursor = new PresentationCursor();
        private readonly LocalCommandRings commandRings = new LocalCommandRings();
        private readonly Dictionary<long, (ObjectView Owner, NativeEffect Effect)> arrows =
            new Dictionary<long, (ObjectView, NativeEffect)>();
        private readonly HashSet<long> pendingArrows = new HashSet<long>();
        private readonly List<(ObjectView Owner, NativeEffect Effect, EntityView Remnant, PresentationEvent Event, double Born)> effects =
            new List<(ObjectView, NativeEffect, EntityView, PresentationEvent, double)>();
        private SessionClient client;
        private SessionUiController ui;
        private PinewatchStage stage;
        private ObjectView audioOwner;
        private CampAudio audioView;
        private SessionViewData applied;
        private double received;
        private int epoch, generation;
        private long connection;
        private float ground;
        public int EffectCount => effects.Count + commandRings.Count;
        public int ArrowCount => arrows.Count;
        public int CommandRingCount => commandRings.Count;
        public int CommandRingInstanceCount => commandRings.InstanceCount;
        public long CommandRingPresentationCount => commandRings.PresentationCount;

        public async UniTask Initialize(SessionClient value, SessionUiController panels, PinewatchStage scene, float groundY)
        {
            client = value; ui = panels; stage = scene; ground = groundY;
            audioOwner = await Create("audio.camp");
            if (this == null) { Release(audioOwner); return; }
            audioView = Required<CampAudio>(audioOwner, "audio");
            await commandRings.Initialize(ObjectDefinitionDatabase.Instance.GetDefinitionByKey("effect.command"), stage.Entities, ground);
            if (this != null) ui.Input.Intent += LocalIntent;
        }

        private void Update()
        {
            if (client == null || audioView == null) return;
            SessionViewData frame = client.Replica.Current;
            SynchronizeWorld();
            if (frame == null) return;
            double now = Time.unscaledTimeAsDouble;
            commandRings.Tick(now);
            if (applied != frame)
            {
                applied = frame; received = now;
                foreach (PresentationEvent item in cursor.Consume(frame))
                {
                    double age = PresentationCursor.Age(frame, item);
                    if (item.Type == "sound") audioView.Play(item.Text, item.Volume);
                    else if (item.Type == "effect") Spawn(item, now - age, generation).Forget();
                    else ui.PresentEvent(item, age);
                }
                var ids = new HashSet<long>(frame.World.Projectiles.Select(p => p.ViewId));
                foreach (long id in arrows.Keys.ToArray()) if (!ids.Contains(id))
                {
                    Release(arrows[id].Owner); arrows.Remove(id);
                }
                foreach (long id in ids)
                    if (!arrows.ContainsKey(id) && pendingArrows.Add(id)) SpawnArrow(id, generation).Forget();
            }
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i];
                double age = now - effect.Born;
                if (age >= PresentationCursor.Lifetime(effect.Event)) { Release(effect.Owner); effects.RemoveAt(i); continue; }
                if (effect.Remnant != null)
                {
                    effect.Remnant.Ambient = stage.Ambient;
                    RequiredRemnant(effect.Remnant).PresentRemnant(effect.Event.Cue, age);
                }
                else effect.Effect.Present(effect.Event.Cue, age, ground, stage.IlluminationAt(effect.Owner.transform.position));
            }
            foreach (ProjectileViewData arrow in frame.World.Projectiles)
                if (arrows.TryGetValue(arrow.ViewId, out var view))
                {
                    double age = arrow.Age + (frame.Paused || frame.Loading ? 0 : Math.Min(now - received, 0.1) * frame.Speed);
                    view.Effect.Present(arrow, age, ground);
                }
        }

        private async UniTask Spawn(PresentationEvent item, double born, int captured)
        {
            ObjectView owner = null;
            try
            {
                bool remnant = item.Cue.Kind == "corpse" || item.Cue.Kind == "rubble";
                owner = remnant ? await EntityViewFactory.Create(item.Cue.ContentId, stage.Entities) :
                    await Create("effect.floating");
                if (!Current(captured) || Time.unscaledTimeAsDouble - born >= PresentationCursor.Lifetime(item)) { Release(owner); return; }
                // 残骸复用同一定义外观，但从不绑定已消失的活实体。
                EntityView visual = remnant ? EntityViewFactory.RequiredPresentation((EntityView)owner).Visual : null;
                if (visual != null) RequiredRemnant(visual);
                NativeEffect effect = remnant ? null : Required<NativeEffect>(owner, "effect");
                owner.transform.position = new Vector3(item.Cue.X / 100, (ground - item.Cue.Y) / 100, 0);
                effects.Add((owner, effect, visual, item, born));
            }
            catch (Exception error) { Release(owner); Debug.LogException(error); }
        }

        internal void SamplePresentation(double age)
        {
            commandRings.SamplePresentation(age);
            foreach (var item in effects)
            {
                if (item.Remnant != null)
                {
                    item.Remnant.Ambient = stage.Ambient;
                    RequiredRemnant(item.Remnant).PresentRemnant(item.Event.Cue, age);
                }
                else item.Effect.Present(item.Event.Cue, age, ground, stage.IlluminationAt(item.Owner.transform.position));
            }
        }

        private async UniTask SpawnArrow(long id, int captured)
        {
            ObjectView owner = null;
            try
            {
                owner = await Create("effect.arrow");
                if (!Current(captured) || !client.Replica.Current.World.Projectiles.Any(p => p.ViewId == id)) { Release(owner); return; }
                arrows.Add(id, (owner, Required<NativeEffect>(owner, "effect")));
            }
            catch (Exception error) { Release(owner); Debug.LogException(error); }
            finally { if (captured == generation) pendingArrows.Remove(id); }
        }

        private UniTask<ObjectView> Create(string key)
        {
            var definition = ObjectDefinitionDatabase.Instance.GetDefinitionByKey(key);
            if (definition == null) throw new InvalidOperationException("Missing native effect definition: " + key);
            return ObjectInstanceFactory.CreateObjectInstanceAsync(new DefinitionReference(definition.Guid).Resolve(), Vector3.zero, Quaternion.identity, stage.Entities);
        }
        private static T Required<T>(ObjectView owner, string key) where T : Component =>
            owner != null && owner.Get<T>(key) != null ? owner.Get<T>(key) : throw new InvalidOperationException("Missing native effect binding: " + key);
        private static IRemnantView RequiredRemnant(EntityView view) => view is IRemnantView remnant ? remnant :
            throw new InvalidOperationException("Entity view cannot present a remnant: " + view.GetType().Name);
        private bool Current(int captured) => this != null && generation == captured &&
            client.ConnectionGeneration == connection && client.Replica.Current?.Epoch == epoch;
        private static void Release(ObjectView owner) => EntityViewFactory.Release(owner);

        private void LocalIntent(InputIntent intent)
        {
            SessionViewData frame = client.Replica.Current;
            if (intent.Action != "Orders" || intent.Actors.Length == 0 || !client.Ready || frame == null ||
                frame.Loading || frame.World.Camp.Mode != "Playing" || (frame.HostOnly && client.PlayerSlot != 0)) return;
            PresentLocalCommand(intent.X);
        }

        internal void PresentLocalCommand(float x)
        {
            SynchronizeWorld();
            commandRings.Show(x, Time.unscaledTimeAsDouble);
        }

        private void SynchronizeWorld()
        {
            int currentEpoch = client.Replica.Current?.Epoch ?? 0;
            if (connection == client.ConnectionGeneration && epoch == currentEpoch) return;
            Clear();
            connection = client.ConnectionGeneration;
            epoch = currentEpoch;
        }

        private void Clear()
        {
            generation++; cursor.Reset(); applied = null;
            commandRings.Clear();
            foreach (var value in arrows.Values) Release(value.Owner);
            foreach (var value in effects) Release(value.Owner);
            arrows.Clear(); pendingArrows.Clear(); effects.Clear();
        }
        private void OnDestroy()
        {
            if (ui != null && ui.Input != null) ui.Input.Intent -= LocalIntent;
            Clear(); commandRings.Dispose(); Release(audioOwner);
        }
    }
}
