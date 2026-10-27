using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>
    /// 历史混合规则的只读导入载体；保留既有资源字段和摘要，导入后拆为标准参数并由标准校验器检查。
    /// 不提供生成、校验或随机种子辅助逻辑，生成阶段不能依赖本类型。
    /// </summary>
    [Serializable]
    public sealed class CaveRuleSettings
    {
        public bool Enabled;
        public int Version = CaveLayoutSettings.CurrentVersion;
        public string Templates = CaveLayoutSettings.TemplateVersion;
        public string Entrance = "valley";
        public string Road = "erosion";
        public string RoomMix = "mixed";
        public string ContentSeed = "ORE-1";
        public int SpaceRevision;
        public int ContentRevision;
        public int RoomCount = 16;
        public double RoomScale = 1;
        public int RoadRadius = 3;
        public double BranchShare = .45;
        public int BranchDepth = 3;
        public double RockGap = .12;
        public int VeinCount = 8;
        public int Capacity = 180;
        public double EmptyShare = .18;
        public double ExposedShare = .35;
        public double RichShare = .35;
        public bool Nests = true;
    }
}
