using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.View.Terrain
{
    /// <summary>真实相机前后回执；仅当输入、页面修订与稳定状态在同次绘制中保持一致才确认，生命周期由表现宿主拥有。</summary>
    internal sealed class TerrainDrawReceipt : IDisposable
    {
        private readonly Func<Camera> camera;
        private readonly Func<bool> stable;
        private readonly Func<(ulong Generation, ulong Commit, int Revision)> identity;
        private readonly Action<ulong, ulong> confirm;
        private bool drawing;
        private (ulong Generation, ulong Commit, int Revision) captured;
        internal TerrainDrawReceipt(Func<Camera> camera, Func<bool> stable,
            Func<(ulong, ulong, int)> identity, Action<ulong, ulong> confirm)
        {
            this.camera = camera; this.stable = stable; this.identity = identity; this.confirm = confirm;
            Camera.onPreCull += Begin; Camera.onPostRender += End;
            RenderPipelineManager.beginCameraRendering += BeginPipeline;
            RenderPipelineManager.endCameraRendering += EndPipeline;
        }
        internal void Invalidate() => drawing = false;
        private void BeginPipeline(ScriptableRenderContext context, Camera value) => Begin(value);
        private void EndPipeline(ScriptableRenderContext context, Camera value) => End(value);
        private void Begin(Camera value)
        {
            if (value != camera()) return;
            drawing = stable(); captured = identity();
        }
        private void End(Camera value)
        {
            if (value != camera() || !drawing) return;
            drawing = false;
            if (stable() && captured.Equals(identity())) confirm(captured.Generation, captured.Commit);
        }
        public void Dispose()
        {
            Camera.onPreCull -= Begin; Camera.onPostRender -= End;
            RenderPipelineManager.beginCameraRendering -= BeginPipeline;
            RenderPipelineManager.endCameraRendering -= EndPipeline; drawing = false;
        }
    }
}
