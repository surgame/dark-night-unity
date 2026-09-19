using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Entry
{
    /// <summary>
    /// 本地会话的有界投射物表现池，开局经 YYGC 定义工厂预热，射击期间只租还已装配对象。
    /// 全量投影决定存活身份；epoch/连接变化全部归还，异步预热被取消时释放已取得的实例。
    /// </summary>
    internal sealed class SessionBallistics : IDisposable
    {
        private readonly List<(ObjectView Owner, BallisticView View)> instances = new List<(ObjectView, BallisticView)>();
        private readonly Stack<int> free = new Stack<int>();
        private readonly Dictionary<long, int> active = new Dictionary<long, int>();
        private readonly HashSet<long> live = new HashSet<long>();
        private readonly List<long> expired = new List<long>();
        private bool disposed;
        public int ActiveCount => active.Count;
        public int InstanceCount => instances.Count;

        public async UniTask Initialize(Transform parent)
        {
            var definition = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("effect.ballistic");
            if (definition == null) throw new InvalidOperationException("Missing ballistic effect definition.");
            try
            {
                for (int i = 0; i < HandheldConfig.PoolCapacity; i++)
                {
                    ObjectView owner = await ObjectInstanceFactory.CreateObjectInstanceAsync(
                        new DefinitionReference(definition.Guid).Resolve(), Vector3.zero, Quaternion.identity, parent);
                    if (disposed) { EntityViewFactory.Release(owner); return; }
                    var view = owner.Get<BallisticView>("ballistic");
                    if (view == null) { EntityViewFactory.Release(owner); throw new InvalidOperationException("Missing ballistic binding."); }
                    owner.gameObject.SetActive(false);
                    free.Push(instances.Count); instances.Add((owner, view));
                }
            }
            catch { Dispose(); throw; }
        }

        public void Present(SessionViewData frame, double seconds, float ground, Color ambient)
        {
            live.Clear(); expired.Clear();
            foreach (var shot in frame.World.Projectiles) if (shot.Kind != 0) live.Add(shot.ViewId);
            foreach (var pair in active) if (!live.Contains(pair.Key)) expired.Add(pair.Key);
            foreach (long id in expired)
            {
                int index = active[id]; active.Remove(id);
                instances[index].Owner.gameObject.SetActive(false); free.Push(index);
            }
            foreach (var shot in frame.World.Projectiles)
            {
                if (shot.Kind == 0) continue;
                if (!active.TryGetValue(shot.ViewId, out int index))
                {
                    if (free.Count == 0) throw new InvalidOperationException("Ballistic projection exceeded the view pool.");
                    index = free.Pop(); active.Add(shot.ViewId, index);
                    instances[index].Owner.gameObject.SetActive(true);
                }
                instances[index].View.Present(shot, seconds, ground, ambient);
            }
        }

        public void Clear()
        {
            active.Clear(); free.Clear();
            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i].Owner != null) instances[i].Owner.gameObject.SetActive(false);
                free.Push(i);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var entry in instances) EntityViewFactory.Release(entry.Owner);
            instances.Clear(); active.Clear(); free.Clear();
        }
    }
}
