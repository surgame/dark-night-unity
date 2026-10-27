using System;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>光照调参台右侧预览的可重放配置；地图与格位置属于本地工具，旋转不修改角色、作者地图或玩家存档。</summary>
    [Serializable]
    public sealed class FlashlightPreviewSettings
    {
        public string MapId = "erosion";
        public int RoomIndex;
        public bool UseRoomStart = true;
        public Vector2 Position;
        public bool AutoRotate = true;
        public float Angle = 20;
        public float DegreesPerSecond = 45;
        public float OrthographicSize = 8;
        public Vector2 CameraOffset;
        public bool Enabled = true;
        public bool CompareSaved;
        public static readonly string[] MapIds = LightingPreviewMap.Ids;
        public static readonly string[] MapNames = LightingPreviewMap.Names;

        public FlashlightPreviewSettings Copy() => JsonUtility.FromJson<FlashlightPreviewSettings>(JsonUtility.ToJson(this));
        public void Validate()
        {
            if (Array.IndexOf(MapIds, MapId) < 0 || RoomIndex < 0 || !Finite(Position.x) || !Finite(Position.y) ||
                Position.x < 0 || Position.x > 512 || Position.y < 0 || Position.y > 512 || !Finite(Angle) ||
                !Finite(DegreesPerSecond) || DegreesPerSecond < 1 || DegreesPerSecond > 180 ||
                !Finite(OrthographicSize) || OrthographicSize < 3 || OrthographicSize > 24 ||
                !Finite(CameraOffset.x) || !Finite(CameraOffset.y) || Mathf.Abs(CameraOffset.x) > 512 || Mathf.Abs(CameraOffset.y) > 512)
                throw new ArgumentException("预览地图、格位置、旋转速度或镜头范围无效。");
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
