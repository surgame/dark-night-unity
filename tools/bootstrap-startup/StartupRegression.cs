using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameCore.NetworkCommands;
using GameCore.Objects.NetworkStates;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>在空闲 Editor 中验证启动根组件和生成注册范围；结束时恢复注册器与状态表，不修改场景资产。</summary>
public static class StartupRegression
{
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

    public static object Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Editor must be idle.");
        var checks = new List<string>();
        var root = new GameObject("BootstrapComponentProbe", typeof(RectTransform));
        var catalog = typeof(GeneratedGenericTypeRegistryCatalog);
        var states = (List<Action>)catalog.GetField("StateDataRegistrars", PrivateStatic).GetValue(null);
        var commands = (List<Action>)catalog.GetField("NetworkCommandRegistrars", PrivateStatic).GetValue(null);
        var scopes = (Dictionary<Action, HashSet<string>>)catalog.GetField("Scopes", PrivateStatic).GetValue(null);
        var oldStates = states.ToArray();
        var oldCommands = commands.ToArray();
        var oldScopes = scopes.ToArray();
        var stateTypes = GenericTypeRegistry<IStateData>.RegisteredTypes
            .ToDictionary(t => t, t => GenericTypeRegistry<IStateData>.GetId(t));
        var commandTypes = GenericTypeRegistry<INetworkCommand>.RegisteredTypes
            .ToDictionary(t => t, t => GenericTypeRegistry<INetworkCommand>.GetId(t));
        bool statesInitialized = GenericTypeRegistry<IStateData>.IsInitialized;
        bool commandsInitialized = GenericTypeRegistry<INetworkCommand>.IsInitialized;
        try
        {
            var ensure = typeof(Runtime.AppStartup.UGUIRuntimeStartupModule)
                .GetMethod("EnsureRootComponents", PrivateStatic);
            ensure.Invoke(null, new object[] { root });
            Require(root.GetComponent<Canvas>() != null && root.GetComponent<CanvasScaler>() != null &&
                root.GetComponent<GraphicRaycaster>() != null, "missing components are added", checks);
            ensure.Invoke(null, new object[] { root });
            Require(root.GetComponents<Canvas>().Length == 1 && root.GetComponents<CanvasScaler>().Length == 1 &&
                root.GetComponents<GraphicRaycaster>().Length == 1, "repeated initialization reuses components", checks);
            UnityEngine.Object.DestroyImmediate(root.GetComponent<CanvasScaler>());
            ensure.Invoke(null, new object[] { root });
            Require(root.GetComponent<CanvasScaler>() != null, "destroyed scaler is recreated", checks);

            catalog.GetMethod("Reset", PrivateStatic).Invoke(null, null);
            foreach (string name in new[] { "StateDataGeneratedRegistry", "NetworkCommandGeneratedRegistry" })
            {
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("YYGC.Generated." + name)).Single(t => t != null);
                type.GetMethod("RegisterRegistrar", PrivateStatic).Invoke(null, null);
            }
            GenericTypeRegistry<IStateData>.Reset();
            GenericTypeRegistry<INetworkCommand>.Reset();
            GeneratedGenericTypeRegistryCatalog.RegisterStateData();
            GeneratedGenericTypeRegistryCatalog.RegisterNetworkCommands();
            var validateState = typeof(StateDataTypeStartupModule).GetMethod("ValidateGeneratedRegistry", PrivateStatic);
            var validateCommand = typeof(NetworkCommandStartupModule).GetMethod("ValidateGeneratedRegistry", PrivateStatic);
            validateState.Invoke(null, null);
            validateCommand.Invoke(null, null);
            checks.Add("generated state and command startup validation succeeds");
            var sample = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("DarkNights.Samples.LanCoop.Runtime.CampState")).Single(t => t != null);
            Require(!GeneratedGenericTypeRegistryCatalog.IsStateDataTypeInScope(sample) &&
                !GenericTypeRegistry<IStateData>.RegisteredTypes.Contains(sample), "independent sample stays outside host registry", checks);
            var required = typeof(DarkNights.Runtime.Objects.ActorState);
            var generated = GenericTypeRegistry<IStateData>.RegisteredTypes
                .ToDictionary(t => t, t => GenericTypeRegistry<IStateData>.GetId(t));
            GenericTypeRegistry<IStateData>.Reset();
            foreach (var entry in generated.Where(e => e.Key != required))
                GenericTypeRegistry<IStateData>.Register(entry.Key, entry.Value);
            bool missingRejected = false;
            try { validateState.Invoke(null, null); }
            catch (TargetInvocationException e)
            {
                missingRejected = e.InnerException is InvalidOperationException &&
                    e.InnerException.Message.Contains(required.FullName);
            }
            Require(missingRejected, "missing real game state is still rejected", checks);
            catalog.GetMethod("Reset", PrivateStatic).Invoke(null, null);
            Require(GeneratedGenericTypeRegistryCatalog.IsStateDataTypeInScope(sample),
                "missing scope metadata retains strict validation", checks);
            return new { passed = checks.Count, checks };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            states.Clear(); states.AddRange(oldStates);
            commands.Clear(); commands.AddRange(oldCommands);
            scopes.Clear(); foreach (var entry in oldScopes) scopes.Add(entry.Key, entry.Value);
            GenericTypeRegistry<IStateData>.Reset();
            foreach (var entry in stateTypes) GenericTypeRegistry<IStateData>.Register(entry.Key, entry.Value);
            if (statesInitialized) GenericTypeRegistry<IStateData>.MarkInitialized();
            GenericTypeRegistry<INetworkCommand>.Reset();
            foreach (var entry in commandTypes) GenericTypeRegistry<INetworkCommand>.Register(entry.Key, entry.Value);
            if (commandsInitialized) GenericTypeRegistry<INetworkCommand>.MarkInitialized();
        }
    }

    private static void Require(bool condition, string name, ICollection<string> checks)
    {
        if (!condition) throw new InvalidOperationException(name);
        checks.Add(name);
    }
}
