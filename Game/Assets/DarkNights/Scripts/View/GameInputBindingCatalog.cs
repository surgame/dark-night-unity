using System;
using System.Collections.Generic;
using GameCore.PlayerInputs;
using UnityEngine.InputSystem;

namespace DarkNights.View
{
    /// <summary>从原生动作枚举设置界面的只读绑定条目和中文名称；不缓存键位、不拥有输入许可或改键生命周期。</summary>
    internal static class GameInputBindingCatalog
    {
        internal static IReadOnlyList<GameInputActions.BindingInfo> Read(InputActionMap hero, InputActionMap camp,
            Func<InputAction, bool> rebindable)
        {
            var result = new List<GameInputActions.BindingInfo>();
            foreach (var map in new[] { hero, camp })
                foreach (var action in map.actions)
                {
                    if (!rebindable(action)) continue;
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        var binding = action.bindings[i];
                        if (binding.isComposite || string.IsNullOrEmpty(binding.effectivePath)) continue;
                        string name = (map == hero ? "主角 / " : "营地 / ") + Label(action) +
                            (binding.isPartOfComposite ? " / " +
                                (binding.name == "Negative" ? "左" : binding.name == "Positive" ? "右" : binding.name) : "");
                        result.Add(new GameInputActions.BindingInfo(action.id, binding.id, name,
                            YYInputRebindingService.GetBindingDisplayString(action, i)));
                    }
                }
            return result;
        }

        private static string Label(InputAction action) => action.name switch
        {
            "Move" => "移动", "Jump" => "跳跃", "Crouch" => "下落／驾驶下降", "Sprint" => "加速", "Interact" => "交互",
            "ToggleLight" => "手电开关", "Attack" => "使用道具", "Item1" => "道具 1", "Item2" => "道具 2",
            "Item3" => "道具 3", "Item4" => "道具 4", "ToggleMode" => "切换模式",
            "Select" => "选择", "Orders" => "指令", "Append" => "追加选择",
            "Pan" => "拖动镜头", "Pause" => "暂停", "Help" => "帮助",
            "Save" => "保存", "Load" => "读取", "Home" => "镜头归位",
            "Guards" => "守卫", "IdleWorkers" => "闲置工人", _ => action.name
        };
    }
}
