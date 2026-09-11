using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 将两种冻结视口下的原生控件边界转换为 UGUI 锚点和像素偏移，仅在首版 Prefab 创建时使用。
    /// 输出仍是普通 RectTransform；不在运行时读取旧引擎数据或覆盖美术编辑后的布局。
    /// </summary>
    public static class NativeUiGeometry
    {
        public static void Set(RectTransform target, JArray first, JArray second, JArray parentFirst, JArray parentSecond)
        {
            if (parentFirst == null)
            {
                target.anchorMin = Vector2.zero;
                target.anchorMax = Vector2.one;
                target.offsetMin = target.offsetMax = Vector2.zero;
                return;
            }
            float width1 = (float)parentFirst[2], width2 = (float)parentSecond[2];
            float height1 = (float)parentFirst[3], height2 = (float)parentSecond[3];
            Vector2 left = Edge((float)first[0], (float)second[0], width1, width2);
            Vector2 right = Edge((float)first[0] + (float)first[2], (float)second[0] + (float)second[2], width1, width2);
            Vector2 bottom = Edge(height1 - (float)first[1] - (float)first[3], height2 - (float)second[1] - (float)second[3], height1, height2);
            Vector2 top = Edge(height1 - (float)first[1], height2 - (float)second[1], height1, height2);
            target.anchorMin = new Vector2(left.x, bottom.x);
            target.anchorMax = new Vector2(right.x, top.x);
            target.offsetMin = new Vector2(left.y, bottom.y);
            target.offsetMax = new Vector2(right.y, top.y);
        }

        private static Vector2 Edge(float first, float second, float size1, float size2)
        {
            float anchor = Mathf.Abs(size2 - size1) < 0.001f ? 0 : (second - first) / (size2 - size1);
            return new Vector2(anchor, first - size1 * anchor);
        }
    }
}
