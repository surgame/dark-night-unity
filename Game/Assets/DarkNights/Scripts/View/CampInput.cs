using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.ViewData;
using GameCore.Interactions;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DarkNights.View
{
    /// <summary>
    /// 每客户端独立的选择、框选、建造模式及镜头输入；复用 Interaction Sessions 协调跨帧输入占用。
    /// 输出显式实体参数的单次意图，暂停仍可选择和安排工作；菜单或未 Ready 时拦截游戏输入。
    /// </summary>
    public sealed class CampInput : MonoBehaviour
    {
        private readonly List<int> selected = new List<int>();
        private IYYInteractionSessionService sessions;
        private YYInteractionSessionHandle placement, drag;
        private IEntityVisuals visuals;
        private PinewatchStage stage;
        private GameInputActions controls;
        private SessionViewData frame;
        private bool ready, dragging, append;
        private Vector2 dragStart, dragWorld;
        public event Action<InputIntent> Intent;
        public IReadOnlyList<int> Selected => selected.AsReadOnly();
        public string BuildKind { get; private set; } = "";
        public float PlacementX { get; private set; }
        public bool PlacementValid { get; private set; }
        public void PresentPlacement(float x, bool valid) { PlacementX = x; PlacementValid = valid; }
        public int Hover { get; private set; }
        public bool Dragging => dragging;
        public Vector2 DragStart => dragStart;
        public Vector2 Pointer => controls?.Pointer ?? Vector2.zero;

        public void Initialize(PinewatchStage scene, IEntityVisuals entities, IYYInteractionSessionService service, GameInputActions actions)
        {
            stage = scene;
            controls = actions;
            visuals = entities;
            sessions = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void Present(SessionViewData value, bool canSend)
        {
            frame = value;
            ready = canSend;
            if (frame == null) { ResetLocal(); return; }
            selected.RemoveAll(id => !frame.World.Actors.Any(a => a.Id == id) && !frame.World.Buildings.Any(b => b.Id == id) &&
                !frame.World.Worksites.Any(w => w.Id == id));
        }

        public void BeginBuild(string kind)
        {
            CancelBuild();
            if (!ready || !sessions.TryBegin(new YYInteractionSessionDescriptor
            {
                Kind = "dark_nights.placement", Owner = "CampInput", Priority = 20,
                Blocks = YYInteractionBlockFlags.World | YYInteractionBlockFlags.LowerPriorityPreview
            }, out placement)) return;
            BuildKind = kind;
        }

        public void CancelBuild() { placement?.Dispose(); placement = null; BuildKind = ""; }
        public void ResetLocal()
        {
            CancelBuild();
            drag?.Dispose(); drag = null;
            dragging = false;
            selected.Clear();
            Hover = 0;
        }

        /// <summary>显式选择当前副本中的一个实体；只改变本地选择，不产生业务命令。</summary>
        public void SelectEntity(int id)
        {
            if (id != 0 && (!ready || frame == null || visuals.Visual(id) == null))
                throw new InvalidOperationException("Selection requires an available current entity.");
            ResetLocal();
            if (id != 0) selected.Add(id);
        }

        /// <summary>从当前本地选区发出一次右键指令；输入互斥与实际鼠标共用，反馈不表示服务端已执行。</summary>
        public void IssueOrders(float x, int target = 0)
        {
            if (!ready || frame == null || BuildKind.Length > 0 ||
                sessions.IsBlocked(YYInteractionBlockFlags.GameplayActions | YYInteractionBlockFlags.WorldConfirm)) return;
            Intent?.Invoke(new InputIntent("Orders", ActorIds(), target, x));
        }

        private void Update()
        {
            if (sessions == null || frame == null || !ready) return;
            GameInputActions.CampFrame keys = controls.ReadCamp();
            if (keys.Menu)
            {
                if (BuildKind.Length > 0) CancelBuild();
                else Emit("Menu");
                return;
            }
            if (controls.HeroMode) return;
            bool blocked = !keys.Allowed;
            if (blocked) { Hover = 0; EndDrag(); return; }
            CameraInput(keys);
            if (keys.Pause) Emit("Pause");
            if (keys.Help) Emit("Help");
            if (keys.Save) Emit("Save");
            if (keys.Load) Emit("Load");
            if (keys.Home) stage.Focus(stage.InitialCameraX);
            if (keys.Guards) SelectGroup(true);
            if (keys.Idle) SelectGroup(false);
            Vector2 pointer = keys.Pointer;
            bool ui = keys.PointerOverUi;
            Vector2 point = stage.SceneCamera.ScreenToWorldPoint(pointer);
            Hover = ui ? 0 : Pick(point);
            if (keys.Orders && !ui)
            {
                if (BuildKind.Length > 0) CancelBuild();
                else IssueOrders(point.x * 100, Hover);
            }
            if (keys.Select && !ui)
            {
                if (BuildKind.Length > 0 && sessions.IsTopOrUnblocked(placement.SessionId, YYInteractionBlockFlags.WorldConfirm))
                    Intent?.Invoke(new InputIntent("Build", ActorIds(), 0, point.x * 100, BuildKind));
                else if (sessions.TryBegin(new YYInteractionSessionDescriptor
                {
                    Kind = "dark_nights.selection", Owner = "CampInput", Priority = 10, Blocks = YYInteractionBlockFlags.World
                }, out drag))
                {
                    dragging = true; dragStart = pointer; dragWorld = point;
                    append = keys.AppendHeld;
                }
            }
            if (dragging && !keys.SelectHeld) FinishSelection(pointer, point);
        }

        private void CameraInput(GameInputActions.CampFrame keys)
        {
            if (!keys.CameraAllowed) return;
            stage.Move(keys.CameraMove * Time.unscaledDeltaTime * 240);
            float scroll = keys.Scroll;
            if (scroll != 0) stage.ChangeZoom(scroll > 0 ? 1.12f : 1 / 1.12f);
            if (keys.PanHeld) stage.Move(-keys.PanDelta.x / stage.Zoom);
        }

        private void FinishSelection(Vector2 screen, Vector2 point)
        {
            if (!append) selected.Clear();
            if (Vector2.Distance(dragStart, screen) > 5)
            {
                Rect bounds = Rect.MinMaxRect(Mathf.Min(dragWorld.x, point.x), Mathf.Min(dragWorld.y, point.y), Mathf.Max(dragWorld.x, point.x), Mathf.Max(dragWorld.y, point.y));
                foreach (ActorViewData actor in frame.World.Actors)
                {
                    EntityView view = visuals.Visual(actor.Id);
                    if (!actor.Enemy && view != null && bounds.Contains(view.SelectionAnchor.position) && !selected.Contains(actor.Id)) selected.Add(actor.Id);
                }
            }
            else
            {
                int id = Pick(point);
                if (id > 0 && selected.Contains(id) && append) selected.Remove(id);
                else if (id > 0 && !selected.Contains(id)) selected.Add(id);
            }
            EndDrag();
        }

        public int Pick(Vector2 point)
        {
            foreach (ActorViewData actor in frame.World.Actors.Reverse()) if (visuals.Visual(actor.Id)?.Contains(point) == true) return actor.Id;
            foreach (BuildingViewData building in frame.World.Buildings.Reverse()) if (visuals.Visual(building.Id)?.Contains(point) == true) return building.Id;
            foreach (WorksiteViewData site in frame.World.Worksites.Reverse())
                if (site.Amount != 0 && site.FarmId == 0 && visuals.Visual(site.Id)?.Contains(point) == true) return site.Id;
            return 0;
        }

        public int[] ActorIds() => frame == null ? Array.Empty<int>() : frame.World.Actors.Where(a => !a.Enemy && selected.Contains(a.Id)).Select(a => a.Id).ToArray();
        private void Emit(string action) { Intent?.Invoke(new InputIntent(action, ActorIds())); }
        private void SelectGroup(bool guards)
        {
            selected.Clear();
            selected.AddRange(frame.World.Actors.Where(a => !a.Enemy && (guards ? a.Kind != "worker" : a.Kind == "worker" && a.Activity == "Idle")).Select(a => a.Id));
        }
        private void EndDrag() { dragging = false; drag?.Dispose(); drag = null; }
        private void OnDestroy() { ResetLocal(); Intent = null; }
    }
}
