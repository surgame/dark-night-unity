using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using GameCore.Objects.Behaviours.Interfaces;
using GameCore.Objects.Definition;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// MovementBehaviour唯一装配配置；目标点、跳跃和空控参数在角色激活时统一冻结。
    /// 玩家和自动单位复用同一运动执行，基础数值仍读取原balance，不持有实例速度或输入。
    /// </summary>
    [Serializable]
    [MovedFrom(true, "DarkNights.Runtime.Objects", "DarkNights.Runtime", "MoveToConfig")]
    public sealed class MovementConfig : IConfigData
    {
        public string Name => "移动";

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
            AirSpeedRatio, AirAccelerationSeconds, AirBrakingSeconds, AirDragPerSecond);

        public string Fingerprint()
        {
            Freeze();
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(
                    JsonUtility.ToJson(this)))).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>按定义身份冻结所有已装配移动能力的参数，缺失或重复配置拒绝装配。</summary>
        public static string ContentFingerprint(IEnumerable<ObjectDefinition> definitions) =>
            string.Join(";", definitions.Where(d => d.BehaviourTypes.Contains(typeof(MovementBehaviour).FullName))
                .OrderBy(d => d.Guid.ToString(), StringComparer.Ordinal)
                .Select(d => d.Guid + ":" + d.SharedConfigs.OfType<MovementConfig>().Single().Fingerprint()));
    }
}
