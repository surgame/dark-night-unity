using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 单次模拟反馈的不可变值；只含表现参数，不持有世界或结算能力，消费方负责按会话生命周期转发。
    /// </summary>
    public sealed class VisualCue
    {
        public string Kind { get; }
        public float X { get; }
        public float Y { get; }
        public string Text { get; }
        public string ContentId { get; }
        public float Face { get; }
        public bool Enemy { get; }

        public VisualCue(string Kind, float X, float Y, string Text = "", string ContentId = "", float Face = 1, bool Enemy = false)
        {
            this.Kind = Kind;
            this.X = X;
            this.Y = Y;
            this.Text = Text;
            this.ContentId = ContentId;
            this.Face = Face;
            this.Enemy = Enemy;
        }
    }
}
