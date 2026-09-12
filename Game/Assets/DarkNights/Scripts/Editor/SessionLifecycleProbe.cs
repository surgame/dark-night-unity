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
                    var session = network.ActiveSession;
                    var server = network.Server;
                    Check("one_authority_" + run, session != null && session.StartCount == 1 &&
                        ReferenceEquals(session.Server, server));
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
                    if (run == 1)
                    {
                        InstanceFinder.ClientManager.StopConnection();
                        await Until(() => !InstanceFinder.ClientManager.Started && server.Authority.PlayerCount == 0);
                        long tick = server.Authority.ServerTick;
                        await Until(() => server.Authority.ServerTick > tick);
                        Check("client_stop_preserves_server", network.Hosting && ReferenceEquals(network.Server, server) &&
                            !server.Authority.Closed && session.StartCount == 1);
                        Check("client_stop_clears_replica", network.Client.Replica.Current == null);
                    }
                    if (run == 2)
                    {
                        InstanceFinder.ServerManager.StopConnection(true);
                        await Until(() => server.Authority.Closed && network.Server == null);
                        long tick = server.Authority.ServerTick;
                        await Task.Delay(200);
                        Check("server_stop_stops_authority", tick == server.Authority.ServerTick && session.Server == null);
                    }
                    network.Disconnect();
                    await Until(() => !network.Hosting && !InstanceFinder.ClientManager.Started && network.Client.Replica.Current == null && entities.Count == 0 && effects.EffectCount == 0);
                    Check("split_cache_cleared_" + run, field.GetValue(InstanceFinder.ClientManager) == null);
                    Check("authority_disposed_" + run, server.Authority.Closed && network.ActiveSession == null);
                }
                await network.Connect(true, "127.0.0.1", 28210);
                await Until(() => network.Client.Ready && entities.Count == 17);
                var despawned = network.Server;
                InstanceFinder.ServerManager.Despawn(network.ActiveSession.Owner.gameObject);
                await Until(() => network.Server == null && network.Client.Replica.Current == null &&
                    entities.Count == 0 && effects.EffectCount == 0);
                Check("object_despawn_clears_world_without_disconnect", network.Hosting &&
                    InstanceFinder.ClientManager.Started && despawned.Authority.Closed);
                network.Disconnect();
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
