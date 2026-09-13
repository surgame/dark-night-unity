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
    /// 将冻结表现事件和箭矢轨迹接入本地原生 Prefab；仅事件序号和显示年龄可写，不接触权威世界。
    /// 工厂创建跨 await 检查连接代次和 epoch，旧世界结果立即释放，重复快照不重复播放。
    /// </summary>
    public sealed class SessionEffects : MonoBehaviour
    {
        private readonly PresentationCursor cursor = new PresentationCursor();
        private readonly Dictionary<long, (ObjectView Owner, NativeEffect Effect)> arrows =
            new Dictionary<long, (ObjectView, NativeEffect)>();
        private readonly HashSet<long> pendingArrows = new HashSet<long>();
        private readonly List<(ObjectView Owner, NativeEffect Effect, NativeVisual Remnant, PresentationEvent Event, double Born)> effects =
            new List<(ObjectView, NativeEffect, NativeVisual, PresentationEvent, double)>();
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
        public int EffectCount => effects.Count;
        public int ArrowCount => arrows.Count;

        public async UniTask Initialize(SessionClient value, SessionUiController panels, PinewatchStage scene, float groundY)
        {
            client = value; ui = panels; stage = scene; ground = groundY;
            audioOwner = await Create("audio.camp");
            if (this == null) { if (audioOwner != null) Destroy(audioOwner.gameObject); return; }
            audioView = Required<CampAudio>(audioOwner, "audio");
        }

        private void Update()
        {
            if (client == null || audioView == null) return;
            SessionViewData frame = client.Replica.Current;
            if (connection != client.ConnectionGeneration || epoch != (frame?.Epoch ?? 0))
            {
                Clear(); connection = client.ConnectionGeneration; epoch = frame?.Epoch ?? 0;
            }
            if (frame == null) return;
            double now = Time.unscaledTimeAsDouble;
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
                    effect.Remnant.PresentRemnant(effect.Event.Cue, age);
                }
                else effect.Effect.Present(effect.Event.Cue, age, ground);
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
                owner = remnant ? await NativeVisualFactory.Create(item.Cue.ContentId, stage.Entities) :
                    await Create(item.Cue.Kind == "command" ? "effect.command" : "effect.floating");
                if (!Current(captured) || Time.unscaledTimeAsDouble - born >= PresentationCursor.Lifetime(item)) { Release(owner); return; }
                // 残骸复用同一定义外观，但从不绑定已消失的活实体。
                NativeVisual visual = remnant ? NativeVisualFactory.RequiredPresentation(owner).Visual : null;
                NativeEffect effect = remnant ? null : Required<NativeEffect>(owner, "effect");
                owner.transform.position = new Vector3(item.Cue.X / 100, (ground - item.Cue.Y) / 100, 0);
                effects.Add((owner, effect, visual, item, born));
            }
            catch (Exception error) { Release(owner); Debug.LogException(error); }
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
        private bool Current(int captured) => this != null && generation == captured &&
            client.ConnectionGeneration == connection && client.Replica.Current?.Epoch == epoch;
        private static void Release(ObjectView owner) => NativeVisualFactory.Release(owner);
        private void Clear()
        {
            generation++; cursor.Reset(); applied = null;
            foreach (var value in arrows.Values) Release(value.Owner);
            foreach (var value in effects) Release(value.Owner);
            arrows.Clear(); pendingArrows.Clear(); effects.Clear();
        }
        private void OnDestroy() { Clear(); Release(audioOwner); }
    }
}
