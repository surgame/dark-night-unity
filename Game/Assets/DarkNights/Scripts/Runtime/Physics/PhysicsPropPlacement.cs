using System;
using DarkNights.Core.Physics;
using UnityEngine;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 原生Scene中的箱子初始布局唯一来源，负ID在作者场景稳定保存；脚底位置和可见尺寸冻结为物理定义。
    /// 只在开局读取，不携带运行状态或刚体；Play时作者实例隐藏，由会话只读表现接管。
    /// </summary>
    public sealed class PhysicsPropPlacement : MonoBehaviour
    {
        public int Id = -1;
        public SpriteRenderer Visual;
        public PhysicsPropDefinition Freeze(PhysicsProfile profile)
        {
            if (Visual == null || Visual.transform != transform || Visual.sprite == null ||
                Visual.sprite != profile.PropViewPrefab.sprite ||
                Quaternion.Angle(transform.rotation, Quaternion.identity) > .001f ||
                Math.Abs(transform.position.z) > .001f || transform.lossyScale.x <= 0 || transform.lossyScale.y <= 0 ||
                Math.Abs(Visual.sprite.bounds.min.y) > .0001f)
                throw new InvalidOperationException("物理道具需要显式脚底精灵、正缩放和零初始旋转／Z。");
            Vector3 size = Visual.sprite.bounds.size;
            return new PhysicsPropDefinition(Id, transform.position.x * 100, transform.position.y * 100,
                size.x * transform.lossyScale.x * 100, size.y * transform.lossyScale.y * 100, profile.PropMass);
        }
    }
}
