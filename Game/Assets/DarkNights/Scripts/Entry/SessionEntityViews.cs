using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Entry
{
    /// <summary>
    /// 把冻结副本分发给会话已经装配的对象，管理展示时间线和本地输入绑定。
    /// 对象的创建、转职和退休由会话负责；缓存核对真实所有者，避免归池后的旧表现引用干扰新对象。
    /// </summary>
    public sealed class SessionEntityViews : MonoBehaviour, IEntityVisuals
    {
        private SessionClient client;
        private SessionNetwork network;
        private GameCatalog catalog;
        private PinewatchStage stage;
        private Action<InputIntent> intentHandler;
        private readonly Dictionary<int, (EntityView Owner, EntityPresentationBehaviour Presentation, string Kind)> views =
            new Dictionary<int, (EntityView, EntityPresentationBehaviour, string)>();
        private readonly Dictionary<int, string> workKinds = new Dictionary<int, string>();
        private int epoch, generation;
        private long connection;
        private SessionViewData applied;
        private readonly PresentationTimeline timeline = new PresentationTimeline();
        public int Count => views.Count;
        public EntityView Visual(int id) => Presentation(id)?.Visual;
        public EntityPresentationBehaviour Presentation(int id) => views.TryGetValue(id, out var view) &&
            view.Owner != null && view.Presentation.Owner == view.Owner.Owner && view.Presentation.Id == id &&
            view.Presentation.Epoch == epoch ? view.Presentation : null;
        public void SetIntentHandler(Action<InputIntent> handler) { intentHandler = handler; }

        public void Initialize(SessionClient value, GameCatalog rules, PinewatchStage scene, SessionNetwork session)
        {
            network = session ?? throw new ArgumentNullException(nameof(session));
            client = value;
            catalog = rules;
            stage = scene;
        }

        private void Update()
        {
            if (client == null) return;
            SessionViewData frame = client.Replica.Current;
            if (connection != client.ConnectionGeneration || epoch != (frame?.Epoch ?? 0))
            {
                Clear();
                connection = client.ConnectionGeneration;
                epoch = frame?.Epoch ?? 0;
            }
            stage.Present(frame);
            if (frame == null) return;
            if (frame != applied) ApplyFrame(frame);
            Present(frame);
        }

        private void ApplyFrame(SessionViewData frame)
        {
            timeline.Push(frame, Time.unscaledTimeAsDouble);
            var wanted = frame.World.Actors.Select(a => (a.Id, a.Kind))
                .Concat(frame.World.Buildings.Select(b => (b.Id, b.Kind)))
                .Concat(frame.World.Worksites.Select(w => (w.Id, w.Kind))).ToDictionary(p => p.Id, p => p.Kind);
            workKinds.Clear();
            foreach (WorksiteViewData site in frame.World.Worksites) workKinds.Add(site.Id, site.Kind);
            foreach (int id in views.Keys.ToArray())
                if (!wanted.ContainsKey(id)) Remove(id);
            foreach (var pair in wanted)
            {
                EntityView owner = FindOwner(pair.Key, pair.Value);
                if (views.TryGetValue(pair.Key, out var previous))
                {
                    if (previous.Owner == owner && previous.Kind == pair.Value && Presentation(pair.Key) != null) continue;
                    Remove(pair.Key);
                }
                // Host 的实时对象可能比最近的 10 Hz 投影先退休；等待下一帧的相应身份。
                if (owner == null) continue;
                EntityPresentationBehaviour presentation = EntityViewFactory.RequiredPresentation(owner);
                int captured = generation;
                Action<InputIntent> submit = intent => Submit(presentation, captured, intent);
                if (catalog.Balance.Units.TryGetValue(pair.Value, out UnitDefinition rules) && presentation is ActorPresentationBehaviour actor)
                    actor.Bind(pair.Key, epoch, pair.Value, rules, submit);
                else if (catalog.Balance.Buildings.ContainsKey(pair.Value) && presentation is BuildingPresentationBehaviour building)
                    building.Bind(pair.Key, epoch, pair.Value, submit);
                else if (pair.Value == "mineral-deposit" && presentation is MineralDepositPresentationBehaviour deposit)
                    deposit.Bind(pair.Key, epoch, pair.Value, submit);
                else if (catalog.Balance.Worksites.ContainsKey(pair.Value) && presentation is WorksitePresentationBehaviour site)
                    site.Bind(pair.Key, epoch, pair.Value, submit);
                else throw new InvalidOperationException("Unsupported entity presentation: " + pair.Value);
                views.Add(pair.Key, (owner, presentation, pair.Value));
                owner.gameObject.name = pair.Value + " #" + pair.Key;
            }
            applied = frame;
        }

        private EntityView FindOwner(int id, string kind)
        {
            if (!network.Hosting) return network.ReplicaObjects.View(id) as EntityView;
            IEntityBehaviour entity = network.ObjectWorld?.Index.Find(id);
            return entity?.RuleKey == kind ? entity.Object.GetView<EntityView>() : null;
        }

        private void Submit(EntityPresentationBehaviour presentation, int captured, InputIntent intent)
        {
            if (this == null || captured != generation || connection != client.ConnectionGeneration ||
                presentation.Epoch != client.Replica.Current?.Epoch || !client.Ready || Presentation(presentation.Id) != presentation) return;
            intentHandler?.Invoke(intent);
        }

        private void Present(SessionViewData frame)
        {
            double now = Time.unscaledTimeAsDouble;
            foreach (ActorViewData actor in frame.World.Actors)
                if (Presentation(actor.Id) is ActorPresentationBehaviour view)
                {
                    workKinds.TryGetValue(actor.TargetId, out string kind);
                    view.Present(actor, frame.Epoch, kind ?? "", timeline.X(actor, now), timeline.ActionTime(actor, now), stage.Ambient, timeline.Height(actor, now));
                }
            foreach (BuildingViewData building in frame.World.Buildings)
                if (Presentation(building.Id) is BuildingPresentationBehaviour view) view.Present(building, frame.Epoch, stage.Ambient);
            foreach (WorksiteViewData site in frame.World.Worksites)
            {
                if (site.IsMineralDeposit || site.Kind == "mineral-deposit")
                {
                    if (Presentation(site.Id) is MineralDepositPresentationBehaviour deposit)
                        deposit.Present(site, frame.Epoch, stage.Ambient);
                }
                else if (Presentation(site.Id) is WorksitePresentationBehaviour view) view.Present(site, frame.Epoch, stage.Ambient);
            }
        }

        internal void SamplePresentation()
        {
            SessionViewData frame = client.Replica.Current;
            if (frame != null) Present(frame);
        }

        private void Remove(int id)
        {
            EntityPresentationBehaviour presentation = Presentation(id);
            presentation?.Unbind();
            views.Remove(id);
        }

        private void Clear()
        {
            generation++;
            foreach (int id in views.Keys.ToArray()) Remove(id);
            workKinds.Clear();
            applied = null;
            timeline.Reset();
        }

        private void OnDestroy() { intentHandler = null; Clear(); }
    }
}
