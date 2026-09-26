using System;
using DarkNights.Core.Config.Expedition;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 星球表格的一行作者配置；仅存在于会话定义的共享配置中，由 Freeze 生成纯不可变输入。
    /// 运行时不得写回本行，数值沿用权威像素；既有 balance 飞行速度、经济与伤害保持原来源。
    /// </summary>
    [Serializable]
    public sealed class PlanetPreset
    {
        public string Id = "grey-pine";
        public string DisplayName = "灰松星";
        [TextArea] public string Description = "开放天空与安全降落区，地表步道连接下方天然洞穴。";
        public bool Enabled = true;
        [Tooltip("留空由服务端每次产生种子；固定种子用于可复现地图。")] public string Seed = "";
        public int DockColumn = 36;
        public int DockRow = 40;
        public int LandingWidth = 32;
        public float ArrivalHeight = 176;
        public float HorizontalRange = 240;
        public float MaximumLift = 448;
        public float TransitSeconds = .8f;
        public int StarCount = 120;
        public float StarSpeed = 110;
        [Tooltip("可选 star-shift、fade、none；表现策略不改变服务端航程。")] public string TransitionKind = "star-shift";
        public string SpaceColorHex = "#060C20";
        public string SkyColorHex = "#243B55";

        public PlanetDefinition Freeze() => new PlanetDefinition(Id, DisplayName, Description, Enabled, Seed,
            DockColumn, DockRow, LandingWidth, ArrivalHeight, HorizontalRange, MaximumLift, TransitSeconds,
            StarCount, StarSpeed, TransitionKind, SpaceColorHex, SkyColorHex);
    }
}
