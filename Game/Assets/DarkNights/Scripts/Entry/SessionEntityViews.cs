using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Framework;
using DarkNights.Runtime.Network;
using DarkNights.View;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Entry
{
    /// <summary>
    /// 登记定义工厂创建的本地个体，管理公共展示时间线并把冻结副本分发给各自的表现 Behaviour。
    /// 异步创建按连接、epoch 和每次外观请求验票；退出、转职和旧结果释放前立即解绑，不持有权威状态。
    /// </summary>
    public sealed class SessionEntityViews : MonoBehaviour, IEntityVisuals
    {
        private SessionClient client;
        private SessionNetwork network;
        private GameCatalog catalog;
        private PinewatchStage stage;
        private DefinitionRuleIndex definitions;
        private SceneEntityViews sceneViews;
        private Action<InputIntent> intentHandler;
        private readonly Dictionary<int, (ObjectView Owner, EntityPresentationBehaviour Presentation, string Kind)> views =
            new Dictionary<int, (ObjectView, EntityPresentationBehaviour, string)>();
        private readonly Dictionary<int, (string Kind, long Ticket)> pending = new Dictionary<int, (string, long)>();
        private readonly Dictionary<int, string> wanted = new Dictionary<int, string>();
        private readonly Dictionary<int, string> workKinds = new Dictionary<int, string>();
        private int epoch, generation;
        private long connection, nextTicket;
        private SessionViewData applied;
        private readonly PresentationTimeline timeline = new PresentationTimeline();
        public int Count => views.Count;
        public NativeVisual Visual(int id) => Presentation(id)?.Visual;
        public EntityPresentationBehaviour Presentation(int id) => views.TryGetValue(id, out var view) ? view.Presentation : null;
        public void SetIntentHandler(Action<InputIntent> handler) { intentHandler = handler; }

        public async UniTask<ObjectView> CreateVisual(string kind)
        {
            ObjectView owner = await ObjectInstanceFactory.CreateObjectInstanceAsync(
                definitions.GetRequired(kind), Vector3.zero, Quaternion.identity, stage.Entities);
            try
            {
                EntityPresentationBehaviour presentation = RequiredPresentation(owner);
                if (presentation.IsBound) throw new InvalidOperationException("New visual already has a live entity: " + kind);
                return owner;
            }
            catch { Release(owner); throw; }
        }

        public static EntityPresentationBehaviour RequiredPresentation(ObjectView owner)
        {
            if (owner == null || owner.Owner == null) throw new InvalidOperationException("Native visual owner is not assembled.");
            EntityPresentationBehaviour value = owner.Owner.GetAllBehaviors().OfType<EntityPresentationBehaviour>().Single();
            if (value.Visual == null) throw new InvalidOperationException("Missing generated native visual binding.");
            return value;
        }

        public static void Release(ObjectView owner)
        {
            if (owner == null) return;
            if (owner.Owner != null)
                foreach (EntityPresentationBehaviour value in owner.Owner.GetAllBehaviors().OfType<EntityPresentationBehaviour>()) value.Unbind();
            Destroy(owner.gameObject);
        }

        public void Initialize(SessionClient value, GameCatalog rules, PinewatchStage scene, LevelLayoutAuthoring authoring,
            SessionNetwork network = null)
        {
            this.network = network;
            client = value;
            catalog = rules;
            stage = scene;
            definitions = new DefinitionRuleIndex(ObjectDefinitionDatabase.Instance);
            definitions.Validate(rules);
            if (network?.UnifiedObjects != true) sceneViews = new SceneEntityViews(authoring, stage.Entities);
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
            wanted.Clear();
            workKinds.Clear();
            foreach (ActorViewData actor in frame.World.Actors) wanted.Add(actor.Id, actor.Kind);
            foreach (BuildingViewData building in frame.World.Buildings) wanted.Add(building.Id, building.Kind);
            foreach (WorksiteViewData site in frame.World.Worksites)
            {
                wanted.Add(site.Id, site.Kind);
                workKinds.Add(site.Id, site.Kind);
            }
            foreach (int id in views.Keys.ToArray())
                if (!wanted.TryGetValue(id, out string kind) || kind != views[id].Kind) Remove(id);
            foreach (int id in pending.Keys.ToArray())
                if (!wanted.TryGetValue(id, out string kind) || kind != pending[id].Kind) pending.Remove(id);
            applied = frame;
            foreach (var pair in wanted)
                if (!views.ContainsKey(pair.Key) && !pending.ContainsKey(pair.Key))
                {
                    long ticket = ++nextTicket;
                    pending.Add(pair.Key, (pair.Value, ticket));
                    Create(pair.Key, pair.Value, generation, ticket).Forget();
                }
        }

        private async UniTask Create(int id, string kind, int captured, long ticket)
        {
            ObjectView owner = null;
            try
            {
                if (network?.UnifiedObjects == true)
                    owner = network.Hosting ? network.ObjectWorld?.Index.Find(id)?.Object.ObjectView : network.ReplicaObjects.View(id);
                else owner = sceneViews.Borrow(kind);
                if (network?.UnifiedObjects == true && owner == null)
                    throw new InvalidOperationException("World object was not prepared before presentation: " + id);
                if (owner == null) owner = await CreateVisual(kind);
                if (!Current(id, kind, captured, ticket)) { ReleaseEntity(owner); return; }
                EntityPresentationBehaviour presentation = RequiredPresentation(owner);
                Action<InputIntent> submit = intent => Submit(presentation, captured, intent);
                if (catalog.Balance.Units.TryGetValue(kind, out UnitDefinition rules) && presentation is ActorPresentationBehaviour actor)
                    actor.Bind(id, epoch, kind, rules, submit);
                else if (catalog.Balance.Buildings.ContainsKey(kind) && presentation is BuildingPresentationBehaviour building)
                    building.Bind(id, epoch, kind, submit);
                else if (catalog.Balance.Worksites.ContainsKey(kind) && presentation is WorksitePresentationBehaviour site)
                    site.Bind(id, epoch, kind, submit);
                else throw new InvalidOperationException("Unsupported entity presentation: " + kind);
                views.Add(id, (owner, presentation, kind));
                owner.gameObject.name = kind + " #" + id;
                if (client.Replica.Current == applied) Present(applied);
            }
            catch (Exception error)
            {
                if (views.TryGetValue(id, out var registered) && registered.Owner == owner) views.Remove(id);
                ReleaseEntity(owner);
                Debug.LogException(error);
            }
            finally
            {
                if (captured == generation && pending.TryGetValue(id, out var request) && request.Ticket == ticket) pending.Remove(id);
            }
        }

        private bool Current(int id, string kind, int captured, long ticket) => this != null && captured == generation &&
            client.ConnectionGeneration == connection && client.Replica.Current?.Epoch == epoch &&
            pending.TryGetValue(id, out var request) && request.Ticket == ticket &&
            wanted.TryGetValue(id, out string current) && current == kind && CurrentKind(id) == kind;

        private string CurrentKind(int id)
        {
            var world = client.Replica.Current.World;
            return world.Actors.FirstOrDefault(value => value.Id == id)?.Kind ??
                world.Buildings.FirstOrDefault(value => value.Id == id)?.Kind ?? world.Worksites.FirstOrDefault(value => value.Id == id)?.Kind;
        }

        private void Submit(EntityPresentationBehaviour presentation, int captured, InputIntent intent)
        {
            if (this == null || captured != generation || connection != client.ConnectionGeneration ||
                presentation.Epoch != client.Replica.Current?.Epoch || !client.Ready ||
                Presentation(presentation.Id) != presentation || CurrentKind(presentation.Id) != presentation.Kind) return;
            intentHandler?.Invoke(intent);
        }

        private void Present(SessionViewData frame)
        {
            double now = Time.unscaledTimeAsDouble;
            foreach (ActorViewData actor in frame.World.Actors)
                if (Presentation(actor.Id) is ActorPresentationBehaviour view)
                {
                    workKinds.TryGetValue(actor.TargetId, out string kind);
                    view.Present(actor, frame.Epoch, kind ?? "", timeline.X(actor, now), timeline.ActionTime(actor, now), stage.Ambient);
                }
            foreach (BuildingViewData building in frame.World.Buildings)
                if (Presentation(building.Id) is BuildingPresentationBehaviour view) view.Present(building, frame.Epoch, stage.Ambient);
            foreach (WorksiteViewData site in frame.World.Worksites)
                if (Presentation(site.Id) is WorksitePresentationBehaviour view) view.Present(site, frame.Epoch, stage.Ambient);
        }

        private void Remove(int id)
        {
            views[id].Presentation.Unbind();
            ReleaseEntity(views[id].Owner);
            views.Remove(id);
        }

        private void Clear()
        {
            generation++;
            foreach (int id in views.Keys.ToArray()) Remove(id);
            pending.Clear();
            wanted.Clear();
            workKinds.Clear();
            applied = null;
            timeline.Reset();
        }

        private void ReleaseEntity(ObjectView owner)
        {
            if (network?.UnifiedObjects == true) return;
            if (owner != null && (sceneViews == null || !sceneViews.Return(owner))) Release(owner);
        }

        private void OnDestroy() { intentHandler = null; Clear(); }
    }
}
