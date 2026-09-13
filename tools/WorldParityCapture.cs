using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.Entry;
using DarkNights.Runtime.Network;
using DarkNights.View;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 通过 Unity CLI run_script 在后台 Play 中捕获与冻结 Godot 参考相同的六种局面。
    /// 只在独立验收 Editor 使用；临时世界和选择归本次会话，结束断开并恢复 GameView 尺寸。
    /// 反射只控制显式验收所需的现有内部入口，不写正式场景、Prefab、规则或玩家存档。
    /// </summary>
    public static class WorldParityCapture
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string ViewportKey = "DarkNights.WorldParity.Viewport";
        private const string CustomKey = "DarkNights.WorldParity.Custom";

        public static object Main(string outputDirectory)
        {
            if (!Application.isPlaying || !Application.isBatchMode)
                throw new InvalidOperationException("This probe requires a background Play Editor.");
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidOperationException("World presentation must remain in Linear space.");
            string directory = Path.GetFullPath(outputDirectory);
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                throw new InvalidOperationException("Capture output must be empty.");
            Directory.CreateDirectory(directory);
            Run(directory).Forget();
            return new { started = true, directory };
        }

        private static async UniTaskVoid Run(string directory)
        {
            SessionNetwork network = null;
            var result = new JObject { ["passed"] = false, ["scope"] = "Frozen Editor image comparison; no Player or foreground performance claim" };
            result["colorSpace"] = "Linear";
            result["referenceColorSpace"] = "Godot OpenGL Compatibility / Gamma";
            result["identicalColorRequired"] = false;
            var captures = new JArray();
            result["captures"] = captures;
            string phase = "startup";
            try
            {
                await Until(() =>
                {
                    network = UnityEngine.Object.FindAnyObjectByType<SessionNetwork>();
                    var controller = network?.GetComponent<SessionUiController>();
                    return network != null && network.ObjectResources != null &&
                        controller != null && Field<CampHudBehaviour>(controller, "hud") != null;
                }, phase);
                if (network.Hosting || network.Client.Ready) throw new InvalidOperationException("Existing session must be preserved.");
                typeof(SessionNetwork).GetProperty(nameof(SessionNetwork.SaveDirectory)).SetValue(network,
                    Path.Combine(directory, "saves", "v2"));
                var stage = UnityEngine.Object.FindAnyObjectByType<PinewatchStage>();
                var ui = network.GetComponent<SessionUiController>();
                var entities = network.GetComponent<SessionEntityViews>();
                var effects = network.GetComponent<SessionEffects>();
                foreach (string name in new[] { "menu", "camp", "night", "remnants", "zoom_out", "wide" })
                {
                    phase = name;
                    int width = name == "wide" ? 1600 : 1280;
                    int height = name == "wide" ? 900 : 800;
                    SetViewport(width, height, false);
                    await Until(() => Screen.width == width && Screen.height == height, "Viewport " + name);
                    bool battle = name == "night" || name == "remnants";
                    if (name != "menu")
                    {
                        await network.Connect(true, "127.0.0.1", 28421);
                        await Until(() => network.Client.Ready, "Ready " + name);
                        long previous = network.Client.Replica.Current.Publication;
                        var world = network.ObjectWorld;
                        world.Restart();
                        world.SetTime(true, 1);
                        if (battle)
                        {
                            world.StartNight();
                            world.Mutations.Run(() =>
                            {
                                Invoke(world.Lifecycle, "SpawnActor", "zombie", 794f, true, "");
                                Invoke(world.Lifecycle, "SpawnActor", "ghoul", 838f, true, "");
                                Invoke(world.Lifecycle, "SpawnActor", "armored", 872f, true, "");
                                return true;
                            });
                        }
                        if (name == "remnants") EmitRemnants(network);
                        await Until(() => network.Client.Replica.Current.Publication > previous &&
                            network.Client.Replica.Current.Paused && network.Client.Replica.Current.Elapsed == 0 &&
                            network.Client.Replica.Current.World.Actors.Count == (battle ? 10 : 7) &&
                            entities.Count == (battle ? 20 : 17), "Frozen world " + name);
                        if (name == "remnants") await Until(() => effects.EffectCount == 6, "Six effects");
                    }
                    await UniTask.Yield();
                    SessionViewData frame = network.Client.Replica.Current;
                    ui.Input.ResetLocal();
                    if (battle) Field<List<int>>(ui.Input, "selected").Add(frame.World.Actors.Single(a => a.Kind == "archer").Id);
                    float zoom = name == "zoom_out" || name == "wide" ? 1.8f : 2.8f;
                    float x = battle ? 730 : zoom == 1.8f ? 550 : 255;
                    stage.ChangeZoom(zoom / stage.Zoom);
                    stage.Focus(x);
                    stage.SamplePresentation(2, battle ? 1 : 0.16f);
                    if (frame != null) Invoke(entities, "Present", frame);
                    SampleEffects(effects, stage, 0, Field<DarkNights.Core.Config.LevelLayout>(network, "layout").GroundY);
                    Field<CampHudBehaviour>(ui, "hud").ResetMessages();
                    Invoke(ui, "Update");
                    Canvas.ForceUpdateCanvases();
                    string image = Path.Combine(directory, name + ".png");
                    SessionRenderCapture.Save(stage, image);
                    var capture = new JObject
                    {
                        ["name"] = name, ["width"] = Screen.width, ["height"] = Screen.height,
                        ["cameraX"] = stage.CameraX, ["zoom"] = stage.Zoom,
                        ["visualTime"] = Field<double>(stage, "visualTime"), ["night"] = Field<float>(stage, "night"),
                        ["selected"] = JArray.FromObject(ui.Input.Selected), ["uiPage"] = ui.Page,
                        ["effects"] = effects.EffectCount, ["entities"] = entities.Count,
                        ["image"] = image, ["elapsed"] = frame?.Elapsed, ["paused"] = frame?.Paused
                    };
                    if (frame != null)
                    {
                        string snapshot = Path.Combine(directory, name + ".v2.json");
                        File.WriteAllText(snapshot, network.ObjectWorld.SaveCodec.Serialize(network.ObjectWorld.CaptureWorld()));
                        capture["snapshot"] = snapshot;
                        capture["frame"] = JObject.FromObject(frame);
                    }
                    captures.Add(capture);
                    network.Disconnect();
                    await Until(() => !network.Hosting && !network.Client.Ready && entities.Count == 0, "Exit " + name);
                    await UniTask.Yield();
                }
                result["passed"] = true;
            }
            catch (Exception error)
            {
                result["phase"] = phase;
                result["exception"] = error.ToString();
            }
            finally
            {
                network?.Disconnect();
                try { SetViewport(0, 0, true); result["viewportRestored"] = true; }
                catch (Exception error) { result["viewportRestored"] = false; result["restoreError"] = error.ToString(); }
                File.WriteAllText(Path.Combine(directory, "result.json"), result.ToString());
            }
        }

        private static void EmitRemnants(SessionNetwork network)
        {
            float ground = network.ObjectWorld.Layout.GroundY;
            var feedback = network.ObjectWorld.Feedback;
            feedback.Emit(new VisualCue("corpse", 744, ground, ContentId: "archer", Face: -1));
            feedback.Emit(new VisualCue("corpse", 720, ground, ContentId: "ghoul"));
            feedback.Emit(new VisualCue("rubble", 590, ground, ContentId: "house"));
            feedback.Emit(new VisualCue("resource", 640, ground - 23, "+3", "wood"));
            feedback.Emit(new VisualCue("damage", 680, ground - 23, "5", Enemy: true));
            feedback.Emit(new VisualCue("command", 760, ground));
        }

        private static void SampleEffects(SessionEffects source, PinewatchStage stage, double age, float ground)
        {
            foreach (object item in Field<IEnumerable>(source, "effects"))
            {
                var type = item.GetType();
                var effect = (NativeEffect)type.GetField("Item2").GetValue(item);
                var remnant = (NativeVisual)type.GetField("Item3").GetValue(item);
                var value = (PresentationEvent)type.GetField("Item4").GetValue(item);
                if (remnant != null) { remnant.Ambient = stage.Ambient; remnant.PresentRemnant(value.Cue, age); }
                else effect.Present(value.Cue, age, ground, stage.IlluminationAt(effect.transform.position));
            }
        }

        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, All).GetValue(owner);
        private static object Invoke(object owner, string name, params object[] args) =>
            owner.GetType().GetMethod(name, All).Invoke(owner, args);

        private static async UniTask Until(Func<bool> condition, string phase)
        {
            double until = Time.realtimeSinceStartupAsDouble + 30;
            while (!condition() && Time.realtimeSinceStartupAsDouble < until) await UniTask.Yield();
            if (!condition()) throw new TimeoutException(phase);
        }

        private static void SetViewport(int width, int height, bool restore)
        {
            Assembly editor = typeof(EditorWindow).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            object sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            object group = sizesType.GetMethod("GetGroup", All).Invoke(sizes,
                new[] { Enum.Parse(editor.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
            Type viewType = editor.GetType("UnityEditor.GameView");
            var view = (EditorWindow)Resources.FindObjectsOfTypeAll(viewType).FirstOrDefault();
            if (view == null) view = EditorWindow.GetWindow(viewType, false, "Game", false);
            PropertyInfo index = viewType.GetProperty("selectedSizeIndex", All);
            int previous = SessionState.GetInt(ViewportKey, -1);
            int custom = SessionState.GetInt(CustomKey, -1);
            if (previous < 0 && !restore)
            {
                previous = (int)index.GetValue(view);
                SessionState.SetInt(ViewportKey, previous);
            }
            if (custom >= 0)
            {
                index.SetValue(view, previous);
                group.GetType().GetMethod("RemoveCustomSize", All).Invoke(group, new object[] { custom });
                SessionState.EraseInt(CustomKey);
            }
            if (restore)
            {
                if (previous >= 0) index.SetValue(view, previous);
                SessionState.EraseInt(ViewportKey);
            }
            else
            {
                object entry = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"), All, null,
                    new object[] { Enum.Parse(editor.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), width, height, "World parity verification" }, null);
                custom = (int)group.GetType().GetMethod("GetCustomCount", All).Invoke(group, null);
                int builtIn = (int)group.GetType().GetMethod("GetBuiltinCount", All).Invoke(group, null);
                group.GetType().GetMethod("AddCustomSize", All).Invoke(group, new[] { entry });
                SessionState.SetInt(CustomKey, custom);
                index.SetValue(view, builtIn + custom);
            }
            view.Repaint();
        }
    }
}
