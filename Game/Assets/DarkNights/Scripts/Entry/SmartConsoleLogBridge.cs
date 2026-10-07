using System;
using System.Collections.Generic;
using ED.SC;
using ED.SC.Components;
using TMPro;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>
    /// Smart Console 的有界 Unity 日志接入；队列、单帧绘制和显示数量都有上限，面板自身的缺字警告不反馈进面板。
    /// 只拥有运行时字体副本和日志展示数据，保留原插件、作者字体及 YYGC 日志入口。
    /// </summary>
    public sealed class SmartConsoleLogBridge : MonoBehaviour
    {
        public const int MaximumQueued = 200;
        public const int MaximumRendered = 400;
        private const int PerFrame = 12;
        private readonly Queue<PendingLog> pending = new Queue<PendingLog>();
        private readonly Dictionary<TMP_FontAsset, TMP_FontAsset> fonts = new Dictionary<TMP_FontAsset, TMP_FontAsset>();
        private ConsoleSystem console;
        private TMP_FontAsset chinese;
        private bool forwarding, subscribed;
        private float nextInspection;
        public int Queued => pending.Count;
        public int Rendered { get; private set; }
        public int Dropped { get; private set; }
        public int SuppressedFontWarnings { get; private set; }
        public int Rotations { get; private set; }

        /// <summary>替换插件的无界应用日志订阅；字体仅在本次运行中创建，不修改 SC.Prefs 或源字体资产。</summary>
        public void Initialize(ConsoleSystem source)
        {
            console = source ?? throw new ArgumentNullException(nameof(source));
            console.ShowApplicationLogs = false;
            chinese = TMP_FontAsset.CreateFontAsset("Microsoft YaHei", "Regular", 40);
            if (chinese != null)
            {
                chinese.name = "Dark Nights Console CJK";
                chinese.isMultiAtlasTexturesEnabled = false;
                chinese.hideFlags = HideFlags.DontSave;
            }
            ApplyFonts();
            Rendered = SmartConsole.GetLogs().Length;
            Subscribe();
        }

        private void Capture(string message, string stack, LogType type)
        {
            if (forwarding) return;
            if (IsConsoleFontWarning(message)) { SuppressedFontWarnings++; return; }
            if (pending.Count == MaximumQueued) { pending.Dequeue(); Dropped++; }
            string bounded = message == null ? "" : message.Length <= 2048 ? message : message.Substring(0, 2048) + "…";
            pending.Enqueue(new PendingLog(bounded, type));
        }

        /// <summary>仅识别日志面板自己的 TMP 缺字警告；其他对象的警告仍正常转入 F10。</summary>
        public static bool IsConsoleFontWarning(string message) => message != null &&
            message.StartsWith("The character with Unicode value ", StringComparison.Ordinal) &&
            (message.Contains("[LogText]") || message.Contains("[AutocompleteText]"));

        private void Update()
        {
            if (console == null) return;
            if (pending.Count == 0 && Time.unscaledTime < nextInspection) return;
            nextInspection = Time.unscaledTime + .1f;
            forwarding = true;
            try
            {
                Rendered = SmartConsole.GetLogs().Length;
                if (Rendered >= MaximumRendered)
                { SmartConsole.Clear(); Rendered = 0; Rotations++; }
                int count = Math.Min(PerFrame, Math.Min(pending.Count, MaximumRendered - Rendered));
                for (int i = 0; i < count; i++)
                {
                    var entry = pending.Dequeue();
                    if (entry.Type == LogType.Warning) SmartConsole.LogWarning(entry.Text);
                    else if (entry.Type is LogType.Error or LogType.Exception or LogType.Assert) SmartConsole.LogError(entry.Text);
                    else SmartConsole.Log(entry.Text);
                    Rendered++;
                }
                ApplyFonts();
            }
            finally { forwarding = false; }
        }

        private void Subscribe()
        {
            if (subscribed || console == null) return;
            Application.logMessageReceived += Capture;
            subscribed = true;
        }

        private void OnEnable() => Subscribe();

        private void ApplyFonts()
        {
            if (chinese == null || console == null) return;
            foreach (var text in console.GetComponentsInChildren<TMP_Text>(true))
            {
                var source = text.font;
                if (source == null || source == chinese || fonts.ContainsValue(source)) continue;
                if (!fonts.TryGetValue(source, out var copy))
                {
                    copy = Instantiate(source);
                    copy.name = source.name + " Dark Nights Console";
                    copy.hideFlags = HideFlags.DontSave;
                    copy.fallbackFontAssetTable = new List<TMP_FontAsset> { chinese };
                    if (source.fallbackFontAssetTable != null) copy.fallbackFontAssetTable.AddRange(source.fallbackFontAssetTable);
                    fonts.Add(source, copy);
                }
                text.font = copy;
            }
        }

        private void OnDisable()
        {
            if (subscribed) Application.logMessageReceived -= Capture;
            subscribed = false;
            pending.Clear();
        }

        private void OnDestroy()
        {
            foreach (var copy in fonts.Values)
            {
                if (copy == null) continue;
                // TMP.OnDestroy 会销毁图集和材质；这些引用归作者资产，副本释放前必须先断开。
                copy.atlasTextures = Array.Empty<Texture2D>();
                copy.material = null;
                Destroy(copy);
            }
            fonts.Clear();
            if (chinese == null) return;
            Destroy(chinese);
        }

        /// <summary>等待送入面板的一条有限文本和原始级别；不保留完整堆栈或引擎对象引用。</summary>
        private readonly struct PendingLog
        {
            internal readonly string Text;
            internal readonly LogType Type;
            internal PendingLog(string text, LogType type) { Text = text; Type = type; }
        }
    }
}
