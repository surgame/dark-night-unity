using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 原生灰松谷月亮、萤火、火把及灯光的显式场景绑定，所有原始位置和样式保存在资源中。
    /// Play 由关卡传入本地表现时间；编辑器只按预览参数采样，不启动会话、网络或访问存档。
    /// </summary>
    [ExecuteAlways]
    public sealed class NativeEnvironment : MonoBehaviour
    {
        [SerializeField] private Transform moon;
        [SerializeField] private SpriteRenderer moonDisc;
        [SerializeField] private SpriteRenderer moonCutout;
        [SerializeField] private Color moonColor = new Color(0.89f, 0.88f, 0.72f, 0.65f);
        [SerializeField] private Color dayCutout = new Color32(94, 113, 125, 255);
        [SerializeField] private Color nightCutout = new Color32(38, 54, 81, 255);
        [SerializeField] private float cameraOffsetX = 138;
        [SerializeField] private SpriteRenderer[] fireflies;
        [SerializeField] private Color fireflyColor = new Color(0.91f, 0.85f, 0.53f, 1);
        [SerializeField] private float distributionWidth = 1100;
        [SerializeField] private CampTorch[] torches;
        [SerializeField] private CampLight[] lights;
        [SerializeField] private SpriteRenderer[] staticSprites;
        [SerializeField] private Color[] staticColors;
        [SerializeField, Range(0, 1)] private float previewNight = 0.16f;
        [SerializeField] private float previewCameraX = 255;
        private readonly Vector4[] lightPositions = new Vector4[7];
        private readonly Vector4[] lightColors = new Vector4[7];
        private static readonly int Positions = Shader.PropertyToID("_DNCampLights");
        private static readonly int Colors = Shader.PropertyToID("_DNCampLightColors");
        private static readonly int Ambient = Shader.PropertyToID("_DNCampAmbient");

        public void Present(double time, float night, float cameraX, Color ambient)
        {
            if (moon == null) return;
            moon.localPosition = new Vector3((cameraX + cameraOffsetX) / 100, moon.localPosition.y, 0);
            moonDisc.color = moonColor * ambient;
            moonCutout.color = Color.Lerp(dayCutout, nightCutout, night) * ambient;
            for (int i = 0; i < fireflies.Length; i++)
            {
                float x = (float)((i * 83.1 + time * 1.1) % distributionWidth);
                float y = -15 - (float)(i * 11.3 % 95) + (float)System.Math.Sin(time * 0.4 + i) * 3;
                fireflies[i].transform.localPosition = new Vector3(x / 100, -y / 100, 0);
                Color color = fireflyColor * ambient;
                color.a *= (0.15f + night * 0.5f) * (0.5f + (float)System.Math.Sin(time + i * 4.1) * 0.5f);
                fireflies[i].color = color;
            }
            foreach (CampTorch torch in torches) torch.Present(time, ambient);
            for (int i = 0; i < staticSprites.Length; i++) staticSprites[i].color = staticColors[i] * ambient;
            for (int i = 0; i < lightPositions.Length; i++)
            {
                lightPositions[i] = i < lights.Length ? lights[i].Shape : Vector4.zero;
                lightColors[i] = i < lights.Length ? lights[i].ColorAt(time, night) : Vector4.zero;
            }
            Shader.SetGlobalVectorArray(Positions, lightPositions);
            Shader.SetGlobalVectorArray(Colors, lightColors);
            Shader.SetGlobalColor(Ambient, ambient);
        }

        private void Update()
        {
            if (!Application.isPlaying) Present(0, previewNight, previewCameraX,
                Color.Lerp(new Color32(233, 235, 222, 255), new Color32(113, 135, 169, 255), previewNight));
        }
        private void OnDisable()
        {
            Shader.SetGlobalVectorArray(Colors, new Vector4[7]);
            Shader.SetGlobalColor(Ambient, Color.white);
        }
    }
}
