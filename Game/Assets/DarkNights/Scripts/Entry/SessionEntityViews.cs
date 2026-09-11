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
    /// 将正式客户端冻结副本接到 YYGC 定义工厂和 View 绑定；每实体只持有本地外观，无第二份经济或生命状态。
    /// 所有异步创建检查连接代次、epoch、实体种类和当前需求；退出或换世界时释放旧对象及未完成结果。
    /// </summary>
    public sealed class SessionEntityViews : MonoBehaviour
    {
        private SessionClient client;
        private GameCatalog catalog;
        private PinewatchStage stage;
        private ContentDefinitionMap map;
        private readonly Dictionary<int, (ObjectView Owner, NativeVisual Visual, string Kind)> views =
            new Dictionary<int, (ObjectView, NativeVisual, string)>();
        private readonly HashSet<int> pending = new HashSet<int>();
        private readonly Dictionary<int, string> wanted = new Dictionary<int, string>();
        private int epoch, generation;
        private long connection;
        private SessionViewData applied;
        public int Count => views.Count;
        public NativeVisual Visual(int id) => views.TryGetValue(id, out var view) ? view.Visual : null;

        public void Initialize(SessionClient value, GameCatalog rules, PinewatchStage scene)
        {
            client = value;
            catalog = rules;
            stage = scene;
            map = ObjectDefinitionDatabase.Instance.GetDefinitionByKey(FormalObjectCatalog.SessionKey)
                .SharedConfigs.OfType<ContentDefinitionMap>().Single();
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
            if (frame != applied)
            {
                wanted.Clear();
                foreach (ActorViewData actor in frame.World.Actors) wanted.Add(actor.Id, actor.Kind);
                foreach (BuildingViewData building in frame.World.Buildings) wanted.Add(building.Id, building.Kind);
                foreach (WorksiteViewData site in frame.World.Worksites) wanted.Add(site.Id, site.Kind);
                foreach (int id in views.Keys.ToArray())
                    if (!wanted.TryGetValue(id, out string kind) || kind != views[id].Kind) Remove(id);
                foreach (var pair in wanted)
                    if (!views.ContainsKey(pair.Key) && pending.Add(pair.Key)) Create(pair.Key, pair.Value, generation).Forget();
                applied = frame;
            }
            Present(frame);
        }

        private async UniTask Create(int id, string kind, int captured)
        {
            ObjectView owner = null;
            try
            {
                owner = await ObjectInstanceFactory.CreateObjectInstanceAsync(map.GetRequired(kind), Vector3.zero, Quaternion.identity, stage.Entities);
                if (this == null || captured != generation || client.ConnectionGeneration != connection ||
                    client.Replica.Current?.Epoch != epoch || !wanted.TryGetValue(id, out string current) || current != kind)
                {
                    if (owner != null) Destroy(owner.gameObject);
                    return;
                }
                if (owner == null) throw new InvalidOperationException("Cannot create native visual: " + kind);
                NativeVisual visual = owner.Get<NativeVisual>("visual");
                if (visual == null) throw new InvalidOperationException("Missing visual binding: " + kind);
                views.Add(id, (owner, visual, kind));
                owner.gameObject.name = kind + " #" + id;
                Present(client.Replica.Current);
            }
            catch (Exception error)
            {
                if (owner != null) Destroy(owner.gameObject);
                Debug.LogException(error);
            }
            finally { if (captured == generation) pending.Remove(id); }
        }

        private void Present(SessionViewData frame)
        {
            foreach (ActorViewData actor in frame.World.Actors)
                if (views.TryGetValue(actor.Id, out var view))
                {
                    Position(view.Visual, actor.X);
                    string kind = frame.World.Worksites.FirstOrDefault(site => site.Id == actor.TargetId)?.Kind ?? "";
                    view.Visual.Apply(actor, catalog, kind);
                }
            foreach (BuildingViewData building in frame.World.Buildings)
                if (views.TryGetValue(building.Id, out var view)) { Position(view.Visual, building.X); view.Visual.Apply(building); }
            foreach (WorksiteViewData site in frame.World.Worksites)
                if (views.TryGetValue(site.Id, out var view)) { Position(view.Visual, site.X); view.Visual.Apply(site); }
        }

        private void Position(NativeVisual visual, float x)
        {
            visual.transform.position = new Vector3(x / 100, 0, 0);
            visual.Ambient = stage.Ambient;
        }

        private void Remove(int id)
        {
            Destroy(views[id].Owner.gameObject);
            views.Remove(id);
        }

        private void Clear()
        {
            generation++;
            foreach (int id in views.Keys.ToArray()) Remove(id);
            pending.Clear();
            wanted.Clear();
            applied = null;
        }

        private void OnDestroy() { Clear(); }
    }
}
