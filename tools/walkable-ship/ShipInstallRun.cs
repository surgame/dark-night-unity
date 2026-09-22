using DarkNights.Editor;
/// <summary>显式首版安装桥接；供已完成编译的 Local Editor 单次调用，资源生成规则由正式安装器拥有。</summary>
public static class ShipInstallRun
{
    public static string Run() { ShipAssetSetup.Install(); return "Ship assets installed and reopened."; }
}
