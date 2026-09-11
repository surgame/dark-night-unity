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
        private string error;

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
            Directory.CreateDirectory(Path.GetDirectoryName(driver.reportPath));
            network.Client.Feedback += driver.OnFeedback;
            network.Failed += driver.OnFailure;
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
        private void OnFailure(Exception exception) { error = exception.ToString(); }

        private async void Update()
        {
            if (network == null || working || Time.realtimeSinceStartupAsDouble < nextPoll) return;
            working = true;
            nextPoll = Time.realtimeSinceStartupAsDouble + 0.2;
            try
            {
                if (File.Exists(commandPath))
                {
                    string[] lines = File.ReadAllLines(commandPath);
                    while (consumed < lines.Length)
                    {
                        var command = JObject.Parse(lines[consumed++]);
                        string operation = (string)command["operation"];
                        if (operation == "disconnect") network.Disconnect();
                        else if (operation == "connect") await network.Connect(role == "host", address, port);
                        else if (operation == "quit") Application.Quit();
                        else
                        {
                            var actors = command["actors"]?.Values<int>().ToArray();
                            await network.Client.Send((SessionOperation)Enum.Parse(typeof(SessionOperation), operation),
                                actors, (int?)command["target"] ?? 0, (float?)command["x"] ?? 0,
                                (string)command["kind"] ?? "", (int?)command["value"] ?? 0);
                        }
                    }
                }
                var report = new JObject
                {
                    ["utc"] = DateTime.UtcNow.ToString("O"), ["role"] = role, ["status"] = network.Status,
                    ["clientStatus"] = network.Client.Status, ["ready"] = network.Client.Ready,
                    ["slot"] = network.Client.PlayerSlot, ["commandsConsumed"] = consumed, ["error"] = error,
                    ["feedback"] = JArray.FromObject(feedback),
                    ["frame"] = network.Client.Replica.Current == null ? null : JObject.FromObject(network.Client.Replica.Current),
                    ["serverPayloadBytes"] = network.Server?.LastPayloadBytes ?? 0
                };
                string temporary = reportPath + ".tmp";
                File.WriteAllText(temporary, report.ToString());
                if (File.Exists(reportPath)) File.Replace(temporary, reportPath, null);
                else File.Move(temporary, reportPath);
            }
            catch (Exception exception) { error = exception.ToString(); Debug.LogException(exception); }
            finally { working = false; }
        }

        private void OnDestroy()
        {
            if (network == null) return;
            network.Client.Feedback -= OnFeedback;
            network.Failed -= OnFailure;
        }
    }
}
