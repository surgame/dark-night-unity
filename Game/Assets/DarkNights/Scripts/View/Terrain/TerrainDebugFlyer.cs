using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkNights.View.Terrain
{
    /// <summary>
    /// 离线地图工作台的本地飞行观察角色，不是正式营地实体，不持有经济、战斗或存档状态。
    /// 直接以二维位移穿过格子；镜头始终跟随，停用或输入文本时不处理键盘移动。
    /// </summary>
    public sealed class TerrainDebugFlyer : MonoBehaviour
    {
        public Camera ViewCamera;
        public SpriteRenderer Art;
        public Sprite IdleFrame;
        public Sprite[] WalkFrames = System.Array.Empty<Sprite>();
        public bool FlightInputEnabled { get; set; } = true;
        [Range(2, 100)] public float Speed = 18;
        [Range(5, 100)] public float CameraDistance = 12;
        public bool InputBlocked { get; set; }
        public bool PointerOverPanel { get; set; }
        public bool Ready { get; set; }
        private float animationTime;
        private bool moving;

        private void Update()
        {
            if (!FlightInputEnabled || !Ready || InputBlocked) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var axis = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                Move(axis, Time.unscaledDeltaTime, keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            }
            if (!PointerOverPanel && Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (scroll != 0) CameraDistance = Mathf.Clamp(CameraDistance * (scroll > 0 ? .9f : 1.1f), 5, 100);
            }
        }

        public void Move(Vector2 axis, float seconds, bool boost)
        {
            Vector3 position = transform.position + (Vector3)Vector2.ClampMagnitude(axis, 1) *
                (Mathf.Clamp(Speed, 2, 100) * (boost ? 3 : 1) * Mathf.Clamp(seconds, 0, .1f));
            Teleport(position);
            PresentMovement(axis.x, axis.sqrMagnitude > 0, seconds);
        }

        /// <summary>行走和观察共用的姿态入口；使用原始画布的底边中点，翻转和换帧不会移动脚底锚点。</summary>
        public void PresentMovement(float horizontal, bool isMoving, float seconds)
        {
            if (Art == null) return;
            if (horizontal != 0) Art.flipX = horizontal < 0;
            if (moving != isMoving) animationTime = 0;
            moving = isMoving;
            animationTime = (animationTime + Mathf.Max(0, seconds)) % 1f;
            if (moving && WalkFrames.Length > 0)
                Art.sprite = WalkFrames[Mathf.FloorToInt(animationTime * WalkFrames.Length)];
            else if (IdleFrame != null) Art.sprite = IdleFrame;
            var sprite = Art.sprite;
            if (sprite == null) return;
            Vector3 scale = Art.transform.localScale;
            float center = (sprite.rect.width * .5f - sprite.pivot.x) / sprite.pixelsPerUnit;
            Art.transform.localPosition = new Vector3(center * scale.x * (Art.flipX ? 1 : -1),
                sprite.pivot.y / sprite.pixelsPerUnit * scale.y, Art.transform.localPosition.z);
        }

        public void Teleport(Vector2 position)
        {
            transform.position = new Vector3(Mathf.Clamp(position.x, .5f, 319.5f), Mathf.Clamp(position.y, -190.5f, .5f), 0);
            Follow();
        }

        private void LateUpdate() => Follow();

        private void Follow()
        {
            if (ViewCamera == null) return;
            CameraDistance = Mathf.Clamp(CameraDistance, 5, 100);
            ViewCamera.orthographicSize = CameraDistance;
            ViewCamera.transform.position = transform.position + new Vector3(0, .75f, -10);
        }
    }
}
