using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>
    /// 仅在显式 dn-role 命令行参数存在时启用的正式 Player 验收驱动，操作走与产品相同的 SessionClient。
    /// 只读指定命令文件，报告输出到调用者指定目录；无玩家存档访问，不将测试输入纳入游戏规则或内容来源。
    /// </summary>
    public sealed class SessionAutomation : MonoBehaviour
    {
        private SessionNetwork network;
        private string reportPath, commandPath, role, address;
        private ushort port;
        private int consumed;
        private bool working;
        private double nextPoll;
        private readonly Queue<CommandFeedback> feedback = new Queue<CommandFeedback>();
        private readonly Queue<DarkNights.Runtime.Terrain.TerrainActionResult> terrainFeedback =
            new Queue<DarkNights.Runtime.Terrain.TerrainActionResult>();
        private string error;
        private int peakEffects, peakArrows;
        private int reportRetries;
        private bool pauseOnProjectile;
        private bool fullReport = true;
        private PlayerPerformanceCapture capture;
        private HeroInputPlayback heroInput;

        public static void Install(SessionNetwork network)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string Read(string key, string fallback = "")
            {
                int index = Array.IndexOf(args, key);
                return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
            }
            string role = Read("--dn-role");
            if (role.Length == 0) return;
            var driver = network.gameObject.AddComponent<SessionAutomation>();
            driver.network = network;
            driver.role = role;
            driver.address = Read("--dn-address", "127.0.0.1");
            driver.port = ushort.Parse(Read("--dn-port", "27991"));
            driver.reportPath = Path.GetFullPath(Read("--dn-report"));
            driver.commandPath = Path.GetFullPath(Read("--dn-commands"));
            driver.heroInput = network.gameObject.AddComponent<HeroInputPlayback>();
            driver.heroInput.Initialize(network.Client);
            Directory.CreateDirectory(Path.GetDirectoryName(driver.reportPath));
            network.Client.Feedback += driver.OnFeedback;
            network.Client.TerrainFeedback += driver.OnTerrainFeedback;
            network.Failed += driver.OnFailure;
            if (Array.IndexOf(args, "--dn-metrics") >= 0)
            {
                driver.capture = network.gameObject.AddComponent<PlayerPerformanceCapture>();
                driver.capture.Initialize(network);
            }
        }

        private async void Start()
        {
            try { await network.Connect(role == "host", address, port); }
            catch (Exception exception) { OnFailure(exception); }
        }

        private void OnFeedback(CommandFeedback value)
        {
            feedback.Enqueue(value);
            while (feedback.Count > 64) feedback.Dequeue();
        }
        private void OnTerrainFeedback(DarkNights.Runtime.Terrain.TerrainActionResult value)
        {
            terrainFeedback.Enqueue(value);
            while (terrainFeedback.Count > 64) terrainFeedback.Dequeue();
        }
        private void OnFailure(Exception exception) { error = exception.ToString(); }

        private async void Update()
        {
            if (network == null || working) return;
            bool freezeProjectile = pauseOnProjectile && network.Client.Ready && network.Client.Replica.Current.World.Projectiles.Count > 0;
            if (!freezeProjectile && Time.realtimeSinceStartupAsDouble < nextPoll) return;
            working = true;
            nextPoll = Time.realtimeSinceStartupAsDouble + 0.2;
            try
            {
                if (freezeProjectile)
                {
                    pauseOnProjectile = false;
                    await network.Client.Send(SessionOperation.SetPaused, value: 1);
                }
                if (File.Exists(commandPath))
                {
                    string[] lines = ReadCommands();
                    while (consumed < lines.Length)
                    {
                        var command = JObject.Parse(lines[consumed++]);
                        string operation = (string)command["operation"];
                        if (operation == "disconnect") network.Disconnect();
                        else if (operation == "connect") await network.Connect(role == "host", address, port);
                        else if (operation == "quit") Application.Quit();
                        else if (operation == "metrics-reset") capture.ResetWindow();
                        else if (operation == "report-detail") fullReport = (int?)command["value"] != 0;
                        else if (operation == "metrics") capture.Save(Path.Combine(
                            Path.GetDirectoryName(reportPath), Path.GetFileName((string)command["file"] ?? "metrics.json")));
                        else if (operation == "raw") await ExecuteRaw(command);
                        else if (operation == "terrain")
                            await network.Client.SendTerrain((int)command["u"], (int)command["v"], (string)command["requestId"]);
                        else if (operation == "input" || operation == "input-raw" || operation == "input-hold" || operation == "input-stop")
                            await heroInput.Execute(command);
                        else if (operation == "pause-on-projectile") pauseOnProjectile = true;
                        else if (operation == "capture")
                        {
                            var stage = UnityEngine.Object.FindAnyObjectByType<DarkNights.View.PinewatchStage>();
                            if (command["x"] != null) stage.Focus((float)command["x"]);
                            SessionRenderCapture.Save(stage, Path.Combine(Path.GetDirectoryName(reportPath),
                                Path.GetFileName((string)command["file"] ?? "capture.png")));
                        }
                        else if (operation == "capture-sample")
                            await SessionPresentationCapture.Save(network, command, Path.GetDirectoryName(reportPath));
                        else if (operation == "ui") network.GetComponent<SessionUiController>().ActivateButton((string)command["panel"], (string)command["key"]);
                        else if (operation == "hero-mode")
                            await network.GetComponent<HeroPlayerController>().SetHeroMode((int?)command["value"] != 0);
                        else if (operation == "input-orders")
                        {
                            var input = network.GetComponent<SessionUiController>().Input;
                            input.SelectEntity((int)command["actor"]);
                            input.IssueOrders((float)command["x"], (int?)command["target"] ?? 0);
                        }
                        else
                        {
                            var actors = command["actors"]?.Values<int>().ToArray();
                            await network.Client.Send((SessionOperation)Enum.Parse(typeof(SessionOperation), operation),
                                actors, (int?)command["target"] ?? 0, (float?)command["x"] ?? 0,
                                (string)command["kind"] ?? "", (int?)command["value"] ?? 0, (int?)command["lease"] ?? 0);
                        }
                    }
                }
                var effects = network.GetComponent<SessionEffects>();
                peakEffects = Math.Max(peakEffects, effects.EffectCount);
                peakArrows = Math.Max(peakArrows, effects.ArrowCount);
                long reportStart = capture == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
                var frame = network.Client.Replica.Current;
                var terrainPreview = UnityEngine.Object.FindAnyObjectByType<DarkNights.View.Terrain.TerrainPreview>();
                var report = new JObject
                {
                    ["utc"] = DateTime.UtcNow.ToString("O"), ["role"] = role, ["status"] = network.Status,
                    ["clientStatus"] = network.Client.Status, ["ready"] = network.Client.Ready,
                    ["slot"] = network.Client.PlayerSlot, ["commandsConsumed"] = consumed, ["error"] = error ?? heroInput.Error,
                    ["inputPacketsSent"] = heroInput.PacketsSent,
                    ["ballisticActive"] = effects.BallisticCount, ["ballisticPool"] = effects.BallisticPoolCount,
                    ["terrain"] = network.Terrain == null ? null : new JObject
                    {
                        ["epoch"] = network.Terrain.Epoch, ["seed"] = network.Terrain.Seed,
                        ["sha256"] = network.Terrain.ContentSha256, ["dataReady"] = network.Terrain.DataReady,
                        ["visible"] = network.Terrain.PresentationReady, ["generationMs"] = network.Terrain.GenerationMilliseconds,
                        ["sentBytes"] = network.Terrain.SentBytes
                    },
                    ["feedback"] = JArray.FromObject(feedback),
                    ["terrainFeedback"] = JArray.FromObject(terrainFeedback),
                    ["terrainPresentation"] = terrainPreview == null ? null : new JObject
                    {
                        ["builtPages"] = terrainPreview.BuiltPages,
                        ["changedChunks"] = terrainPreview.LastChangedChunkCount,
                        ["refreshRegions"] = terrainPreview.LastRefreshRegionCount,
                        ["refreshBatches"] = terrainPreview.RefreshBatchCount,
                        ["refreshing"] = terrainPreview.RefreshingReplica
                    },
                    ["frame"] = frame == null || !fullReport ? null : JObject.FromObject(frame),
                    ["reportDetail"] = fullReport ? "full" : "summary", ["publication"] = frame?.Publication ?? 0,
                    ["serverTick"] = frame?.ServerTick ?? 0, ["epoch"] = frame?.Epoch ?? 0,
                    ["readyCount"] = frame?.ReadyCount ?? 0, ["entityCount"] = frame?.World.Identities.Count ?? 0,
                    ["projectileCount"] = frame?.World.Projectiles.Count ?? 0,
                    ["serverPayloadBytes"] = network.Server?.LastPayloadBytes ?? 0,
                    ["storageBusy"] = network.Server?.Storage.Busy ?? false,
                    ["storageStatus"] = network.Server?.Storage.Status ?? "",
                    ["uiPage"] = network.GetComponent<SessionUiController>().Page,
                    ["selected"] = JArray.FromObject(network.GetComponent<SessionUiController>().Input.Selected),
                    ["entityViews"] = network.GetComponent<SessionEntityViews>().Count,
                    ["effectViews"] = effects.EffectCount, ["arrowViews"] = effects.ArrowCount,
                    ["peakEffectViews"] = peakEffects, ["peakArrowViews"] = peakArrows
                };
                string temporary = reportPath + ".tmp";
                File.WriteAllText(temporary, report.ToString());
                try
                {
                    if (File.Exists(reportPath)) File.Replace(temporary, reportPath, null);
                    else File.Move(temporary, reportPath);
                    reportRetries = 0;
                }
                catch (IOException) when (++reportRetries < 10)
                {
                    // Windows 文件扫描或报告读取可短暂阻止替换；下一次轮询重发最新冻结报告。
                }
                capture?.RecordReport((System.Diagnostics.Stopwatch.GetTimestamp() - reportStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            }
            catch (Exception exception) { error = exception.ToString(); Debug.LogException(exception); }
            finally { working = false; }
        }

        private void OnDestroy()
        {
            if (network == null) return;
            network.Client.Feedback -= OnFeedback;
            network.Client.TerrainFeedback -= OnTerrainFeedback;
            network.Failed -= OnFailure;
        }

        private string[] ReadCommands()
        {
            using var stream = new FileStream(commandPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            string text = reader.ReadToEnd();
            int complete = text.LastIndexOf('\n');
            return complete < 0 ? Array.Empty<string>() : text.Substring(0, complete).Split('\n');
        }

        private System.Threading.Tasks.ValueTask ExecuteRaw(JObject command)
        {
            // 仅显式自动化使用，重复／非法业务包仍交给正式 Gateway 和可信服务端权限入口。
            var frame = network.Client.Replica.Current;
            return network.Client.SendFrozen(new SessionRequest(
                (SessionOperation)Enum.Parse(typeof(SessionOperation), (string)command["intent"]),
                (int?)command["protocol"] ?? SessionAuthority.ProtocolVersion, (int?)command["epoch"] ?? frame.Epoch,
                (int?)command["policy"] ?? frame.PolicyRevision, (long)command["sequence"],
                command["actors"]?.Values<int>().ToArray(), (int?)command["target"] ?? 0,
                (float?)command["x"] ?? 0, (string)command["kind"] ?? "", (int?)command["value"] ?? 0,
                (int?)command["lease"] ?? 0));
        }
    }
}
