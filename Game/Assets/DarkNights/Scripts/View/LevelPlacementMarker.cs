using DarkNights.Core.Config;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 保存一个可编辑的初始实体位置、内容 ID、生成顺序和外观变体。它只导出冻结配置并绘制场景辅助，不创建会话或预览玩法。
    /// </summary>
    [ExecuteAlways]
    public sealed class LevelPlacementMarker : MonoBehaviour
    {
        [SerializeField] private LevelPlacementCategory category;
        [SerializeField] private string contentId = "";
        [SerializeField] private int spawnOrder;
        [SerializeField] private int variant;
        [SerializeField] private string actorName = "";

        public LevelPlacementCategory Category => category;
        public string ContentId => contentId;
        public int SpawnOrder => spawnOrder;
        public int Variant => variant;
        public string ActorName => actorName;

        public PlacementDefinition CreatePlacement(float x)
        {
            return new PlacementDefinition(contentId, x, variant, actorName);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = category == LevelPlacementCategory.Building
                ? new Color(0.85f, 0.7f, 0.35f, 0.9f)
                : category == LevelPlacementCategory.Worksite
                    ? new Color(0.35f, 0.7f, 0.45f, 0.9f)
                    : new Color(0.45f, 0.7f, 0.9f, 0.9f);
            Gizmos.DrawSphere(transform.position, category == LevelPlacementCategory.Actor ? 3f : 5f);
        }
    }
}
