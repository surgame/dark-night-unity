using System;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>
    /// 入口白模模块的唯一作者轮廓，坐标相对模块原点，单位为Unity世界单位。
    /// 编辑器显式重建原生网格；运行期冻结同一轮廓为现有格地形，不持有运行状态或结算内容。
    /// 首片只允许按X递增的下边和洞顶，不包含物品、独立过渡或随机求解。
    /// </summary>
    public sealed class WhiteboxTerrainAuthoring : MonoBehaviour
    {
        public string ModuleId;
        public string DisplayName;
        public float Width = 15.28f;
        public Vector2[] Floor = Array.Empty<Vector2>();
        public Vector2[] Ceiling = Array.Empty<Vector2>();
        public float DisplayBottom = -4.8f;
        public float DisplayTop = 4.8f;
        public MeshFilter TerrainMesh;
        public MeshFilter BackgroundMesh;
        public LineRenderer FloorLine;
        public LineRenderer CeilingLine;
        public SpriteRenderer TerrainArtwork;
        public SpriteRenderer BackgroundArtwork;
        [HideInInspector] public string ArtworkFingerprint;
        [HideInInspector] public string BakedFingerprint;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ModuleId) || string.IsNullOrWhiteSpace(DisplayName) ||
                Width <= 1 || Width > 40 || float.IsNaN(Width) ||
                transform.position != Vector3.zero || transform.rotation != Quaternion.identity ||
                transform.lossyScale != Vector3.one)
                throw new InvalidOperationException("入口模块需要有效身份、有限宽度及原点单位变换。");
            ValidateLine(Floor, true);
            ValidateLine(Ceiling, false);
            if (Math.Abs(Floor[0].x) > .001f || Math.Abs(Floor[0].y) > .001f ||
                Math.Abs(Floor[Floor.Length - 1].x - Width) > .001f ||
                Math.Abs(Ceiling[Ceiling.Length - 1].x - Width) > .001f ||
                DisplayBottom >= -3 || DisplayTop <= 3)
                throw new InvalidOperationException("入口白模边界或平地起点无效。");
        }

        private void ValidateLine(Vector2[] points, bool floor)
        {
            if (points == null || points.Length < 2)
                throw new InvalidOperationException("白模轮廓至少需要两个点。");
            float previous = -1;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 p = points[i];
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y) ||
                    p.x < previous || p.x < 0 || p.x > Width + .001f ||
                    p.y <= DisplayBottom || p.y >= DisplayTop)
                    throw new InvalidOperationException((floor ? "地面" : "洞顶") + "轮廓点无效或X不递增。");
                float column = p.x * EntityView.PixelsPerUnit / 16 - .5f;
                float level = p.y * EntityView.PixelsPerUnit / 16;
                if (!(floor && i == 0) && Math.Abs(column - Mathf.Round(column)) > .001f ||
                    Math.Abs(level - Mathf.Round(level)) > .001f)
                    throw new InvalidOperationException("轮廓拐点必须位于16像素格边界和完整高度层。");
                if (i > 0 && Math.Abs(p.x - points[i - 1].x) > .0001f)
                {
                    float slope = Math.Abs((p.y - points[i - 1].y) / (p.x - points[i - 1].x));
                    if (Math.Min(slope, Math.Min(Math.Abs(slope - .5f), Math.Abs(slope - 1))) > .001f)
                        throw new InvalidOperationException("入口首片只使用平面、全坡及半坡。");
                }
                previous = p.x;
            }
        }

        public static float Height(Vector2[] line, float x)
        {
            if (x <= line[0].x) return line[0].y;
            for (int i = 1; i < line.Length; i++)
                if (line[i].x > line[i - 1].x && x < line[i].x)
                    return Mathf.Lerp(line[i - 1].y, line[i].y,
                        (x - line[i - 1].x) / (line[i].x - line[i - 1].x));
            return line[line.Length - 1].y;
        }
    }
}
