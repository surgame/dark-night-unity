using GameCore.Objects.Runner;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.View
{
    /// <summary>
    /// 只保存场景实例的生成顺序、名字和变体，并显式关联同一实例的 Loader 与 ObjectView。
    /// 身份、分类和 Prefab 均来自 Loader 的 Definition；本组件不初始化对象，也不拥有游戏状态。
    /// </summary>
    [ExecuteAlways]
    public sealed class LevelPlacementMarker : MonoBehaviour
    {
        [SerializeField] private ObjectDefinitionLoader loader;
        [SerializeField] private ObjectView view;
        [SerializeField] private int spawnOrder;
        [SerializeField] private int variant;
        [SerializeField] private string actorName = "";

        public ObjectDefinitionLoader Loader => loader;
        public ObjectView View => view;
        public int SpawnOrder => spawnOrder;
        public int Variant => variant;
        public string ActorName => actorName;

#if UNITY_EDITOR
        public void EditorConfigure(ObjectDefinitionLoader source, ObjectView owner, int order, int appearance, string initialName)
        {
            loader = source;
            view = owner;
            spawnOrder = order;
            variant = appearance;
            actorName = initialName;
        }
#endif

        private void OnDrawGizmos()
        {
            if (loader == null) return;
            Gizmos.color = new Color(0.45f, 0.7f, 0.9f, 0.9f);
            Gizmos.DrawSphere(loader.transform.position, 0.03f);
        }
    }
}
