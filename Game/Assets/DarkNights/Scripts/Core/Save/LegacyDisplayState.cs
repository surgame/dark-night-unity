using System;
using System.Collections.Generic;

namespace DarkNights.Core.Save
{
    /// <summary>
    /// 旧单机存档中的房主本地显示设置副本，导入者可选择应用；不进入权威世界，不覆盖其他客户端的镜头或选择。
    /// </summary>
    public sealed class LegacyDisplayState
    {
        public float CameraX { get; }
        public float CameraZoom { get; }
        public IReadOnlyList<int> SelectedIds { get; }

        public LegacyDisplayState(float cameraX, float cameraZoom, IReadOnlyList<int> selectedIds)
        {
            CameraX = cameraX;
            CameraZoom = cameraZoom;
            SelectedIds = new List<int>(selectedIds ?? throw new ArgumentNullException(nameof(selectedIds))).AsReadOnly();
        }
    }
}
