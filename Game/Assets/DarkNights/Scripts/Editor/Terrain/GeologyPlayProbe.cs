using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AnyRules.Next;
using DarkNights.Entry.Terrain;
using DarkNights.View;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

namespace DarkNights.Editor.Terrain
{
    /// <summary>显式启动的本地矿洞Play定向探针；经真实鼠标和正式矿镐触发塌方，保存实际GameView序列及冻结读数。临时设备和输入设置在finally恢复，不读写玩家存档。</summary>
    public static class GeologyPlayProbe
    {
        private static bool running;
        private static string folder;
        private static GeologyLabEntry lab;
        private static Mouse mouse;
        private static Keyboard keyboard;
        private static readonly JObject checks = new JObject();

        public static async void Run(int eventIndex = 0)
        {
            if (!Application.isPlaying || running) throw new InvalidOperationException("只在本次GameplayLab的Play中运行一次。");
            running = true; checks.RemoveAll();
            folder = Path.GetFullPath("../artifacts/cl02-mixed-unity-20261015/play-" + DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(folder);
            var originals = InputSystem.devices.Where(d => (d is Mouse || d is Keyboard) && d.enabled).ToArray();
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            string failure = null;
            IDisposable captureSize = null;
            try
            {
                var game = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
                var sizeType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DarkNights.Tests.TerrainGameViewTestSize"))
                    .FirstOrDefault(t => t != null) ?? throw new InvalidOperationException("缺少既有GameView定向验收尺寸工具。");
                captureSize = (IDisposable)Activator.CreateInstance(sizeType, game, 1280, 720);
                game.Focus();
                lab = UnityEngine.Object.FindAnyObjectByType<GeologyLabEntry>();
                if (lab == null) throw new InvalidOperationException("当前不是GameplayLab。");
                await Until(() => lab.Lab != null && lab.TerrainView.Ready, 30);
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                mouse = InputSystem.AddDevice<Mouse>("Geology Play verification");
                keyboard = InputSystem.AddDevice<Keyboard>("Geology Play verification keys");
                InputUser.PerformPairingWithDevice(mouse, lab.InputPlayer.user);
                InputUser.PerformPairingWithDevice(keyboard, lab.InputPlayer.user);
                foreach (var original in originals) InputSystem.DisableDevice(original);
                await Task.Delay(200);
                Capture("initial.png");
                var geology = lab.Lab.Events[eventIndex]; var config = geology.Configuration;
                var eventView = lab.EventSlots[eventIndex].View;
                Check("native_cave_ready", lab.TerrainView.Ready && lab.TerrainView.RockBuildCount > 0);
                Check("two_real_cave_fragments", lab.Blocks.Sum(b => b.Width * b.Height) > 500);
                Check("nine_source_cells", config.RockCount == 9);
                if (eventIndex == 0) await Pointer(config.Support, true);
                else
                {
                    var binding = lab.InputPlayer.actions.FindAction("Player/Move").bindings.First(b => b.isPartOfComposite &&
                        string.Equals(b.name, "positive", StringComparison.OrdinalIgnoreCase));
                    string keyName = binding.effectivePath.Substring(binding.effectivePath.LastIndexOf('/') + 1);
                    var key = keyboard.allKeys.Single(k => string.Equals(k.name, keyName, StringComparison.OrdinalIgnoreCase)).keyCode;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                }
                await Until(() => geology.Capture().Phase == 1, 10);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                await Pointer(eventIndex == 0 ? config.Support : config.Landing(0), false);
                Check(eventIndex == 0 ? "real_mouse_pickaxe_trigger" : "real_keyboard_region_trigger",
                    eventIndex != 0 || lab.Lab.World.Terrain.Map.Read(config.Support).Cell.IsEmpty);
                Capture("warning.png");
                await Task.Delay(300);
                lab.Lab.SetPaused(true);
                var before = geology.Capture(); double clock = lab.Lab.SimulationTime;
                int dust = eventView.Debris.Dust.particleCount;
                await Task.Delay(250);
                Check("pause_freezes_event_and_particles", geology.Capture().Clock == before.Clock &&
                    lab.Lab.SimulationTime == clock && eventView.Debris.Dust.particleCount == dust);
                lab.Lab.SetPaused(false);
                await Until(() => geology.Capture().Phase == 2, 5);
                Check("ceiling_detaches_before_landing", Enumerable.Range(0, config.RockCount)
                    .All(i => lab.Lab.World.Terrain.Map.Read(config.Source(i)).Cell.IsEmpty));
                int frame = 0, peakDust = 0, peakChips = 0, fragments = 0;
                double next = lab.Lab.SimulationTime;
                var motion = new JArray();
                while (geology.Capture().Phase == 2 && frame < 80)
                {
                    var view = eventView;
                    peakDust = Math.Max(peakDust, view.Debris.Dust.particleCount);
                    peakChips = Math.Max(peakChips, view.Debris.Chips.particleCount);
                    fragments = Math.Max(fragments, view.VisibleFragments);
                    if (lab.Lab.SimulationTime >= next)
                    {
                        Capture("fall-" + frame.ToString("D3") + ".png");
                        motion.Add(new JObject { ["time"] = geology.Capture().Clock,
                            ["x"] = view.Fragments[0].transform.position.x, ["y"] = view.Fragments[0].transform.position.y });
                        frame++; next += .04;
                    }
                    await Task.Delay(10);
                }
                await Until(() => geology.Capture().Phase == 4, 4);
                await Until(() => lab.TerrainView.Ready, 8);
                Capture("pile-impact.png"); await Task.Delay(180); Capture("pile-dust.png");
                var map = lab.Lab.World.Terrain.Map;
                Check("settled_pile_on_authoritative_map", Enumerable.Range(0, config.RubbleCount).All(i => !map.Read(config.Landing(i)).Cell.IsEmpty));
                Check("independent_fragments_visible", fragments == config.Collapse.Count);
                Check("dust_and_chips_emitted", peakDust > 0 && peakChips > 0 && eventView.Debris.ImpactCount == config.Collapse.Count);
                checks["peak_dust"] = peakDust; checks["peak_chips"] = peakChips; checks["captured_fall_frames"] = frame;
                File.WriteAllText(Path.Combine(folder, "motion.json"), motion.ToString());
                // 用实际鼠标逐个清理近处瓦砾；远端格子先保留，防止探针越权传送角色。
                var nearest = config.Landing(0);
                await Pointer(nearest, true); await Until(() => map.Read(nearest).Cell.IsEmpty, 10);
                await Pointer(nearest, false); Capture("pickaxe-cleared-first.png");
                Check("real_mouse_pickaxe_clears_rubble", map.Read(nearest).Cell.IsEmpty);
                await Task.Delay(1800); Capture("pile-settled.png");
                Check("particles_expire", eventView.Debris.Dust.particleCount == 0 && eventView.Debris.Chips.particleCount == 0);
            }
            catch (Exception error) { failure = error.ToString(); }
            finally
            {
                if (mouse != null) InputSystem.RemoveDevice(mouse); mouse = null;
                if (keyboard != null) InputSystem.RemoveDevice(keyboard); keyboard = null;
                foreach (var original in originals) if (original.added) InputSystem.EnableDevice(original);
                InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                if (lab?.Lab != null) lab.Lab.SetPaused(false);
                captureSize?.Dispose();
                checks["error"] = failure; checks["completed"] = true;
                checks["success"] = failure == null; checks["unity"] = Application.unityVersion;
                File.WriteAllText(Path.Combine(folder, "result.json"), checks.ToString()); running = false;
            }
        }

        private static void Check(string name, bool valid)
        { checks[name] = valid; if (!valid) throw new InvalidOperationException("Play检查失败：" + name); }

        private static async Task Pointer(CellCoord target, bool pressed)
        {
            Vector3 point = lab.ViewCamera.WorldToScreenPoint(new Vector3(target.U * .16f, (632 + target.V * 16) / 100f, 0));
            var state = new MouseState { position = new Vector2(point.x, point.y), buttons = (ushort)(pressed ? 1 : 0) };
            InputSystem.QueueStateEvent(mouse, state); await Task.Delay(70);
        }

        private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(folder, name));

        private static async Task Until(Func<bool> predicate, double seconds)
        {
            double deadline = EditorApplication.timeSinceStartup + seconds;
            while (!predicate())
            {
                if (!Application.isPlaying || EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("Play探针等待超时。状态：" + lab?.Status.text);
                await Task.Delay(20);
            }
        }
    }
}
