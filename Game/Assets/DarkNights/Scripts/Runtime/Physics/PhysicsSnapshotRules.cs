using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Physics;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 网络与保存共用的物理闭包校验；配置、角色、道具和约束必须完整对应同一个冻结切点。
    /// 校验仅读数据，失败不得交换当前业务或原生场景。
    /// </summary>
    public static class PhysicsSnapshotRules
    {
        public static void Validate(PhysicsWorldSnapshot snapshot, PhysicsWorldDefinition definition,
            IEnumerable<int> actorIds)
        {
            if (definition == null)
            {
                if (snapshot != null) throw new FormatException("格运动会话不能接收物理检查点。");
                return;
            }
            if (snapshot == null || snapshot.DefinitionHash != definition.Identity)
                throw new FormatException("物理检查点与后端配置不一致。");
            var expected = new HashSet<int>(actorIds.Concat(definition.Props.Select(p => p.Id)));
            if (!expected.SetEquals(snapshot.Bodies.Select(b => b.Id)))
                throw new FormatException("物理检查点缺少关联物体或包含未知物体。");
            if (snapshot.Ropes.Any(r => r.HangingRopeId == 0 && r.Length > definition.LinkMaximum))
                throw new FormatException("物理约束长度超过当前配置。");
            if (snapshot.Ropes.Any(r => r.HangingRopeId == 0 && definition.HangingRopes.Any(h => h.Contains(r.TargetId))))
                throw new FormatException("环境绳节必须使用明确抓附模式。");
            foreach (var link in snapshot.Ropes.Where(r => r.HangingRopeId != 0))
            {
                var rope = definition.HangingRopes.SingleOrDefault(r => r.Id == link.HangingRopeId);
                if (rope == null || link.Position < Math.Min(40, rope.Length * .1f) || link.Position > rope.Length - 4 ||
                    HangingRopeGeometry.Locate(rope, link.Position).BodyId != link.TargetId)
                    throw new FormatException("环境绳抓附与冻结拓扑不一致。");
                if (snapshot.Ropes.Any(other => other.OwnerId != link.OwnerId &&
                    other.HangingRopeId == link.HangingRopeId &&
                    Math.Abs(other.Position - link.Position) < HangingRopeDefinition.GripSpacing - .001f))
                    throw new FormatException("环境绳抓附间距不足。");
            }
        }
    }
}
