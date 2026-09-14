using System;
using Cysharp.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.View;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Entry
{
    /// <summary>
    /// 预热并独占八个本地指令圈，复用正式 Definition、Prefab 和绑定；点击时不加载或创建对象。
    /// 只保存显示坐标与未缩放时间，密集点击复用最早的圈；换局清空显示，宿主释放时销毁整个池。
    /// </summary>
    public sealed class LocalCommandRings : IDisposable
    {
        public const int Capacity = 8;
        private readonly ObjectView[] owners = new ObjectView[Capacity];
        private readonly NativeEffect[] views = new NativeEffect[Capacity];
        private readonly VisualCue[] cues = new VisualCue[Capacity];
        private readonly double[] born = new double[Capacity];
        private int next;
        private float ground;
        private bool initialized, disposed;
        public int Count { get; private set; }
        public int InstanceCount { get; private set; }
        public long PresentationCount { get; private set; }

        public async UniTask Initialize(ObjectDefinition definition, Transform parent, float groundY)
        {
            if (initialized || disposed) throw new InvalidOperationException("Command ring pool is already initialized or disposed.");
            if (definition == null || definition.isNetwork)
                throw new InvalidOperationException("Command rings require a local effect definition.");
            initialized = true;
            ground = groundY;
            try
            {
                for (int i = 0; i < Capacity; i++)
                {
                    ObjectView owner = await ObjectInstanceFactory.CreateObjectInstanceAsync(
                        definition, Vector3.zero, Quaternion.identity, parent);
                    if (disposed) { EntityViewFactory.Release(owner); return; }
                    owners[i] = owner;
                    NativeEffect view = owner != null ? owner.Get<NativeEffect>("effect") : null;
                    if (view == null) throw new InvalidOperationException("Missing native command ring binding: effect.");
                    views[i] = view;
                    view.Present(new VisualCue("command", 0, ground), 0, ground);
                    owner.gameObject.SetActive(false);
                    InstanceCount++;
                }
            }
            catch { Dispose(); throw; }
        }

        public void Show(float x, double now)
        {
            if (disposed || InstanceCount != Capacity || float.IsNaN(x) || float.IsInfinity(x)) return;
            int index = next;
            next = (next + 1) % Capacity;
            if (cues[index] == null) Count++;
            cues[index] = new VisualCue("command", x, ground);
            born[index] = now;
            PresentationCount++;
            views[index].Present(cues[index], 0, ground);
            owners[index].gameObject.SetActive(true);
        }

        public void Tick(double now)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (cues[i] == null) continue;
                double age = now - born[i];
                if (age >= NativeEffect.CommandLifetime)
                {
                    owners[i].gameObject.SetActive(false);
                    cues[i] = null;
                    Count--;
                }
                else views[i].Present(cues[i], age, ground);
            }
        }

        internal void SamplePresentation(double age)
        {
            for (int i = 0; i < Capacity; i++)
                if (cues[i] != null) views[i].Present(cues[i], age, ground);
        }

        public void Clear()
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (owners[i] != null) owners[i].gameObject.SetActive(false);
                cues[i] = null;
            }
            Count = 0;
            next = 0;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Clear();
            for (int i = 0; i < Capacity; i++)
            {
                if (views[i] != null) views[i].ReleaseRuntimeResources();
                EntityViewFactory.Release(owners[i]);
                owners[i] = null;
                views[i] = null;
            }
            InstanceCount = 0;
        }
    }
}
