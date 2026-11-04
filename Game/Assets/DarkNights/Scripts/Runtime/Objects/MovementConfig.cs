using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DarkNights.Core.Config;
using GameCore.Objects.Behaviours.Interfaces;
using GameCore.Objects.Definition;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// MovementBehaviour唯一装配配置；目标点、跳跃和空控参数在角色激活时统一冻结。
    /// 玩家速度选择冻结预设或原balance，自动单位仍使用显式单位步速，不持有实例速度或输入。
    /// </summary>
    [Serializable]
    [MovedFrom(true, "DarkNights.Runtime.Objects", "DarkNights.Runtime", "MoveToConfig")]
    public sealed class MovementConfig : IConfigData
    {
        public string Name => "移动";

        [Header("手动角色速度"), InspectorName("速度预设")]
        [Tooltip("角色激活时冻结，修改后重新创建会话生效。自动单位沿用单位步速；跳速和重力继续读取balance。参照方案只换算速度，过渡时间为候选。")]
        public MovementSpeedPreset SpeedPreset;

        [Header("目标点移动"), InspectorName("到达距离")]
        [Tooltip("自动移动按实际位置判断到达，单位为逻辑像素；不绕过碰撞吸附目标点。")]
        public float ArrivalDistance = 0.8f;

        [Header("能力开关"), InspectorName("允许主动跳跃")]
        [Tooltip("关闭后不接受起跳请求；重力、已有竖直速度和落地仍正常执行。")]
        public bool CanJump;

        [InspectorName("允许空中操控")]
        [Tooltip("关闭后不接受空中方向修正；已有惯性、松键减速、重力与碰撞仍继续执行。")]
        public bool EnableAirControl;

        [Header("空中操控"), InspectorName("空中修正速度比例"), Range(0, 1)]
        public float AirSpeedRatio = MovementDefinition.DefaultSpeedRatio;

        [InspectorName("空中增速时间（秒）"), Min(.01f)]
        public float AirAccelerationSeconds = MovementDefinition.DefaultAccelerationSeconds;

        [InspectorName("空中反打刹停时间（秒）"), Min(.01f)]
        public float AirBrakingSeconds = MovementDefinition.DefaultBrakingSeconds;

        [InspectorName("空中松键减速比例／秒"), Min(0)]
        public float AirDragPerSecond = MovementDefinition.DefaultDragPerSecond;

        public MovementDefinition Freeze() => new MovementDefinition(ArrivalDistance, CanJump, EnableAirControl,
            AirSpeedRatio, AirAccelerationSeconds, AirBrakingSeconds, AirDragPerSecond, FreezeSpeed());

        private MovementSpeedDefinition FreezeSpeed()
        {
            return SpeedPreset switch
            {
                MovementSpeedPreset.Balance => null,
                MovementSpeedPreset.SlowExploration => new MovementSpeedDefinition(72, 108, .28f, .20f),
                MovementSpeedPreset.BalancedExploration => new MovementSpeedDefinition(80, 120, .24f, .18f),
                MovementSpeedPreset.AgileExploration => new MovementSpeedDefinition(88, 132, .18f, .14f),
                MovementSpeedPreset.StrongSprint => new MovementSpeedDefinition(72, 126, .26f, .18f),
                MovementSpeedPreset.OriginalGame => new MovementSpeedDefinition(112, 176, 64f / 2400, 64f / 2400),
                MovementSpeedPreset.MinecraftReference => new MovementSpeedDefinition(
                    4.317f / 1.8f * 44, 5.612f / 1.8f * 44, .22f, .16f),
                MovementSpeedPreset.TerrariaReference => new MovementSpeedDefinition(
                    3f * 60 / 42 * 44, 6f * 60 / 42 * 44, .40f, .26f),
                _ => throw new ArgumentOutOfRangeException(nameof(SpeedPreset), "未知移动速度预设。")
            };
        }

        public string Fingerprint()
        {
            MovementSpeedDefinition speed = Freeze().Speed;
            using var bytes = new MemoryStream();
            using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
            writer.Write("movement-speed-presets-v1");
            writer.Write(JsonUtility.ToJson(this));
            writer.Write(speed != null);
            if (speed != null)
            {
                writer.Write(speed.WalkSpeed);
                writer.Write(speed.SprintSpeed);
                writer.Write(speed.SprintAccelerationSeconds);
                writer.Write(speed.SprintDecelerationSeconds);
            }
            writer.Flush();
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>按定义身份冻结所有已装配移动能力的参数，缺失或重复配置拒绝装配。</summary>
        public static string ContentFingerprint(IEnumerable<ObjectDefinition> definitions) =>
            string.Join(";", definitions.Where(d => d.BehaviourTypes.Contains(typeof(MovementBehaviour).FullName))
                .OrderBy(d => d.Guid.ToString(), StringComparer.Ordinal)
                .Select(d => d.Guid + ":" + d.SharedConfigs.OfType<MovementConfig>().Single().Fingerprint()));
    }
}
