using System;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.View
{
    /// <summary>
    /// 吊绳ObjectDefinition的原生主视图，显式绑定绳身与固定点显示。
    /// 只消费公共物理展示时间线，不创建刚体、不推进物理或写回权威状态。
    /// </summary>
    public sealed class HangingRopeView : ObjectView
    {
        public LineRenderer Line;
        public SpriteRenderer Anchor;

        public void Configure(Vector3 position, float width, int corners, int caps)
        {
            if (Line == null || Anchor == null)
                throw new InvalidOperationException("吊绳主视图缺少绳身或固定点绑定。");
            transform.position = position;
            Line.useWorldSpace = true;
            Line.startWidth = Line.endWidth = width;
            Line.numCornerVertices = corners;
            Line.numCapVertices = caps;
        }
    }
}
