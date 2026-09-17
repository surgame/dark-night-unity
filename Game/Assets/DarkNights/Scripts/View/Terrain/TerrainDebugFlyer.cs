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
        [Range(2, 100)] public float Speed = 18;
        [Range(5, 100)] public float CameraDistance = 12;
        public bool InputBlocked { get; set; }
        public bool PointerOverPanel { get; set; }
        public bool Ready { get; set; }

        private void Update()
        {
            if (!Ready || InputBlocked) return;
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
            if (Art != null && axis.x != 0)
            {
                Art.flipX = axis.x < 0;
                Vector3 offset = Art.transform.localPosition;
                offset.x = Mathf.Abs(offset.x) * (Art.flipX ? 1 : -1);
                Art.transform.localPosition = offset;
            }
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
