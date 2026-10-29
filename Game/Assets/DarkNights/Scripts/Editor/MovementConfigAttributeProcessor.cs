using System;
using System.Collections.Generic;
using System.Reflection;
using DarkNights.Runtime.Objects;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;

namespace DarkNights.Editor
{
    /// <summary>
    /// 为Workshop及Definition Inspector中的移动配置添加编辑器条件显示。
    /// 空中操控关闭时隐藏四项参数；仅修饰Odin属性树，保留序列化值及原有Undo和保存流程。
    /// </summary>
    public sealed class MovementConfigAttributeProcessor : OdinAttributeProcessor<MovementConfig>
    {
        public override void ProcessChildMemberAttributes(InspectorProperty parentProperty,
            MemberInfo member, List<Attribute> attributes)
        {
            switch (member.Name)
            {
                case nameof(MovementConfig.AirSpeedRatio):
                case nameof(MovementConfig.AirAccelerationSeconds):
                case nameof(MovementConfig.AirBrakingSeconds):
                case nameof(MovementConfig.AirDragPerSecond):
                    attributes.Add(new ShowIfAttribute(nameof(MovementConfig.EnableAirControl), false));
                    break;
            }
        }
    }
}
