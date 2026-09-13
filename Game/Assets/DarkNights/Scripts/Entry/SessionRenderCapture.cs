using System;
using System.IO;
using System.Linq;
using DarkNights.View;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>
    /// 显式验收驱动的离屏截图，使用正在运行的场景摄像机和真实 UGUI Canvas，不依赖隐藏窗口交换链。
    /// 捕获在同一主线程完成，finally 恢复全部摄像机和 Canvas 状态；常规产品流程不会调用。
    /// </summary>
    public static class SessionRenderCapture
    {
        public static void Save(PinewatchStage stage, string path)
        {
            Camera camera = stage.SceneCamera;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>()
                .Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var modes = canvases.Select(c => (c.worldCamera, c.planeDistance, c.sortingOrder)).ToArray();
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            Texture2D image = null;
            try
            {
                camera.targetTexture = target;
                foreach (Canvas canvas in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1;
                    canvas.sortingOrder += 10000;
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                    canvases[i].worldCamera = modes[i].worldCamera;
                    canvases[i].planeDistance = modes[i].planeDistance;
                    canvases[i].sortingOrder = modes[i].sortingOrder;
                }
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (image != null) UnityEngine.Object.Destroy(image);
                RenderTexture.ReleaseTemporary(target);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
