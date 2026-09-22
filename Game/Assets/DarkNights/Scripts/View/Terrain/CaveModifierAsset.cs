using System;
using DarkNights.Core.Logic.Terrain;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>策划／美术可引用和排序的 modifier 资产基类；主线程捕获纯参数后才启动后台生成，禁止在任务中读取 ScriptableObject。</summary>
    public abstract class CaveModifierAsset : ScriptableObject
    {
        public abstract ICaveMaskModifier Capture();
        public static CaveModifierStack CaptureStack(CaveModifierAsset[] assets)
        {
            if (assets == null || assets.Length == 0) return CaveModifierStack.Empty;
            var modifiers = new ICaveMaskModifier[assets.Length];
            for (int i = 0; i < assets.Length; i++)
                modifiers[i] = assets[i] != null ? assets[i].Capture() : throw new InvalidOperationException("Modifier 列表存在丢失引用；关闭请移除该项。");
            return new CaveModifierStack(modifiers);
        }
    }
}
