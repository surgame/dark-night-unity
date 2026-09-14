using DarkNights.Core.ViewData;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 原生箭矢、浮字和命令圈的序列化表现绑定，坐标和寿命沿用原作像素参数。
    /// 调用者传入冻结轨迹、事件年龄和线性照明；运行网格与字体订阅随实例释放，不结算伤害或推进模拟。
    /// </summary>
    public sealed class NativeEffect : MonoBehaviour
    {
        public const double CommandLifetime = 0.8;
        [SerializeField] private Text label;
        [SerializeField] private Image icon;
        [SerializeField] private Sprite[] resources;
        [SerializeField] private MeshFilter ring;
        private Mesh ringMesh;
        private Vector3[] ringVertices;
        private Color[] ringColors;

        public void Present(VisualCue cue, double age, float ground, Color? linearLight = null)
        {
            transform.position = new Vector3(cue.X / 100, (ground - cue.Y) / 100, 0);
            if (ring != null)
            {
                PresentRing(age);
                return;
            }
            label.text = cue.Text;
            Color color = cue.Kind == "resource" ? new Color32(198, 219, 163, 255) :
                cue.Enemy ? new Color32(232, 218, 193, 255) : new Color32(239, 149, 130, 255);
            color.a = Mathf.Clamp01((1.6f - (float)age) * 2);
            Color illumination = linearLight ?? Color.white;
            label.color = (color.linear * illumination).gamma;
            label.rectTransform.anchoredPosition = new Vector2(-5 + Mathf.Sin(cue.X * 3.7f + cue.Y) * 5, (float)age * 10);
            icon.gameObject.SetActive(cue.Kind == "resource");
            if (cue.Kind == "resource")
            {
                int index = 0;
                for (int i = 0; i < resources.Length; i++)
                    if (DarkNights.Core.Config.GameText.ResourceIds[i] == cue.ContentId) index = i;
                icon.sprite = resources[Mathf.Clamp(index, 0, resources.Length - 1)];
                icon.color = (new Color(1, 1, 1, color.a) * illumination).gamma;
                icon.rectTransform.anchoredPosition = label.rectTransform.anchoredPosition + new Vector2(10, 6);
            }
            PixelFont(label.font);
        }

        private void PresentRing(double age)
        {
            const int segments = 19;
            if (ringMesh == null)
            {
                ringMesh = new Mesh { name = "Command Ring Runtime", hideFlags = HideFlags.DontSave };
                ringMesh.MarkDynamic();
                ringVertices = new Vector3[segments * 4]; ringColors = new Color[segments * 4];
                var triangles = new int[segments * 6];
                for (int i = 0; i < segments; i++)
                {
                    int v = i * 4, t = i * 6;
                    triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                    triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
                }
                ringMesh.vertices = ringVertices; ringMesh.triangles = triangles;
                ring.sharedMesh = ringMesh;
            }
            float radius = (5 + (float)age * 10) / 100;
            var tint = new Color(.9f, .84f, .56f, Mathf.Clamp01(1 - (float)(age / CommandLifetime)));
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                Vector2 q = new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
                Vector2 edge = q - p, normal = new Vector2(-edge.y, edge.x).normalized * .005f;
                ringVertices[i * 4] = Flatten(p + normal); ringVertices[i * 4 + 1] = Flatten(q + normal);
                ringVertices[i * 4 + 2] = Flatten(q - normal); ringVertices[i * 4 + 3] = Flatten(p - normal);
                for (int j = 0; j < 4; j++) ringColors[i * 4 + j] = tint;
            }
            ringMesh.vertices = ringVertices; ringMesh.colors = ringColors; ringMesh.RecalculateBounds();
        }

        private static Vector3 Flatten(Vector2 point) => new Vector3(point.x, point.y * .35f);

        private void OnEnable() { if (label != null) Font.textureRebuilt += PixelFont; }
        private void OnDisable() { Font.textureRebuilt -= PixelFont; }
        private void PixelFont(Font font)
        {
            if (label != null && font == label.font && font.material.mainTexture != null)
                font.material.mainTexture.filterMode = FilterMode.Point;
        }
        /// <summary>由池所有者显式释放运行网格；覆盖尚未显示及 EditMode 中没有销毁回调的实例，可重复调用。</summary>
        public void ReleaseRuntimeResources()
        {
            if (ringMesh == null) return;
            if (ring != null && ring.sharedMesh == ringMesh) ring.sharedMesh = null;
            if (Application.isPlaying) Destroy(ringMesh);
            else DestroyImmediate(ringMesh);
            ringMesh = null;
            ringVertices = null;
            ringColors = null;
        }

        private void OnDestroy() => ReleaseRuntimeResources();

        public void Present(ProjectileViewData arrow, double age, float ground)
        {
            float ratio = Mathf.Clamp01((float)(age / arrow.Duration));
            Vector2 from = new Vector2(arrow.FromX, arrow.FromY), to = new Vector2(arrow.ToX, arrow.ToY);
            Vector2 point = Vector2.Lerp(from, to, ratio) + new Vector2(0, -Mathf.Sin(Mathf.PI * ratio) * 10);
            Vector2 tangent = to - from + new Vector2(0, -Mathf.Cos(Mathf.PI * ratio) * 31.4f);
            transform.position = new Vector3(point.x / 100, (ground - point.y) / 100, 0);
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(-tangent.y, tangent.x) * Mathf.Rad2Deg);
        }
    }
}
