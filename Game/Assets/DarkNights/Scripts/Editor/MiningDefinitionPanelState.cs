using System;
using System.Linq;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;
using UnityEditor;

namespace DarkNights.Editor
{
    /// <summary>由 EditorWindow 序列化的采集导航状态；只保存作者资产选择和预览输入，不复制能力配置或运行状态。</summary>
    [Serializable]
    internal sealed class MiningDefinitionPanelState
    {
        public ObjectDefinition Tool, Deposit;
        public string Material = "";
        public bool Mineral = true, Rare, Initialized;
        internal void Initialize()
        {
            if (Initialized) return;
            Tool = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/ShipTrade/item-pickaxe.asset");
            Deposit = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/MineralDeposit/MineralDeposit.asset");
            Material = Deposit?.SharedConfigs.OfType<MineralDepositRuleConfig>().FirstOrDefault()?.CommonResource ?? "";
            Initialized = true;
        }
    }
}
