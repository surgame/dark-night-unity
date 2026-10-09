using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkNights.Runtime.Diagnostics;
using DarkNights.Runtime.Framework;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using Runtime.Utils;

namespace DarkNights.Runtime.Framework
{
    /// <summary>
    /// 六个正式UGUI Prefab的短期预加载租约；只加载模板，实例仍由UGUIManager按固定顺序装配。
    /// 租约覆盖全部面板创建及接线，之后归还；实际实例使用既有工厂持有的独立常驻引用。
    /// </summary>
    public sealed class SessionUiResources : IDisposable
    {
        private static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[]
        {
            "Chrome", "MainMenu", "PauseMenu", "Help", "Result", "Hero"
        });
        private readonly ComponentAssetLease<ObjectInstance>[] leases;
        public static IReadOnlyList<string> PanelNames => Names;

        private SessionUiResources(ComponentAssetLease<ObjectInstance>[] leases) { this.leases = leases; }

        public static async UniTask<SessionUiResources> Prepare(CancellationToken cancellationToken)
        {
            BootstrapStartupTrace.Mark("UiPreloadStarted");
            ObjectDefinition[] definitions = Names.Select(name => ObjectDefinitionDatabase.Instance
                .GetDefinitionByKey("ui." + name.ToLowerInvariant())).ToArray();
            if (definitions.Any(definition => definition == null))
                throw new InvalidOperationException("Missing native UI definition.");
            var prepared = await StartupResourceBatch.Load(definitions,
                (definition, token) => FastInstantiator.AcquireComponentAsync<ObjectInstance>(definition.PrefabRef, token),
                cancellationToken);
            BootstrapStartupTrace.Mark("UiPreloadReady");
            return new SessionUiResources(prepared);
        }

        public void Dispose()
        {
            foreach (ComponentAssetLease<ObjectInstance> lease in leases) lease.Dispose();
        }
    }
}
