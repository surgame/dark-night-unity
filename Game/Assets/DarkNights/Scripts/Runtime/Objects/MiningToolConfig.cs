using System;
using System.Linq;
using DarkNights.Core.Config;
using GameCore.Objects.Behaviours.Interfaces;
using GameCore.Objects.Definition;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>工具自身 Definition 的采集参数；装配时冻结，目标白名单只在工具侧维护，空矿床列表表示所有矿床定义。</summary>
    [Serializable]
    public sealed class MiningToolConfig : IConfigData
    {
        public string Name => "工具采集能力";
        [InspectorName("支持的目标类别")] public MiningTargetKinds Targets = MiningTargetKinds.Foreground;
        [InspectorName("支持全部材料")] public bool AllMaterials = true;
        [InspectorName("允许的材料 Key")] public string[] Materials = Array.Empty<string>();
        [InspectorName("允许的矿床 Definition（空为全部）")] public DefinitionReference[] Deposits = Array.Empty<DefinitionReference>();
        [InspectorName("采集等级")] public int Level = 1;
        [InspectorName("单次伤害")] public int Damage = 10;
        [InspectorName("吸附与挥砍距离（逻辑像素）")]
        [Tooltip("从手部沿瞄准方向到最近真实表面的最大距离；吸附与权威落镐共用，不额外扩大预览范围。16 逻辑像素为一格，新会话生效。")]
        public float Reach = 64;
        [InspectorName("手部高度（逻辑像素）")] public float HandHeight = 36;
        [InspectorName("动作周期（秒）")] public float Seconds = .48f;
        [InspectorName("命中进度")] public float ImpactFraction = .6f;

        public MiningToolRules Freeze()
        {
            if (Materials == null || Deposits == null || Materials.Distinct(StringComparer.Ordinal).Count() != Materials.Length ||
                Deposits.Any(value => value.IsEmpty || value.LegacyId != 0 || !Guid.TryParseExact(value.GuidString, "N", out _)) ||
                Deposits.Select(value => value.GuidString).Distinct().Count() != Deposits.Length)
                throw new InvalidOperationException("材料或矿床白名单缺失、重复或定义身份无效。");
            return new MiningToolRules(Targets, AllMaterials, Materials, Deposits.Select(value => value.GuidString),
                Level, Damage, Reach, HandHeight, Seconds, ImpactFraction);
        }

        /// <summary>仅供独立地形工作台和既有设备伤害标定读取默认工具；正式玩家采集从装备槽解析定义。</summary>
        public static MiningToolRules DefaultRules() => ObjectDefinitionDatabase.Instance.GetDefinitionByKey("item.pickaxe")
            .SharedConfigs.OfType<MiningToolConfig>().Single().Freeze();

        public string Fingerprint()
        {
            Freeze();
            using var hash = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(this))))
                .Replace("-", "").ToLowerInvariant();
        }
    }
}
