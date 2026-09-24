using DarkNights.Editor.Terrain;
using UnityEditor;

namespace DarkNights.Editor
{
    /// <summary>
    /// 统一注册正式项目顶部菜单；具体命令和状态仍由各工具负责，独立示例保留自己的程序集入口。
    /// </summary>
    public static class DarkNightsMenu
    {
        private const string Root = "Dark Nights/";
        private const string LegacyTerrain = Root + "Terrain/Legacy/";
        internal const string HeroSpeedPath = Root + "Debug/主角移动 8×";

        [MenuItem(Root + "Content/Initialize Environment")]
        private static void InitializeEnvironment() => DarkNightsEnvironmentSetup.Initialize();
        [MenuItem(Root + "Content/Validate Environment")]
        private static void ValidateEnvironment() => EnvironmentValidation.Validate();
        [MenuItem(Root + "Build/Addressables Content")]
        private static void BuildAddressables() => DarkNightsEnvironmentSetup.BuildAddressablesContent();
        [MenuItem(Root + "Content/Register Initial Configuration")]
        private static void RegisterConfiguration() => GameContentSetup.Register();
        [MenuItem(Root + "Content/Create Initial Pinewatch Layout")]
        private static void CreatePinewatchLayout() => PinewatchLayoutSetup.Create();
        [MenuItem(Root + "Content/Create Initial Formal Objects")]
        private static void CreateFormalObjects() => FormalObjectContentSetup.CreateInitial();
        [MenuItem(Root + "Content/Install Formal Session Network")]
        private static void InstallSessionNetwork() => SessionNetworkSetup.Install();
        [MenuItem(Root + "Content/Install Initial Native Art")]
        private static void InstallNativeArt() => NativeArtSetup.Install();
        [MenuItem(Root + "Content/Install Initial Pinewatch Visuals")]
        private static void InstallPinewatchVisuals() => PinewatchVisualSetup.Install();
        [MenuItem(Root + "Content/Install Initial Native Environment")]
        private static void InstallNativeEnvironment() => NativeEnvironmentSetup.Install();
        [MenuItem(Root + "Content/Install Initial Native Effects")]
        private static void InstallNativeEffects() => NativeEffectsSetup.Install();
        [MenuItem(Root + "Content/Install Initial Native UI")]
        private static void InstallNativeUi() => NativeUiSetup.Install();
        [MenuItem(Root + "Content/Calibrate Native UI Theme Once")]
        private static void CalibrateUiTheme() => NativeThemeCalibration.Apply();
        [MenuItem(Root + "Content/Add Native Save Slots")]
        private static void AddSaveSlots() => NativeSaveUiSetup.Install();
        [MenuItem(Root + "Content/Install Hero Input Slice")]
        private static void InstallHeroInput() => HeroContentSetup.Install();
        [MenuItem(Root + "Content/Install Handheld Equipment")]
        private static void InstallHandheldEquipment() => HandheldContentSetup.Install();
        [MenuItem(Root + "Content/Install Mineral Deposit Object")]
        private static void InstallMineralDeposit() => MineralDepositContentSetup.Install();
        [MenuItem(Root + "Art/安装可步入飞船首版")]
        private static void InstallShipArt() => ShipAssetSetup.Install();
        [MenuItem(Root + "Art/安装地形 Modifier 首版")]
        private static void InstallTerrainModifiers() => TerrainModifierInstaller.Install();
        [MenuItem(Root + "Build/Windows Mono")]
        private static void BuildMono() => GamePlayerBuild.Mono();
        [MenuItem(Root + "Build/Windows Mono Map State")]
        private static void BuildMapStateMono() => GamePlayerBuild.MapStateMono();
        [MenuItem(Root + "Build/Windows IL2CPP")]
        private static void BuildIl2Cpp() => GamePlayerBuild.Il2Cpp();
        [MenuItem(Root + "Verify/Session Lifecycle")]
        private static void VerifySessionLifecycle() => SessionLifecycleProbe.Run();
        [MenuItem(Root + "Verify/Native UI Runtime")]
        private static void VerifyNativeUi() => NativeUiRuntimeProbe.Run();

        [MenuItem(HeroSpeedPath)]
        private static void ToggleHeroSpeed() => ScenePlaySelection.ToggleDebugSpeed();
        [MenuItem(HeroSpeedPath, true)]
        private static bool ValidateHeroSpeed() => ScenePlaySelection.ValidateDebugSpeed();

        [MenuItem(LegacyTerrain + "打开随机地图 Bootstrap")]
        private static void OpenTerrainDebug() => TerrainDebugSetup.Open();
        [MenuItem(LegacyTerrain + "构建随机地图 Bootstrap Mono")]
        private static void BuildTerrainDebug() => TerrainDebugSetup.Build();
        [MenuItem(LegacyTerrain + "打开天然洞穴实验")]
        private static void OpenCaveExploration() => CaveExplorationSetup.Open();
        [MenuItem(LegacyTerrain + "Map generator")]
        private static void OpenLegacyTerrainGenerator() => TerrainGeneratorWindow.Open();
        [MenuItem(Root + "Terrain/Cave Wall Tuner")]
        private static void OpenTerrainPreview() => TerrainStylePreviewWindow.Open();
        [MenuItem(LegacyTerrain + "Create initial test tiles")]
        private static void CreateTestTiles() => TerrainTestAssets.Create();
        [MenuItem(LegacyTerrain + "Create initial cave tiles")]
        private static void CreateCaveTiles() => CaveTerrainAssets.Create();
        [MenuItem(LegacyTerrain + "Create default test map and scene")]
        private static void CreateTestMap() => TerrainMapExporter.CreateDefault();
        [MenuItem(LegacyTerrain + "Create network test scene")]
        private static void CreateNetworkTestScene() => TerrainPlayerBuild.Create();
        [MenuItem(LegacyTerrain + "Build test Mono")]
        private static void BuildNetworkTest() => TerrainPlayerBuild.Mono();
        [MenuItem(LegacyTerrain + "Create random Pinewatch template")]
        private static void CreateRandomPinewatch() => RandomLevelSetup.Install();
    }
}
