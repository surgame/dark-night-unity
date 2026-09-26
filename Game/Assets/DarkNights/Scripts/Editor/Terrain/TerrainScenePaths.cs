namespace DarkNights.Editor.Terrain
{
    /// <summary>地形工作台、验证场景与保留待删除对照的编辑器路径合同；正式关卡和第三方 Sample 不在本表中。</summary>
    public static class TerrainScenePaths
    {
        public const string Workbenches = "Assets/DarkNights/Res/Scenes/Workbenches/Terrain";
        public const string OldWorkbenches = Workbenches + "/(old)";
        public const string Tests = "Assets/DarkNights/Res/Scenes/Tests/Terrain";
        public const string PendingDeletion = "Assets/DarkNights/Res/Scenes/PendingDeletion/Terrain";
        public const string ReferenceChamber = Workbenches + "/ReferenceChamber.unity";
        public const string RandomCave = Workbenches + "/RandomCave.unity";
        public const string CaveExploration = OldWorkbenches + "/CaveExploration(old).unity";
        public const string TerrainDebugBootstrap = OldWorkbenches + "/TerrainDebugBootstrap(old).unity";
        public const string PendingCaveContourStatic = PendingDeletion + "/CaveContourStatic.unity";
        public const string TerrainTest = Tests + "/TerrainTest.unity";
        public const string TerrainNetworkTest = Tests + "/TerrainNetworkTest.unity";
    }
}
