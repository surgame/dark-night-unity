using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.View.Terrain
{
    /// <summary>航程地图的本地显露适配；只给本宿主渲染器写属性块，保留真实绘制和分页生命周期，不修改共享材质。</summary>
    public sealed class TerrainArrivalVisibility : MonoBehaviour
    {
        private static readonly int OpacityId = Shader.PropertyToID("_ArrivalOpacity");
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private Camera viewCamera;
        private Func<float> opacitySource;
        private bool drawingComplete;
        public bool FullyDrawn { get; private set; }

        public void Initialize(Camera camera, Func<float> opacity)
        {
            viewCamera = camera; opacitySource = opacity;
            Camera.onPreCull += BeginCamera; Camera.onPostRender += EndCamera;
            RenderPipelineManager.beginCameraRendering += BeginPipeline;
            RenderPipelineManager.endCameraRendering += EndPipeline;
        }

        private void BeginPipeline(ScriptableRenderContext context, Camera camera) => BeginCamera(camera);
        private void EndPipeline(ScriptableRenderContext context, Camera camera) => EndCamera(camera);
        private void BeginCamera(Camera camera)
        {
            if (camera != viewCamera) return;
            float opacity = Mathf.Clamp01(opacitySource?.Invoke() ?? 1);
            if (opacity >= 1 && FullyDrawn) { drawingComplete = true; return; }
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(OpacityId)) continue;
                renderer.GetPropertyBlock(properties); properties.SetFloat(OpacityId, opacity);
                renderer.SetPropertyBlock(properties);
            }
            drawingComplete = opacity >= 1;
        }
        private void EndCamera(Camera camera)
        {
            if (camera == viewCamera) FullyDrawn = drawingComplete && gameObject.activeInHierarchy;
        }
        private void OnDestroy()
        {
            Camera.onPreCull -= BeginCamera; Camera.onPostRender -= EndCamera;
            RenderPipelineManager.beginCameraRendering -= BeginPipeline;
            RenderPipelineManager.endCameraRendering -= EndPipeline;
        }
    }
}
