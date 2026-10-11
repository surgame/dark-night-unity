using System;
using DarkNights.Core.Config.Terrain;
using UnityEngine;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>
    /// .minemap 作者文件导入后的关卡资源；仅持有生成输入，创建会话前复制为纯 Core 合同。
    /// 原文件是唯一可编辑来源；导入资产不拥有矿量、采空、角色或梯子状态。
    /// </summary>
    public sealed class MineMapAsset : ScriptableObject
    {
        public MineMapDefinition Definition;
        public MineMapDefinition Freeze() => Definition?.CopyValidated()
            ?? throw new InvalidOperationException("矿洞资源缺少定义。");
    }
}
