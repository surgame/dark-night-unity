using System;
using UnityEditor;
/// <summary>按失败修正触发一次脚本导入，并读取实际已加载程序集版本作为后续验收屏障。</summary>
public static class ShipRefresh
{
    public static string Run() { AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); return "Current ship batch refresh submitted."; }
    public static string Check() {
        var field = typeof(DarkNights.Core.Save.SessionSnapshot).GetField("CurrentVersion");
        return "compiling=" + EditorApplication.isCompiling + "; shipFraming=" + (typeof(DarkNights.View.PinewatchStage).GetMethod("FocusShip") != null) + "; releasePilot=" + (typeof(DarkNights.Runtime.Objects.ExpeditionShip).GetMethod("ReleasePilot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) != null) + "; transformGate=" + (typeof(DarkNights.Core.Save.ExpeditionShipValidator).GetMethod("Transform") != null) + "; snapshot=" + (field?.GetRawConstantValue()?.ToString() ?? "old-assembly");
    }
}
