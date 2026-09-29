using UnityEngine;
using UnityEngine.InputSystem;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Entry.Terrain
{
    /// <summary>洞穴工作台输入；Tab 在无碰撞观察和共用权威运动之间切换，鼠标仅调用有距离限制的调试破坏，不代表正式装备结算。</summary>
    public sealed class CaveWorkshopInput : MonoBehaviour
    {
        public TerrainDebugBootstrap Bootstrap;
        public bool Walking = true;
        private float accumulator;
        private bool jump;
        private void OnEnable()
        {
            if (Bootstrap != null && Bootstrap.Flyer != null) Bootstrap.Flyer.FlightInputEnabled = !Walking;
        }
        private void Update()
        {
            if (Bootstrap == null || Bootstrap.Workshop == null || Bootstrap.Generating) return;
            var flyer = Bootstrap.Flyer; var keys = Keyboard.current; var mouse = Mouse.current;
            if (flyer.Art != null && flyer.IdleFrame != null)
                flyer.Art.transform.localScale = Vector3.one *
                    (flyer.IdleFrame.pixelsPerUnit * HeroControlDefinition.VisualScale / PlayableTerrain.CellPixels);
            flyer.FlightInputEnabled = !Walking;
            if (keys == null || flyer.InputBlocked || flyer.WorkbenchPointerActive) return;
            if (keys.tabKey.wasPressedThisFrame)
            {
                Walking = !Walking;
                accumulator = 0; jump = false;
                if (Walking) Bootstrap.Workshop.Teleport(flyer.transform.position.x, flyer.transform.position.y);
            }
            flyer.FlightInputEnabled = !Walking;
            if (!Walking) return;
            jump |= keys.spaceKey.wasPressedThisFrame;
            accumulator = Mathf.Min(accumulator + Time.unscaledDeltaTime, .1f);
            float horizontal = (keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0);
            bool sprint = keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed;
            float previousX = Bootstrap.Workshop.X;
            while (accumulator >= 1f / 60)
            { Bootstrap.Workshop.Tick(horizontal, jump, keys.spaceKey.isPressed, 1f / 60, sprint); jump = false; accumulator -= 1f / 60; }
            flyer.Teleport(new Vector2(Bootstrap.Workshop.X, Bootstrap.Workshop.Y));
            float distance = Mathf.Abs(Bootstrap.Workshop.X - previousX) * PlayableTerrain.CellPixels;
            flyer.PresentMovement(horizontal, horizontal != 0 && Bootstrap.Workshop.Grounded,
                Time.unscaledDeltaTime, distance / HeroControlDefinition.WalkCycleDistance);
            if (mouse != null && !flyer.PointerOverPanel)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0) flyer.CameraDistance = Mathf.Clamp(flyer.CameraDistance * (scroll > 0 ? .9f : 1.1f), 5, 100);
                if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                {
                    var p = flyer.ViewCamera.ScreenToWorldPoint(new Vector3(mouse.position.ReadValue().x, mouse.position.ReadValue().y, 10));
                    if (Bootstrap.Workshop.Edit(p.x, p.y, mouse.rightButton.wasPressedThisFrame)) Bootstrap.Preview.NotifyReplicaChanged();
                }
            }
        }
        private void OnDisable() { if (Bootstrap != null && Bootstrap.Flyer != null) Bootstrap.Flyer.FlightInputEnabled = true; }
    }
}
