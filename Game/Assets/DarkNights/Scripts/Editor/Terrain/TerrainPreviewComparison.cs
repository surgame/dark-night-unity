using System;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>上一幅有效GPU图像及其实际绘制镜头；等待和比较期间按当前画布导航重投影，退休释放，不保存地图状态。</summary>
    internal sealed class TerrainPreviewComparison : IDisposable
    {
        internal RenderTexture Image { get; private set; }
        private Vector3 cameraPosition;
        private float cameraSize;
        internal void Capture(TerrainStylePreviewStage stage)
        {
            Capture(stage.Image);
            cameraPosition = stage.ImageCameraPosition; cameraSize = stage.ImageCameraSize;
        }
        internal void Capture(Texture source)
        {
            if (source == null) return;
            if (Image == null || Image.width != source.width || Image.height != source.height)
            {
                Dispose();
                Image = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32)
                { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                Image.Create();
            }
            Graphics.Blit(source, Image);
            cameraPosition = new Vector3(160, -96, -10); cameraSize = 96;
        }
        internal void Draw(Rect rect, TerrainStylePreviewCanvas canvas)
        {
            if (Image == null) return;
            Vector3 center = canvas.CameraCenter(rect, Image);
            float height = Mathf.Min(rect.width / Image.width, rect.height / Image.height) * Image.height;
            float scale = height / (192 / canvas.Zoom);
            var size = new Vector2(cameraSize * 2 * Image.width / Image.height, cameraSize * 2) * scale;
            var offset = new Vector2(cameraPosition.x - center.x, center.y - cameraPosition.y) * scale;
            var bounds = new Rect(rect.size * .5f + offset - size * .5f, size);
            EditorGUI.DrawRect(rect, new Color(.12f, .13f, .15f));
            GUI.BeginGroup(rect);
            GUI.DrawTexture(bounds, Image, ScaleMode.StretchToFill, false);
            GUI.EndGroup();
        }
        public void Dispose()
        {
            if (Image == null) return;
            Image.Release(); UnityEngine.Object.DestroyImmediate(Image); Image = null;
        }
    }
}
