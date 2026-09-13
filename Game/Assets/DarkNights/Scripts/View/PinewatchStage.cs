using DarkNights.Core.ViewData;
using DarkNights.Core.Config;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 正式场景的显式摄像机、实体容器与背景绑定；镜头和昼夜渐变归本地客户端所有。
    /// 暂停时仍响应镜头，场景未开局时保持可见预览，不创建网络或可写世界。
    /// </summary>
    public sealed class PinewatchStage : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Transform entities;
        [SerializeField] private SpriteRenderer sky;
        [SerializeField] private NativeBackdrop[] backgrounds;
        [SerializeField] private NativeEnvironment environment;
        [SerializeField] private float cameraX = 255;
        [SerializeField] private float zoom = 2.8f;
        [SerializeField] private float worldWidth = 1100;
        private float night = 0.16f;
        private double visualTime;
        private int epoch;
        private bool observing;
        private static readonly Color DayAmbient = new Color32(233, 235, 222, 255);
        private static readonly Color NightAmbient = new Color32(113, 135, 169, 255);
        public Camera SceneCamera => sceneCamera;
        public Transform Entities => entities;
        public Color Ambient => Color.Lerp(DayAmbient, NightAmbient, night);
        public float CameraX => cameraX;
        public float Zoom => zoom;
        public double PresentationTime => visualTime;
        public float NightAmount => night;
        public float WorldWidth => worldWidth;
        public float InitialCameraX { get; private set; }
        public Color IlluminationAt(Vector3 position) => environment != null ? environment.IlluminationAt(position) : Ambient.linear;

        public void Initialize(LevelLayout layout)
        {
            worldWidth = layout.WorldWidth;
            InitialCameraX = layout.CameraX;
            Focus(InitialCameraX);
        }

        public void Move(float pixels) { cameraX += pixels; UpdateCamera(); }
        public void Focus(float pixels) { cameraX = pixels; UpdateCamera(); }
        public void ChangeZoom(float factor) { zoom = Mathf.Clamp(zoom * factor, 1.8f, 4.5f); UpdateCamera(); }

        public void Present(SessionViewData frame)
        {
            CampViewData camp = frame?.World.Camp;
            float target = camp == null ? 0.35f : camp.Mode == "Won" ? 0 : camp.WavePhase == "Night" ? 1 :
                camp.DayRemaining < 22 ? (float)(1 - camp.DayRemaining / 22) * 0.6f : 0.05f;
            if (frame == null) observing = false;
            else if (!observing || epoch != frame.Epoch)
            {
                night = frame.Elapsed > 0 ? target : 0.16f;
                observing = true; epoch = frame.Epoch;
            }
            night = Mathf.MoveTowards(night, target, Time.unscaledDeltaTime * 0.16f);
            visualTime += Time.unscaledDeltaTime;
            Render();
        }

        /// <summary>
        /// 在指定的本地表现时刻采样场景，用于编辑预览与固定状态画面对照；不推进世界或网络。
        /// 参数只控制本场景的光照、环境动画和镜头，下一次常规 Present 从该表现时刻继续。
        /// </summary>
        public void SamplePresentation(double time, float nightAmount)
        {
            if (double.IsNaN(time) || double.IsInfinity(time) || time < 0 ||
                float.IsNaN(nightAmount) || nightAmount < 0 || nightAmount > 1)
                throw new System.ArgumentOutOfRangeException(nameof(time), "Invalid presentation sample.");
            visualTime = time;
            night = nightAmount;
            Render();
        }

        private void Render()
        {
            sky.color = Color.Lerp(new Color32(170, 188, 193, 255), new Color32(70, 87, 120, 255), night) * Ambient;
            foreach (NativeBackdrop backdrop in backgrounds) backdrop.Apply(cameraX, night, Ambient);
            if (environment != null) environment.Present(visualTime, night, cameraX, Ambient);
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            float half = Screen.width * 0.5f / zoom;
            cameraX = Mathf.Clamp(cameraX, half, Mathf.Max(half, worldWidth - half));
            sceneCamera.orthographicSize = Screen.height * 0.5f / zoom / 100;
            sceneCamera.transform.position = new Vector3(cameraX / 100, Screen.height * 0.215f / zoom / 100, -10);
        }
    }
}
