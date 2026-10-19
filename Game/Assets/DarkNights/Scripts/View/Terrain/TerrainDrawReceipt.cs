using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.View.Terrain
{
    /// <summary>宿主拥有的真实相机回执；保存最后确认的输入及画面修订，同提交号下重新投影仍须再次实绘，退休撤除所有相机订阅。</summary>
    internal sealed class TerrainDrawReceipt : IDisposable
    {
        private readonly Func<Camera> camera;
        private readonly Func<bool> stable;
        private readonly Func<(ulong Generation, ulong Commit, int Revision)> identity;
        private readonly Action<ulong, ulong> confirm;
        private bool drawing, hasConfirmed;
        private (ulong Generation, ulong Commit, int Revision) captured;
        private (ulong Generation, ulong Commit, int Revision) confirmed;
        internal bool IsCurrent => hasConfirmed && confirmed.Equals(identity());
        internal TerrainDrawReceipt(Func<Camera> camera, Func<bool> stable,
            Func<(ulong, ulong, int)> identity, Action<ulong, ulong> confirm)
        {
            this.camera = camera; this.stable = stable; this.identity = identity; this.confirm = confirm;
            Camera.onPreCull += Begin; Camera.onPostRender += End;
            RenderPipelineManager.beginCameraRendering += BeginPipeline;
            RenderPipelineManager.endCameraRendering += EndPipeline;
        }
        internal void Invalidate() { drawing = false; hasConfirmed = false; }
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
            if (stable() && captured.Equals(identity()))
            {
                confirmed = captured; hasConfirmed = true;
                confirm(captured.Generation, captured.Commit);
            }
        }
        public void Dispose()
        {
            Camera.onPreCull -= Begin; Camera.onPostRender -= End;
            RenderPipelineManager.beginCameraRendering -= BeginPipeline;
            RenderPipelineManager.endCameraRendering -= EndPipeline; Invalidate();
        }
    }
}
