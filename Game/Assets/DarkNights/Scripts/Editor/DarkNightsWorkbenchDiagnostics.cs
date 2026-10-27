using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Save;
using DarkNights.Runtime.Session;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 开发页的联机、存储与构建只读摘要；仅进入或显式刷新时采样，不定时扫描磁盘或捕获权威状态。
    /// 存档只检查固定十槽的文件信息，构建参数取自实际构建器；所有写入继续交给原有明确操作。
    /// </summary>
    internal static class DarkNightsWorkbenchDiagnostics
    {
        internal static void Build(DarkNightsWorkbenchPage page, Action<DarkNightsWorkbenchEntry> execute)
        {
            page.Heading("联机状态");
            page.Value("正式端口", "游戏 UDP27777 / LAN发现 UDP27778");
            page.Value("版本", "协议 " + SessionAuthority.ProtocolVersion + " / 存档 v" + ObjectWorldSaveJson.FormatVersion);
            var state = page.Value("当前连接", "");
            void Refresh()
            {
                var network = Application.isPlaying ? UnityEngine.Object.FindAnyObjectByType<SessionNetwork>() : null;
                state.text = network == null ? "尚无运行会话；内容兼容与Ready仍由原握手验证" :
                    (network.Hosting ? "Host" : "Client") + " · " + network.Status +
                    " · Ready=" + (network.Client?.Ready == true) + " · 槽位=" + (network.Client?.PlayerSlot ?? -1);
            }
            Refresh();
            page.Action("刷新连接摘要", Refresh, false);
            page.Note("完整运行操作仍在F1调试面板；此页只读取状态，不连接房间、切权限或启动快速局。");
            page.Extend(extra =>
            {
                Storage(extra);
                extra.Heading("Windows Mono构建输入");
                extra.Value("选项", "Windows x64 / Mono / Development");
                extra.Value("默认输出", GamePlayerBuild.DefaultOutput("mono"));
                foreach (string path in GamePlayerBuild.GetScenePaths()) extra.Value("包含场景", path);
                extra.Note("场景清单直接来自GamePlayerBuild；构建命令会先构建Addressables。其他后端及专项构建见下方原命令。");
                extra.Heading("安装、构建与专项验证");
                extra.Add(DarkNightsWorkbenchCommands.CreatePanel(execute));
            });
        }

        private static string SaveDirectory()
        {
            var network = Application.isPlaying ? UnityEngine.Object.FindAnyObjectByType<SessionNetwork>() : null;
            if (!string.IsNullOrEmpty(network?.SaveDirectory)) return network.SaveDirectory;
            string[] arguments = System.Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "--dn-save-dir");
            string root = index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] :
                Path.Combine(Application.persistentDataPath, "Saves");
            return Path.Combine(Path.GetFullPath(root), "v" + ObjectWorldSaveJson.FormatVersion);
        }

        private static void Storage(DarkNightsWorkbenchPage page)
        {
            page.Heading("存档位置与槽位");
            var directory = page.Value("当前／预计目录", "");
            var slots = page.Value("槽位占用", "");
            var recent = page.Value("最近修改", "");
            void Refresh()
            {
                string path = SaveDirectory(); directory.text = path;
                var files = Enumerable.Range(0, GameSaveStore.SlotCount).Select(slot =>
                    new FileInfo(Path.Combine(path, "slot-" + slot.ToString("D2") + ".dnsave.json")))
                    .Where(file => file.Exists).ToArray();
                slots.text = files.Length + " / " + GameSaveStore.SlotCount;
                recent.text = files.OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault()?.Name ?? "尚无存档";
            }
            Refresh();
            page.Action("刷新槽位信息", Refresh, false);
            page.Action("打开存档目录", () =>
            {
                string path = SaveDirectory();
                if (!Directory.Exists(path)) throw new DirectoryNotFoundException("目录尚未创建；首次正式保存后再打开。");
                EditorUtility.RevealInFinder(path);
            }, false);
            page.Note("未运行时显示本机启动参数解析的正式目录；运行时显示会话实际目录，快速局另在QuickTests子目录。只查文件信息，不加载、删除、迁移或覆盖存档。");
        }
    }
}
