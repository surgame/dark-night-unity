using System;
using System.IO;
using System.Threading;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;
using DarkNights.Runtime.Save;
using DarkNights.Runtime.Session;
using static DarkNights.Tests.SessionScenario;

namespace DarkNights.Tests
{
    /// <summary>
    /// 通过真实后台文件任务验证授权、冻结时刻、失败保持和新 epoch；每轮使用隔离临时目录。
    /// 不操作玩家存档，不将此单线程驱动当作多进程网络或 Ready 超时验收。
    /// </summary>
    public static class SessionStorageScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            Recovery(check);
            string directory = Path.Combine(Path.GetTempPath(), "dark-nights-storage-" + Guid.NewGuid().ToString("N"));
            try
            {
                using var authority = Open(catalog, layout, out var host, out var guest);
                var files = new GameSaveStore(directory, Codec(catalog, layout));
                using var storage = new SessionStorage(authority, files);
                var codec = Codec(catalog, layout);
                check(Execute(authority, guest, Request(authority, SessionOperation.Save, 1)).Code == SessionResultCode.PermissionDenied,
                    "Guest cannot trigger host filesystem save");
                Execute(authority, host, Request(authority, SessionOperation.SetPaused, 1, value: 1));
                string initial = codec.Serialize(authority.CaptureWorld());
                var save = Request(authority, SessionOperation.Save, 2, value: 3);
                var receipt = Execute(authority, host, save);
                check(receipt.Code == SessionResultCode.Applied && !authority.Loading && storage.Busy,
                    "Save accepts a frozen job without freezing the world");
                check(ReferenceEquals(authority.Submit(host, save), receipt), "Repeated save uses the same accepted job");
                check(Execute(authority, host, Request(authority, SessionOperation.BeginLoad, 3)).Code == SessionResultCode.Loading,
                    "Overlapping storage requests are rejected before starting another task");
                Execute(authority, guest, Request(authority, SessionOperation.Recruit, 2));
                Finish(storage);
                check(files.Read(3) == initial, "Saved state matches request boundary before subsequent recruitment");
                Execute(authority, host, Request(authority, SessionOperation.SetControlMode, 4, value: 1));
                Execute(authority, host, Request(authority, SessionOperation.BeginLoad, 5, value: 3));
                Finish(storage);
                check(authority.Epoch == 2 && authority.ReadyCount == 0 && authority.ControlMode == CampControlMode.HostOnly,
                    "Background load advances epoch, resets Ready and preserves room policy");
                check(codec.Serialize(authority.CaptureWorld()) == initial, "Background file load restores exact frozen world");
                authority.AcknowledgeReady(host, authority.Epoch, authority.Revision);
                File.WriteAllText(Path.Combine(directory, "slot-04.dnsave.json"), "{\"format\":");
                Execute(authority, host, Request(authority, SessionOperation.BeginLoad, 1, value: 4));
                Finish(storage);
                check(authority.Epoch == 2 && !authority.Loading && codec.Serialize(authority.CaptureWorld()) == initial && storage.Status.Contains("失败"),
                    "Corrupt file failure preserves epoch, pause, world and provides feedback");
                Execute(authority, host, Request(authority, SessionOperation.BeginLoad, 2, value: 9));
                Finish(storage);
                check(!authority.Loading && authority.Epoch == 2, "Missing slot releases loading without replacing world");
                Execute(authority, host, Request(authority, SessionOperation.Restart, 3));
                Finish(storage);
                check(authority.Epoch == 3 && authority.CaptureWorld().Elapsed == 0 && authority.ReadyCount == 0 && authority.ControlMode == CampControlMode.HostOnly,
                    "Restart creates a fresh camp in the existing room and resets Ready");
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        private static void Finish(SessionStorage storage)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            do { storage.Advance(); if (!storage.Busy) return; Thread.Sleep(1); } while (DateTime.UtcNow < deadline);
            throw new TimeoutException("Storage task failed to complete.");
        }

        private static void Recovery(Action<bool, string> check)
        {
            var slots = new SessionRecoverySlots();
            check(slots.Claim(true, "", 0) == 0 && slots.Claim(true, "", 0) == -1, "Only one trusted local host can claim slot zero");
            int first = slots.Claim(false, "", 0);
            string token = slots.Token(first);
            check(first == 1 && token.Length == 44 && slots.Claim(false, token, 1) == -1, "An active guest cannot be displaced by replaying its credential");
            slots.Release(first, 2);
            check(slots.Claim(false, "", 3) == 2, "Disconnected guest slot is reserved from new participants");
            check(slots.Claim(false, token, 4) == 1 && slots.Token(1) != token, "Recovery preserves slot and rotates credential");
            slots.Release(1, 5);
            check(slots.Claim(false, token, 6) == -1, "A rotated credential cannot be replayed");
            token = slots.Token(1);
            check(slots.Claim(false, token, 125) == -1 && slots.Claim(false, "", 125) == 1, "Expired reservation rejects old credential and frees slot");
            check(new SessionRecoverySlots().Claim(false, token, 0) == -1, "Recovery token is scoped to the original room");
        }
    }
}
