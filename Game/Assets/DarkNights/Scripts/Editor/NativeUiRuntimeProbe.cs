using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DarkNights.Entry;
using DarkNights.Runtime.Network;
using DarkNights.View;
using GameCore.Interactions;
using GameCore.Objects.Runner;
using GameCore.UI.UGUI;
using Newtonsoft.Json.Linq;
using Runtime.AppStartup;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace DarkNights.Editor
{
    /// <summary>
    /// Play 内有限的真实 Input System／UGUI 操作验收：开局、选择、工作、建造、地图和菜单互斥。
    /// 只在显式 Editor 菜单启动，临时隔离 Unity 鼠标输入并在 finally 恢复；不进入 Player 或读写玩家存档。
    /// </summary>
    public static class NativeUiRuntimeProbe
    {
        private static bool running;

        public static async void Run()
        {
            if (!Application.isPlaying || running) throw new InvalidOperationException("Run once in Play after startup.");
            running = true;
            Mouse[] originals = InputSystem.devices.OfType<Mouse>().Where(m => m.enabled).ToArray();
            Mouse mouse = null;
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            var checks = new Dictionary<string, bool>();
            string error = null;
            void Check(string name, bool ok) { checks[name] = ok; if (!ok) throw new InvalidOperationException(name); }
            try
            {
                if (Screen.width < 1280 || Screen.height < 800)
                    throw new InvalidOperationException("UI probe requires at least 1280 x 800.");
                await Until(() => UnityEngine.Object.FindAnyObjectByType<SessionPlacementView>() != null);
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                foreach (Mouse original in originals) InputSystem.DisableDevice(original);
                mouse = InputSystem.AddDevice<Mouse>("DarkNights UI Probe");
                var network = AppStartup.Instance.Context.Resolve<SessionNetwork>();
                var ui = network.GetComponent<SessionUiController>();
                var entities = network.GetComponent<SessionEntityViews>();
                var placement = network.GetComponent<SessionPlacementView>();
                var stage = UnityEngine.Object.FindAnyObjectByType<PinewatchStage>();
                EntityView[] sceneVisuals = UnityEngine.Object.FindObjectsByType<EntityView>();
                if (network.Client.Replica.Current != null) throw new InvalidOperationException("Probe needs fresh MainMenu.");
                await Button(mouse, "MainMenu", "Slot");
                Check("main_menu_slot_changes_once", Page("MainMenu").Get<Text>("SlotLabel").text.StartsWith("存档槽位 2 / 10"));
                var address = Page("MainMenu").Get<InputField>("Address");
                await Click(mouse, RectTransformUtility.WorldToScreenPoint(null, address.transform.position), 1);
                Check("address_is_reachable_by_mouse", address.isFocused);
                await Button(mouse, "MainMenu", "NewGame");
                await Until(() => network.Client.Ready && ui.Page == "" && entities.Count == 17);
                Check("mouse_main_menu_starts_ready_host", network.Client.PlayerSlot == 0);
                Check("default_hero_toolbar_stays_hidden", !Page("Hero").transform.Find("Toolbar").gameObject.activeInHierarchy);
                await network.GetComponent<HeroPlayerController>().SetHeroMode(false);
                await Until(() => network.Client.Replica.Current.World.Actors.All(actor => actor.ControllerSlot != 0));
                await Button(mouse, "Chrome", "Pause");
                await Until(() => network.Client.Replica.Current.Paused);
                var actor = network.Client.Replica.Current.World.Actors.First(a => a.Kind == "worker");
                await Click(mouse, World(stage, actor.X), 1);
                Check("mouse_selects_explicit_actor", ui.Input.Selected.SequenceEqual(new[] { actor.Id }));
                var site = network.Client.Replica.Current.World.Worksites.First(w => w.Kind == "stone");
                await Click(mouse, World(stage, site.X), 2);
                await Until(() => network.Client.Replica.Current.World.Actors.Single(a => a.Id == actor.Id).TargetId == site.Id);
                Check("right_click_assigns_work_while_paused", network.Client.Replica.Current.Paused);
                await Button(mouse, "Chrome", "BuildHouse");
                await Pointer(mouse, World(stage, 184), 0);
                await Until(() => placement.Valid && UnityEngine.Object.FindObjectsByType<EntityView>().Length == entities.Count + 1);
                Check("definition_preview_valid_without_payment", network.Client.Replica.Current.World.Camp.Stock.Wood == 100);
                await Click(mouse, World(stage, 184), 1);
                await Until(() => network.Client.Replica.Current.World.Buildings.Count == 5);
                Check("mouse_build_pays_once", network.Client.Replica.Current.World.Camp.Stock.Wood == 75);
                await Click(mouse, World(stage, 184), 2);
                await Until(() => ui.Input.BuildKind == "" && UnityEngine.Object.FindObjectsByType<EntityView>().Length == entities.Count);
                Check("cancel_releases_placement_session", YYInteractionSessionService.Instance.ActiveSessions.Count == 0);
                var map = Page("Chrome").Get<CampMap>("Map");
                float before = stage.CameraX;
                await Click(mouse, RectTransformUtility.WorldToScreenPoint(null, map.rectTransform.TransformPoint(
                    new Vector2(map.rectTransform.rect.xMax - 10, map.rectTransform.rect.center.y))), 1);
                Check("minimap_mouse_focuses_local_camera", stage.CameraX > before + 100);
                await Button(mouse, "Chrome", "Menu");
                Check("menu_owns_one_modal_session", ui.Page == "PauseMenu" && YYInteractionSessionService.Instance.ActiveSessions.Count == 1);
                await Button(mouse, "PauseMenu", "Slot");
                Check("pause_slot_updates_both_panels", Page("PauseMenu").Get<Text>("SlotLabel").text.StartsWith("存档槽位 3 / 10") &&
                    Page("PauseMenu").Get<Text>("SlotLabel").text == Page("MainMenu").Get<Text>("SlotLabel").text);
                await Button(mouse, "PauseMenu", "ControlMode");
                await Until(() => network.Client.Replica.Current.HostOnly);
                Check("mouse_control_mode_reaches_authority_once", network.Client.Replica.Current.PolicyRevision == 1);
                await Button(mouse, "PauseMenu", "ControlMode");
                await Until(() => !network.Client.Replica.Current.HostOnly);
                Check("mouse_control_mode_restores_shared_camp", network.Client.Replica.Current.PolicyRevision == 2);
                int[] selection = ui.Input.Selected.ToArray();
                await Click(mouse, World(stage, actor.X), 1);
                Check("modal_blocks_world_selection", ui.Input.Selected.SequenceEqual(selection));
                await Button(mouse, "PauseMenu", "Resume");
                Check("resume_releases_modal_without_unpausing_world", ui.Page == "" && network.Client.Replica.Current.Paused && YYInteractionSessionService.Instance.ActiveSessions.Count == 0);
                await Button(mouse, "Chrome", "Menu");
                Check("reopened_pause_preserves_slot_and_resets_pressed_text",
                    Page("PauseMenu").Get<Text>("SlotLabel").text == Page("MainMenu").Get<Text>("SlotLabel").text &&
                    ((Color32)Page("PauseMenu").Get<Text>("ControlModeLabel").color).Equals(new Color32(228, 229, 215, 255)));
                await Button(mouse, "PauseMenu", "Resume");
                stage.Focus(stage.InitialCameraX);
                await Task.Delay(200);
                string imagePath = Path.GetFullPath("../artifacts/migration/native-ui-host-1280.png");
                SessionRenderCapture.Save(stage, imagePath);
                Check("hud_shapes_have_meshes", map.canvasRenderer.GetMesh().vertexCount > 100);
                await Button(mouse, "Chrome", "Menu");
                await Button(mouse, "PauseMenu", "MainMenu");
                await Until(() => ui.Page == "MainMenu" && entities.Count == 0 && !network.Hosting);
                Check("exit_preserves_scene_views_without_live_bindings", sceneVisuals.All(visual => visual != null) &&
                    UnityEngine.Object.FindObjectsByType<ObjectInstance>().SelectMany(instance => instance.GetAllBehaviors())
                        .OfType<EntityPresentationBehaviour>().All(presentation => !presentation.IsBound));
                await Button(mouse, "MainMenu", "NewGame");
                await Until(() => network.Client.Ready && entities.Count == 17);
                Check("second_start_restores_fresh_world", network.Client.Replica.Current.World.Buildings.Count == 4);
            }
            catch (Exception exception) { error = exception.ToString(); }
            finally
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = background;
                foreach (Mouse original in originals) if (original.added) InputSystem.EnableDevice(original);
                string path = Path.GetFullPath("../artifacts/migration/ui-runtime.json");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, new JObject { ["passed"] = error == null, ["checks"] = JObject.FromObject(checks), ["error"] = error }.ToString());
                running = false;
                Debug.Log("DARK_NIGHTS_UI_RUNTIME passed=" + (error == null) + " checks=" + checks.Count + " report=" + path);
            }
        }

        private static UGUIView Page(string name) => UnityEngine.Object.FindObjectsByType<UGUIView>(FindObjectsInactive.Include)
            .Single(view => view.name == name + "(Clone)");
        private static Vector2 World(PinewatchStage stage, float x) => stage.SceneCamera.WorldToScreenPoint(new Vector3(x / 100, .06f));
        private static Task Button(Mouse mouse, string page, string key)
        {
            var button = Page(page).Get<Button>(key);
            if (!button.interactable || !button.gameObject.activeInHierarchy) throw new InvalidOperationException("Button unavailable: " + page + "/" + key);
            var rect = (RectTransform)button.transform;
            return Click(mouse, RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)), 1);
        }
        private static async Task Click(Mouse mouse, Vector2 point, ushort buttons)
        {
            await Pointer(mouse, point, 0);
            await Pointer(mouse, point, buttons);
            await Pointer(mouse, point, 0);
        }
        private static async Task Pointer(Mouse mouse, Vector2 point, ushort buttons)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = buttons });
            await Task.Delay(120);
        }
        private static async Task Until(Func<bool> condition)
        {
            double end = Time.realtimeSinceStartupAsDouble + 12;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > end) throw new TimeoutException("Runtime UI condition was not met.");
                await Task.Delay(100);
            }
        }
    }
}
