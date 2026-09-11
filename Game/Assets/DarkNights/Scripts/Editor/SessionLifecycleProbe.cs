using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using DarkNights.Entry;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using FishNet;
using FishNet.Managing.Client;
using Newtonsoft.Json;
using Runtime.AppStartup;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 显式 Editor 运行探针，制造未收齐分片后关闭真实连接，再连续重开同一正式 Host。
    /// 反射仅用于设置第三方内部失败前置条件；不进入 Player，不更改原世界或玩家存档。
    /// </summary>
    public static class SessionLifecycleProbe
    {
        private static bool running;
        [MenuItem("Dark Nights/Verify/Session Lifecycle")]
        public static async void Run()
        {
            if (!Application.isPlaying || running) throw new InvalidOperationException("Run once in Play.");
            running = true;
            var checks = new Dictionary<string, bool>();
            string failure = null;
            void Check(string name, bool ok) { checks[name] = ok; if (!ok) throw new InvalidOperationException(name); }
            try
            {
                await Until(() => UnityEngine.Object.FindAnyObjectByType<SessionEffects>() != null);
                var network = AppStartup.Instance.Context.Resolve<SessionNetwork>();
                var entities = network.GetComponent<SessionEntityViews>();
                var effects = network.GetComponent<SessionEffects>();
                if (network.Client.Replica.Current != null) throw new InvalidOperationException("Probe requires MainMenu.");
                Check("interaction_service_present", GameCore.Interactions.YYInteractionSessionService.Instance != null);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var field = typeof(ClientManager).GetField("_splitReader", flags);
                for (int run = 0; run < 3; run++)
                {
                    await network.Connect(true, "127.0.0.1", 28210);
                    await Until(() => network.Client.Ready && entities.Count == 17);
                    Check("host_ready_" + run, network.Client.PlayerSlot == 0);
                    await network.Client.Send(SessionOperation.SetPaused, value: 1);
                    await network.Client.Send(SessionOperation.Recruit);
                    await Until(() => network.Client.Replica.Current.World.Camp.Population == 8);
                    Check("single_payment_after_restart_" + run, network.Client.Replica.Current.World.Camp.Stock.Food == 48);
                    if (run == 0)
                    {
                        object[] arguments = { 2, null };
                        typeof(ClientManager).GetMethod("TryGetSplitReader", flags).Invoke(InstanceFinder.ClientManager, arguments);
                        object split = arguments[1];
                        split.GetType().GetField("_receivedMessages", flags).SetValue(split, (ushort)1);
                    }
                    network.Disconnect();
                    await Until(() => !network.Hosting && !InstanceFinder.ClientManager.Started && network.Client.Replica.Current == null && entities.Count == 0 && effects.EffectCount == 0);
                    Check("split_cache_cleared_" + run, field.GetValue(InstanceFinder.ClientManager) == null);
                }
            }
            catch (Exception exception) { failure = exception.ToString(); Debug.LogException(exception); }
            finally
            {
                running = false;
                Directory.CreateDirectory("../artifacts/migration");
                File.WriteAllText("../artifacts/migration/session-lifecycle.json", JsonConvert.SerializeObject(new
                {
                    passed = failure == null, checks, error = failure,
                    domainReloadDisabled = EditorSettings.enterPlayModeOptionsEnabled && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0
                }, Formatting.Indented));
            }
        }

        private static async Task Until(Func<bool> condition)
        {
            double deadline = EditorApplication.timeSinceStartup + 20;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Lifecycle condition timed out.");
                await Task.Delay(50);
            }
        }
    }
}
