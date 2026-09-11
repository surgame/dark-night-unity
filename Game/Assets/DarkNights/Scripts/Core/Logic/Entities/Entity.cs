using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Core.Logic.Entities
{
    /// <summary>
    /// 持久世界实体的稳定身份和规则坐标。只由权威世界分配 ID；不保存引擎节点、网络对象 ID 或玩家选择。
    /// </summary>
    public abstract class Entity
    {
        public int Id { get; }
        public string Kind { get; internal set; }
        public float X { get; internal set; }

        protected Entity(int id, string kind, float x)
        {
            Id = id;
            Kind = kind;
            X = x;
        }
    }
}
