using System;
using DarkNights.Core.Physics;
using UnityEngine;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// Scene中的环境吊绳作者实例；固定世界锚点与初始拓扑唯一从本对象冻结。
    /// 编辑态Gizmos仅显示自然垂落范围，运行绳节由会话后端生成，显示对象不挂权威刚体。
    /// </summary>
    public sealed class HangingRopePlacement : MonoBehaviour
    {
        public int RopeId = 1;
        public int FirstBodyId = -9;
        public int SegmentCount = 8;
        public float SegmentLength = .64f;
        public float SegmentMass = .2f;
        public float GrabReach = .48f;
        public float ClimbSpeed = .8f;
        public float SwingAcceleration = 4;
        public LineRenderer Preview;

        public HangingRopeDefinition Freeze()
        {
            if (transform.lossyScale != Vector3.one || Quaternion.Angle(transform.rotation, Quaternion.identity) > .001f ||
                Math.Abs(transform.position.z) > .001f || Preview == null ||
                GetComponentsInChildren<Rigidbody2D>(true).Length != 0)
                throw new InvalidOperationException("吊绳作者锚点需保持单位变换、显式预览且不能携带运行刚体。");
            return new HangingRopeDefinition(RopeId, FirstBodyId, SegmentCount, transform.position.x * 100,
                transform.position.y * 100, SegmentLength * 100, SegmentMass, GrabReach * 100,
                ClimbSpeed * 100, SwingAcceleration * 100);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(.75f, .65f, .4f);
            Vector3 top = transform.position;
            Gizmos.DrawWireSphere(top, .08f);
            for (int i = 0; i < SegmentCount; i++)
            {
                Vector3 end = top + Vector3.down * SegmentLength;
                Gizmos.DrawLine(top, end); Gizmos.DrawWireSphere(end, .025f); top = end;
            }
        }
    }
}
