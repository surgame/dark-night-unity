using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DarkNights.Entry;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using DarkNights.View;
using Newtonsoft.Json.Linq;
using Runtime.AppStartup;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 显式 Play 验收入口，经正式菜单及命令链验证音乐、暂停中的效果、真实战斗箭矢与离开清理。
    /// 探针只读统计投影和本地实例；不篡改规则状态、不改写玩家存档，结果输出到 artifacts。
    /// </summary>
    public static class NativeEffectsRuntimeProbe
    {
        private static bool running;
        [UnityEditor.MenuItem("Dark Nights/Verify/Native Effects Runtime")]
        public static async void Run()
        {
            if (!Application.isPlaying || running) throw new InvalidOperationException("Run once in Play.");
            running = true;
            var checks = new Dictionary<string, bool>();
            string error = null;
            void Check(string name, bool value) { checks[name] = value; if (!value) throw new InvalidOperationException(name); }
            try
            {
                await Until(() => UnityEngine.Object.FindAnyObjectByType<CampAudio>() != null, 15);
                var network = AppStartup.Instance.Context.Resolve<SessionNetwork>();
                var ui = network.GetComponent<SessionUiController>();
                var effects = network.GetComponent<SessionEffects>();
                var entities = network.GetComponent<SessionEntityViews>();
                var stage = UnityEngine.Object.FindAnyObjectByType<PinewatchStage>();
                if (network.Client.Replica.Current != null) throw new InvalidOperationException("Probe requires fresh MainMenu.");
                Check("native_music_plays_original_clip", UnityEngine.Object.FindAnyObjectByType<CampAudio>()
                    .GetComponentsInChildren<AudioSource>().Any(a => a.clip != null && a.clip.name == "snd_vindsvept_hollow" && a.isPlaying));
                ui.ActivateButton("MainMenu", "NewGame");
                await Until(() => network.Client.Ready && entities.Count == 17, 15);
                ui.ActivateButton("Chrome", "Pause");
                await Until(() => network.Client.Replica.Current.Paused, 5);
                int worker = network.Client.Replica.Current.World.Actors.First(a => a.Kind == "worker").Id;
                await network.Client.Send(SessionOperation.IssueOrders, new[] { worker }, x: 510);
                await Until(() => effects.EffectCount > 0, 5);
                Check("paused_command_creates_one_ring", effects.EffectCount == 1);
                await Task.Delay(1000);
                Check("paused_ring_expires_without_snapshot_replay", effects.EffectCount == 0 && network.Client.Replica.Current.Paused);
                ui.ActivateButton("Chrome", "Pause");
                await Until(() => !network.Client.Replica.Current.Paused, 5);
                ui.ActivateButton("Chrome", "Speed");
                ui.ActivateButton("Chrome", "Night");
                bool arrows = false, floating = false, corpse = false;
                double end = Time.unscaledTimeAsDouble + 90;
                while (Time.unscaledTimeAsDouble < end && !(arrows && floating && corpse))
                {
                    if (network.Client.Replica.Current == null) throw new InvalidOperationException("Battle disconnected: " + network.Status);
                    arrows |= effects.ArrowCount > 0;
                    floating |= stage.Entities.GetComponentsInChildren<UnityEngine.UI.Text>().Length > 0;
                    corpse |= network.Client.Replica.Current.Events.Any(e => e.Type == "effect" && e.Cue.Kind == "corpse") &&
                        stage.Entities.GetComponentsInChildren<EntityView>().Length > entities.Count;
                    await Task.Delay(50);
                }
                Check("real_battle_creates_projectile_views", arrows);
                Check("real_battle_creates_floating_text", floating);
                Check("real_death_creates_native_remnant", corpse);
                stage.Focus(780);
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("../artifacts/migration/native-effects-battle.png"));
                network.Disconnect();
                await Until(() => !network.Hosting && entities.Count == 0 && effects.EffectCount == 0 && effects.ArrowCount == 0, 10);
                Check("disconnect_clears_transient_views", stage.Entities.GetComponentsInChildren<NativeEffect>().Length == 0);
            }
            catch (Exception exception) { error = exception.ToString(); }
            finally
            {
                string path = Path.GetFullPath("../artifacts/migration/effects-runtime.json");
                File.WriteAllText(path, new JObject { ["passed"] = error == null, ["checks"] = JObject.FromObject(checks), ["error"] = error }.ToString());
                running = false;
                Debug.Log("DARK_NIGHTS_EFFECTS_RUNTIME passed=" + (error == null) + " checks=" + checks.Count + " report=" + path);
            }
        }
        private static async Task Until(Func<bool> condition, double seconds)
        {
            double end = Time.unscaledTimeAsDouble + seconds;
            while (!condition())
            {
                if (Time.unscaledTimeAsDouble > end) throw new TimeoutException("Effects runtime condition was not met.");
                await Task.Delay(50);
            }
        }
    }
}
