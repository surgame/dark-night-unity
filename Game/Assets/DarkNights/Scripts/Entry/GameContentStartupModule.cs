using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Config;
using DarkNights.Runtime.Framework;
using GameCore.NetworkCommands;
using GameCore.Objects.NetworkStates;
using Runtime.AppStartup;
using UnityEngine;
using UnityEngine.Scripting;

namespace DarkNights.Entry
{
    /// <summary>
    /// 将正式配置加载接入 YYGC 的应用启动流程，只有完整解析成功后才注册共享目录。
    /// 目录归应用上下文所有，不建立世界或网络会话；失败阻止 AppStartup 进入 Ready。
    /// </summary>
    [Serializable]
    [Preserve]
    [AppStartupModule]
    public sealed class GameContentStartupModule : IAppStartupModule
    {
        public string Name => "Dark Nights 规则配置";
        public string Description => "加载并验证灰松谷数值和波次，不创建正式玩法。";
        public string Category => "游戏内容";
        public int Order => 8500;
        public bool Required => true;
        public IReadOnlyList<Type> Dependencies => new[]
        {
            typeof(ObjectV2RuntimeStartupModule),
            typeof(StateDataTypeStartupModule),
            typeof(NetworkCommandStartupModule)
        };

        public async UniTask InitializeAsync(AppStartupContext context, CancellationToken cancellationToken)
        {
            GameCatalog catalog = await GameCatalogLoader.LoadAsync(cancellationToken);
            FormalObjectCatalog.ValidateRuntime();
            new DefinitionRuleIndex(GameCore.Objects.Definition.ObjectDefinitionDatabase.Instance).Validate(catalog);
            context.Register(catalog);
            Debug.Log($"DARK_NIGHTS_CONTENT_READY level={catalog.Level.Id} seed={catalog.Level.Seed} " +
                $"units={catalog.Balance.Units.Count} buildings={catalog.Balance.Buildings.Count} " +
                $"worksites={catalog.Balance.Worksites.Count} waves={catalog.Level.Waves.Count}");
            Debug.Log(FormalObjectCatalog.RuntimeSummary());
        }
    }
}
