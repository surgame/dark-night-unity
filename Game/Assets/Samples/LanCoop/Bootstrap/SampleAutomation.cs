using System;
using System.IO;
using DarkNights.Samples.LanCoop.Core;
using DarkNights.Samples.LanCoop.Runtime;
using GameCore.NetworkCommands;
using UnityEngine;

namespace DarkNights.Samples.LanCoop.Bootstrap
{
    /// <summary>显式 -sample-report 启用的多进程驱动适配；原子报告与单调文件命令可复跑，没有后台定时任务。</summary>
    public sealed class SampleAutomation : MonoBehaviour
    {
        public SampleNetwork Network;
        private readonly SampleReport report = new SampleReport();
        private string path, role, address;
        private ushort port;
        private float nextPoll;
        private void Start()
        {
            path = Argument("-sample-report", "");
            if (string.IsNullOrEmpty(path)) { enabled = false; return; }
            role = Argument("-sample-role", "client");
            address = Argument("-sample-address", "127.0.0.1");
            port = ushort.Parse(Argument("-sample-port", "17877"));
            report.role = role;
            report.processId = System.Diagnostics.Process.GetCurrentProcess().Id;
            report.unityVersion = Application.unityVersion;
#if ENABLE_IL2CPP
            report.scriptingBackend = "IL2CPP";
#else
            report.scriptingBackend = "Mono";
#endif
            Network.Result += OnResult;
            Application.logMessageReceived += OnLog;
            int latency = int.Parse(Argument("-sample-latency", "0"));
            Network.Manager.TransportManager.LatencySimulator.SetLatency(latency);
            Network.Manager.TransportManager.LatencySimulator.SetEnabled(latency > 0);
            Network.Connect(role == "host", address, port);
        }
        private void Update()
        {
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + .1f;
            try
            {
                if (File.Exists(path + ".input"))
                {
                    var control = JsonUtility.FromJson<SampleControl>(File.ReadAllText(path + ".input"));
                    if (control != null && control.id > report.inputId)
                    {
                        report.inputId = control.id;
                        Execute(control);
                    }
                }
                var state = Network.Replica;
                report.ready = Network.Ready;
                report.status = Network.Status;
                report.joinCount = Network.JoinCount;
                report.senderCount = NetworkCommandGateway.Instance.RegisteredSenderCount;
                if (state != null)
                {
                    report.epoch = state.Epoch;
                    report.revision = state.Revision;
                    report.coins = state.Coins;
                    report.purchases = state.Purchases;
                    report.occupant = state.Occupant;
                    report.paused = state.Paused;
                    report.ticks = state.SimulationTicks;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(report, true));
                if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
                else File.Move(path + ".tmp", path);
            }
            catch (IOException) { /* 报告读取瞬间的文件占用在下一次轮询重试。 */ }
        }
        private void Execute(SampleControl control)
        {
            switch (control.action)
            {
                case "Disconnect": Network.Disconnect(); break;
                case "Connect": Network.Connect(role == "host", address, port); break;
                case "Quit": Application.Quit(0); break;
                case "Screenshot": ScreenCapture.CaptureScreenshot(path + ".png"); break;
                default:
                    if (Enum.TryParse(control.action, out SampleOperation operation))
                        Network.Send(operation, control.entity, control.fault, control.repeat);
                    else report.error = "Unknown automation action: " + control.action;
                    break;
            }
        }
        private void OnResult(long sequence, string code, int epoch, int revision)
        {
            report.results.Add(sequence + ":" + code + ":" + epoch + ":" + revision);
            if (report.results.Count > 128) report.results.RemoveAt(0);
        }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error) report.error = message + "\n" + stack;
        }
        private static string Argument(string key, string fallback)
        {
            var args = System.Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
        private void OnDestroy()
        {
            Network.Result -= OnResult;
            Application.logMessageReceived -= OnLog;
        }
    }
}
