using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DarkNights.Core.Physics;
using GameCore.Objects.Behaviours.Interfaces;
using GameCore.Objects.Definition;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 吊绳ObjectDefinition的唯一共享制作参数，统一使用世界单位、kg和秒。
    /// 场景只提供固定点及拓扑身份，激活前冻结规则，副本不持有运行姿态或抓附状态。
    /// </summary>
    [Serializable]
    public sealed class HangingRopeConfig : IConfigData
    {
        public string Name => "可攀爬吊绳";
        [InspectorName("物理节数"), Range(2, 12)] public int SegmentCount = 12;
        [InspectorName("每节长度")] public float SegmentLength = .32f;
        [InspectorName("每节碰撞宽度")] public float SegmentWidth = .06f;
        [InspectorName("每节质量（kg）")] public float SegmentMass = .1f;
        [InspectorName("抓取距离")] public float GrabReach = .48f;
        [InspectorName("攀爬速度")] public float ClimbSpeed = .75f;
        [InspectorName("助摆加速度")] public float SwingAcceleration = 1.6f;
        [InspectorName("水平助力渐退阈值")] public float SwingSpeedThreshold = 3;
        [InspectorName("切向速度阻尼／秒")] public float TangentialDamping = .24f;
        [InspectorName("绳节角阻尼")] public float AngularDamping = .08f;
        [InspectorName("显示宽度")] public float LineWidth = .024f;
        [InspectorName("圆角顶点")] public int CornerVertices = 2;
        [InspectorName("端帽顶点")] public int CapVertices = 2;
        [InspectorName("每节显示采样点")] public int SamplesPerSegment = 4;
        [InspectorName("曲线最大偏移")] public float MaximumCurveOffset = .02f;

        public HangingRopeConfig Copy() => (HangingRopeConfig)MemberwiseClone();

        public HangingRopeDefinition Freeze(ObjectDefinition definition, int id, int firstBodyId, float x, float y)
        {
            string hash = DefinitionHash(definition);
            return new HangingRopeDefinition(id, firstBodyId, SegmentCount, x * 100, y * 100,
                SegmentLength * 100, SegmentMass, GrabReach * 100, ClimbSpeed * 100,
                SwingAcceleration * 100, SwingSpeedThreshold * 100, TangentialDamping,
                definition.Guid.ToString(), hash, SegmentWidth * 100, AngularDamping);
        }

        public static HangingRopeConfig Required(ObjectDefinition definition)
        {
            if (definition == null || definition.Guid.IsEmpty || !definition.isLocal ||
                definition.PrefabRef == null ||
                definition.BehaviourTypes.Count(name => name == typeof(HangingRopeBehaviour).FullName) != 1 ||
                definition.SharedConfigs.OfType<HangingRopeConfig>().Count() != 1)
                throw new InvalidOperationException("吊绳定义需要本地Prefab、唯一HangingRopeConfig及HangingRopeBehaviour。");
            var config = definition.SharedConfigs.OfType<HangingRopeConfig>().Single();
            PhysicsNumbers.Require(config.LineWidth, .001f, .2f);
            PhysicsNumbers.Require(config.MaximumCurveOffset, 0, .08f);
            if (config.CornerVertices < 0 || config.CornerVertices > 8 ||
                config.CapVertices < 0 || config.CapVertices > 8 ||
                config.SamplesPerSegment < 1 || config.SamplesPerSegment > 8)
                throw new InvalidOperationException("吊绳显示采样或圆角配置越界。");
            return config;
        }

        public static string DefinitionHash(ObjectDefinition definition)
        {
            var config = Required(definition);
            string text = definition.Guid + "|" + definition.PrefabRef.AssetGUID + "|" +
                string.Join(";", definition.BehaviourTypes) + "|" + JsonUtility.ToJson(config);
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }
}
