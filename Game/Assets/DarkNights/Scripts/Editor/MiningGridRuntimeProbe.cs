using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AnyRules.Next;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Entry;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using DarkNights.View;
using DarkNights.View.Terrain;
using GameCore.UI.UGUI;
using GameCore.Objects.Definition;
using Newtonsoft.Json.Linq;
using Runtime.AppStartup;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using UnityEngine.UI;

namespace DarkNights.Editor
{
    /// <summary>本地 Play 中经正式客户端购镐、航行、着陆和真实鼠标采矿的有界验证；临时输入设备及设置在 finally 恢复。</summary>
    public static class MiningGridRuntimeProbe
    {
        private static bool running;
        private static SessionNetwork network;
        private static HeroPlayerController hero;
        private static string folder;
        private static readonly JObject checks = new JObject();
        private static ActorViewData Actor => network.Client.Replica.Current.World.Actors.Single(actor => actor.ControllerSlot == network.Client.PlayerSlot);
        private static ExpeditionViewData Expedition => network.Client.Replica.Current.World.Expedition;
        private static float ShipX => network.Client.Replica.Current.World.Buildings.Single(building => building.Id == Expedition.Ship.Id).X;
        private static float ShipHeight => Expedition.Devices.Single(device => device.Id == Expedition.Ship.Id).Height;

        [DarkNightsWorkbenchCommand("Dark Nights/Verify/Mining Grid Runtime", "采集网格运行检查")]
        public static async void Run()
        {
            if (!Application.isPlaying || running) throw new InvalidOperationException("请在 Bootstrap 新鲜主菜单的 Play 中执行一次。");
            running = true; checks.RemoveAll();
            folder = Path.Combine(MiningGridValidation.EvidenceRoot, "play-" + DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(folder);
            var originals = InputSystem.devices.OfType<Mouse>().Where(device => device.enabled).ToArray();
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            Mouse mouse = null; string error = null; bool miningVerified = false;
            try
            {
                await Until(() => UnityEngine.Object.FindAnyObjectByType<SessionUiController>() != null);
                network = AppStartup.Instance.Context.Resolve<SessionNetwork>(); hero = network.GetComponent<HeroPlayerController>();
                if (network.Client.Replica.Current != null) throw new InvalidOperationException("只允许新鲜主菜单，不改动已有运行会话。");
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                mouse = InputSystem.AddDevice<Mouse>("Mining grid verification");
                var inputPlayer = UnityEngine.Object.FindAnyObjectByType<PinewatchStage>().InputPlayer;
                InputUser.PerformPairingWithDevice(mouse, inputPlayer.user);
                foreach (var original in originals) InputSystem.DisableDevice(original);
                await Until(() => UnityEngine.Object.FindObjectsByType<UGUIView>(FindObjectsInactive.Include)
                    .Any(view => view.name == "MainMenu(Clone)"));
                var start = Page("MainMenu").Get<Button>("NewGame");
                await Click(mouse, ButtonCenter(start));
                await Until(() => network.Client.Ready && network.GetComponent<SessionUiController>().Page == "");
                Check("ready_protocol_18", SessionAuthority.ProtocolVersion == 18);
                hero.enabled = false;
                await Walk(ShipX + 32);
                await network.Client.Send(SessionOperation.BuyEquipment, new[] { Actor.Id }, Expedition.Ship.Id,
                    kind: "pickaxe", value: Actor.InventoryRevision, controlLease: Actor.ControlLease);
                await Until(() => Actor.Slot0 == 2);
                Check("pickaxe_bought_via_client", true);
                await Walk(ShipX + 96);
                var journey = Expedition.Journey;
                await network.Client.Send(SessionOperation.SelectDestination, new[] { Actor.Id }, Expedition.Ship.Id,
                    kind: journey.Planets.First(planet => planet.Enabled).Id, value: journey.Revision, controlLease: Actor.ControlLease);
                await Until(() => network.Client.Ready && Expedition.Journey.Phase == JourneyPhase.Descent, 150);
                await HoldUntil(() => ShipHeight - Expedition.Ship.DockHeight < 48, 0, true, 60);
                await Until(() => Expedition.Journey.Phase == JourneyPhase.Landed, 90);
                await Walk(ShipX - 192);
                Check("walked_out_of_landed_ship", !Expedition.Crew.Single(crew => crew.Id == Actor.Id).Boarded);
                CellCoord? target = Target();
                bool mineable = target != null;
                if (target == null) target = RejectedTarget() ?? new CellCoord(TerrainMiningGeometry.CellU(Actor.X),
                    TerrainMiningGeometry.CellV(Actor.Height - 16));
                hero.enabled = true;
                await Task.Delay(500);
                var graphic = UnityEngine.Object.FindAnyObjectByType<TerrainMiningSelectorGraphic>();
                var stage = UnityEngine.Object.FindAnyObjectByType<PinewatchStage>();
                Vector2 point = stage.SceneCamera.WorldToScreenPoint(new Vector3(TerrainMiningGeometry.CenterX(target.Value.U) / 100,
                    TerrainMiningGeometry.CenterHeight(target.Value.V) / 100, 0));
                await Pointer(mouse, point, 0);
                Check("expedition_hero_root_active", Page("Hero").gameObject.activeInHierarchy);
                Check("legacy_toolbar_stays_hidden", !Page("Hero").transform.Find("Toolbar").gameObject.activeInHierarchy);
                Check("selector_has_nonzero_viewport", graphic.rectTransform.rect.width > 0 && graphic.rectTransform.rect.height > 0);
                Check("pickaxe_hover_renders_grid", Vertices(graphic) > 0);
                Check("center_brightness_matches_target_validity", Bright(graphic) == mineable);
                string hint = hero.MiningHint;
                Check("hover_explains_mining_validity", hint.Length != 0 &&
                    (mineable ? hint.StartsWith("左键采集") : !hint.StartsWith("左键采集")));
                var trade = network.GetComponent<ShipTradeHud>();
                var document = trade.GetComponent<UnityEngine.UIElements.UIDocument>();
                Check("equipment_hud_shows_mining_hint", UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(
                    document.rootVisualElement, "hint").text == hint);
                Check("selector_does_not_block_mouse", !graphic.raycastTarget);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "pickaxe-grid.png"));
                await Task.Delay(300);
                if (mineable)
                {
                    var before = network.Terrain.Replica.Read(target.Value).Cell;
                    await Click(mouse, point);
                    await Until(() => network.Terrain.Replica.Read(target.Value).Cell.IsEmpty);
                    Check("real_mouse_mines_selected_cell", !before.IsEmpty); miningVerified = true;
                    await Pointer(mouse, point, 0); Check("empty_target_is_dimmed", !Bright(graphic));
                }
                else
                {
                    var before = network.Terrain.Replica.Read(target.Value).Cell;
                    await Click(mouse, point);
                    var after = network.Terrain.Replica.Read(target.Value).Cell;
                    Check("real_mouse_rejected_target_stays_unchanged", before.TileId == after.TileId && before.Flags == after.Flags);
                    Check("rejected_click_keeps_explanation", hero.MiningHint == hint);
                }
                await network.Client.Send(SessionOperation.SetPaused, value: 1);
                await Until(() => network.Client.Replica.Current.Paused);
                await Task.Delay(200); Check("pause_hides_grid", Vertices(graphic) == 0);
                await network.Client.Send(SessionOperation.SetPaused, value: 0);
                await Until(() => !network.Client.Replica.Current.Paused);
                await hero.HandleAction("HeroItem1"); await Until(() => Actor.SelectedItem == 1);
                await Task.Delay(200); Check("switch_to_empty_slot_hides_grid", Vertices(graphic) == 0);
                await hero.HandleAction("HeroItem0"); await Until(() => Actor.SelectedItem == 0);
                await Pointer(mouse, point, 0); Check("reequip_restores_grid", Vertices(graphic) > 0);
                var menu = Page("Chrome").Get<Button>("Menu");
                await Click(mouse, ButtonCenter(menu));
                await Until(() => network.GetComponent<SessionUiController>().Page == "PauseMenu");
                Check("menu_hides_grid_root", !graphic.gameObject.activeInHierarchy);
                var resume = Page("PauseMenu").Get<Button>("Resume");
                await Click(mouse, ButtonCenter(resume));
                await Until(() => network.GetComponent<SessionUiController>().Page == "");
                await Pointer(mouse, point, 0); Check("menu_close_restores_grid", Vertices(graphic) > 0);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "after-mining.png"));
            }
            catch (Exception failure) { error = failure.ToString(); }
            finally
            {
                if (hero != null) hero.enabled = true;
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                foreach (var original in originals) if (original.added) InputSystem.EnableDevice(original);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                File.WriteAllText(Path.Combine(folder, "result.json"), new JObject { ["passed"] = error == null,
                    ["checks"] = checks, ["miningVerified"] = miningVerified,
                    ["miningLimit"] = miningVerified ? "" : "本次落地区未找到可达合法采矿格；没有伪造地图或坐标，实际采矿仍待验证。", ["error"] = error }.ToString());
                running = false;
                Debug.Log("MINING_GRID_PLAY passed=" + (error == null) + " checks=" + checks.Count + " report=" + folder);
            }
        }

        private static CellCoord? Target()
        {
            var map = network.Terrain.Replica;
            float hand = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch")
                .SharedConfigs.OfType<HandheldConfig>().Single().HandHeight;
            float reach = network.ObjectWorld.Catalog.Balance.HeroControl.WorkReach;
            int centerU = TerrainMiningGeometry.CellU(Actor.X), centerV = TerrainMiningGeometry.CellV(Actor.Height + hand);
            for (int distance = 0; distance <= 8; distance++)
                for (int deltaU = -distance; deltaU <= distance; deltaU++)
                    for (int deltaV = -distance; deltaV <= distance; deltaV++)
                    {
                        var cell = new CellCoord(centerU + deltaU, centerV + deltaV);
                        if (TerrainMiningQuery.CanMine(map, network.Terrain.Tiles, cell) &&
                            TerrainMiningQuery.Reachable(map, Actor.X, Actor.Height + hand, cell, reach)) return cell;
                    }
            return null;
        }
        private static CellCoord? RejectedTarget()
        {
            var map = network.Terrain.Replica;
            int centerU = TerrainMiningGeometry.CellU(Actor.X), centerV = TerrainMiningGeometry.CellV(Actor.Height);
            for (int distance = 0; distance <= 8; distance++)
                for (int deltaU = -distance; deltaU <= distance; deltaU++)
                    for (int deltaV = -distance; deltaV <= distance; deltaV++)
                    {
                        var cell = new CellCoord(centerU + deltaU, centerV + deltaV);
                        if (TerrainMiningQuery.BlockReason(map, network.Terrain.Tiles, cell).StartsWith("硬岩")) return cell;
                    }
            return null;
        }
        private static async Task Walk(float destination) => await HoldUntil(() => Math.Abs(Actor.X - destination) < 6,
            destination < Actor.X ? -1 : 1, false, 25);
        private static async Task HoldUntil(Func<bool> complete, int horizontal, bool down, int seconds)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (!complete())
            {
                if (Time.realtimeSinceStartupAsDouble > end) throw new TimeoutException("权威输入移动超时。");
                await network.Client.SendInput(Actor.Id, Actor.ControlLease, horizontal, false, false,
                    dropPressed: down, selectionRevision: Actor.SelectionRevision);
                await Task.Delay(60);
            }
            await network.Client.SendInput(Actor.Id, Actor.ControlLease, 0, false, false, selectionRevision: Actor.SelectionRevision);
            await Task.Delay(200);
        }
        private static void Check(string name, bool passed)
        {
            checks[name] = passed;
            File.WriteAllText(Path.Combine(folder, "progress.json"), new JObject { ["lastCheck"] = name, ["checks"] = checks }.ToString());
            if (!passed) throw new InvalidOperationException(name);
        }
        private static UGUIView Page(string name) => UnityEngine.Object.FindObjectsByType<UGUIView>(FindObjectsInactive.Include)
            .Single(view => view.name == name + "(Clone)");
        private static Vector2 ButtonCenter(Button button)
        {
            var rect = (RectTransform)button.transform;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }
        private static async Task Pointer(Mouse mouse, Vector2 position, ushort buttons)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = buttons });
            await Task.Delay(180);
        }
        private static async Task Click(Mouse mouse, Vector2 position)
        {
            await Pointer(mouse, position, 0); await Pointer(mouse, position, 1); await Pointer(mouse, position, 0);
        }
        private static async Task Until(Func<bool> complete, int seconds = 20)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (!complete())
            {
                if (Time.realtimeSinceStartupAsDouble > end) throw new TimeoutException("等待真实运行状态超时。");
                await Task.Delay(100);
            }
        }
        private static int Vertices(TerrainMiningSelectorGraphic graphic)
        {
            Canvas.ForceUpdateCanvases(); return graphic.canvasRenderer.GetMesh()?.vertexCount ?? 0;
        }
        private static bool Bright(TerrainMiningSelectorGraphic graphic)
        {
            Canvas.ForceUpdateCanvases(); return graphic.canvasRenderer.GetMesh()?.colors.Any(color => color.a > .9f) == true;
        }
    }
}
