using DarkNights.Editor.Terrain;

namespace DarkNights.Editor
{
    /// <summary>
    /// 注册工作台维护命令；具体命令与状态仍归原工具。
    /// </summary>
    public static class DarkNightsMenu
    {
        private const string Root = "Dark Nights/";

        [DarkNightsWorkbenchCommand(Root + "Content/Initialize Environment", "初始化项目环境")]
        private static void InitializeEnvironment() => DarkNightsEnvironmentSetup.Initialize();
        [DarkNightsWorkbenchCommand(Root + "Content/Validate Environment", "检查项目环境")]
        private static void ValidateEnvironment() => EnvironmentValidation.Validate();
        [DarkNightsWorkbenchCommand(Root + "Build/Addressables Content", "构建 Addressables 内容")]
        private static void BuildAddressables() => DarkNightsEnvironmentSetup.BuildAddressablesContent();
        [DarkNightsWorkbenchCommand(Root + "Content/Register Initial Configuration", "注册初始配置")]
        private static void RegisterConfiguration() => GameContentSetup.Register();
        [DarkNightsWorkbenchCommand(Root + "Content/Create Initial Pinewatch Layout", "创建灰松谷初始布局")]
        private static void CreatePinewatchLayout() => PinewatchLayoutSetup.Create();
        [DarkNightsWorkbenchCommand(Root + "Content/Create Initial Formal Objects", "创建正式对象首版")]
        private static void CreateFormalObjects() => FormalObjectContentSetup.CreateInitial();
        [DarkNightsWorkbenchCommand(Root + "Content/Install Formal Session Network", "安装会话网络")]
        private static void InstallSessionNetwork() => SessionNetworkSetup.Install();
        [DarkNightsWorkbenchCommand(Root + "Content/Install Initial Native Art", "安装原生美术首版")]
        private static void InstallNativeArt() => NativeArtSetup.Install();
        [DarkNightsWorkbenchCommand(Root + "Content/Install Initial Pinewatch Visuals", "安装灰松谷表现首版")]
        private static void InstallPinewatchVisuals() => PinewatchVisualSetup.Install();
        [DarkNightsWorkbenchCommand(Root + "Content/Install Initial Native Environment", "安装原生环境首版")]
        private static void InstallNativeEnvironment() => NativeEnvironmentSetup.Install();
        [DarkNightsWorkbenchCommand(Root + "Content/Install Initial Native Effects", "安装原生特效首版")]
        private static void InstallNativeEffects() => NativeEffectsSetup.Install();
        [DarkNightsWorkbenchCommand(Root + "Content/Install Initial Native UI", "安装原生界面首版")]
        private static void InstallNativeUi() => NativeUiSetup.Install();
        [DarkNightsWorkbenchCommand(Root + "Content/Calibrate Native UI Theme Once", "校准界面主题")]
        private static void CalibrateUiTheme() => NativeThemeCalibration.Apply();
        [DarkNightsWorkbenchCommand(Root + "Content/Add Native Save Slots", "安装存档槽位")]
        private static void AddSaveSlots() => NativeSaveUiSetup.Install();
        [DarkNightsWorkbenchCommand(Root + "Content/Install Hero Input Slice", "安装主角输入")]
        private static void InstallHeroInput() => HeroContentSetup.Install();
        [DarkNightsWorkbenchCommand(Root + "Content/Install Handheld Equipment", "安装手持装备")]
        private static void InstallHandheldEquipment() => HandheldContentSetup.Install();
        [DarkNightsWorkbenchCommand(Root + "Content/Install Mineral Deposit Object", "安装矿床对象")]
        private static void InstallMineralDeposit() => MineralDepositContentSetup.Install();
        [DarkNightsWorkbenchCommand(Root + "Art/安装可步入飞船首版", "安装可步入飞船首版")]
        private static void InstallShipArt() => ShipAssetSetup.Install();
        [DarkNightsWorkbenchCommand(Root + "Art/安装地形 Modifier 首版", "安装地形 Modifier 首版")]
        private static void InstallTerrainModifiers() => TerrainModifierInstaller.Install();
        [DarkNightsWorkbenchCommand(Root + "Art/创建 LocalV2 岩层对照样式", "创建 LocalV2 岩层对照样式")]
        private static void CreateLocalV2TerrainStyle() => TerrainModifierInstaller.CreateLocalV2Candidate();
        [DarkNightsWorkbenchCommand(Root + "Build/Windows Mono", "构建 Windows Mono")]
        private static void BuildMono() => GamePlayerBuild.Mono();
        [DarkNightsWorkbenchCommand(Root + "Build/Windows Mono Map State", "构建地图状态 Mono")]
        private static void BuildMapStateMono() => GamePlayerBuild.MapStateMono();
        [DarkNightsWorkbenchCommand(Root + "Build/Windows IL2CPP", "构建 Windows IL2CPP")]
        private static void BuildIl2Cpp() => GamePlayerBuild.Il2Cpp();
        [DarkNightsWorkbenchCommand(Root + "Verify/Session Lifecycle", "检查会话生命周期")]
        private static void VerifySessionLifecycle() => SessionLifecycleProbe.Run();
        [DarkNightsWorkbenchCommand(Root + "Verify/Native UI Runtime", "检查原生界面运行")]
        private static void VerifyNativeUi() => NativeUiRuntimeProbe.Run();

    }
}
