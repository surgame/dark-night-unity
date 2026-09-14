using GameCore.Objects.Runner;
using UnityEngine;
using UnityEngine.Serialization;

namespace DarkNights.View
{
    /// <summary>
    /// 保存正式场景对象的稳定放置身份和实例初值，并显式关联同根 Loader 与主视图。
    /// Definition、Prefab 和运行状态由各自所有者提供；放置身份由编辑工具自动维护，不要求人工填写。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ScenePlacement : MonoBehaviour
    {
        [SerializeField, HideInInspector] private ObjectDefinitionLoader loader;
        [SerializeField, HideInInspector] private EntityView view;
        [SerializeField, FormerlySerializedAs("variant")] private int initialVariant;
        [SerializeField, FormerlySerializedAs("actorName")] private string initialName = "";
        [SerializeField, HideInInspector] private string placementKey = "";

        public ObjectDefinitionLoader Loader => loader;
        public EntityView View => view;
        public int InitialVariant => initialVariant;
        public string InitialName => initialName;
        public string PlacementKey => placementKey;

#if UNITY_EDITOR
        public void EditorConfigure(ObjectDefinitionLoader source, EntityView owner, int variant, string name)
        {
            loader = source;
            view = owner;
            initialVariant = variant;
            initialName = name;
            EditorEnsurePlacementKey();
        }

        public void EditorEnsurePlacementKey()
        {
            if (string.IsNullOrEmpty(placementKey)) placementKey = System.Guid.NewGuid().ToString("N");
        }

        public void EditorRegeneratePlacementKey()
        {
            placementKey = System.Guid.NewGuid().ToString("N");
        }

        private void OnValidate()
        {
            EditorEnsurePlacementKey();
            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return;
            while (HasDuplicatePlacementKey()) EditorRegeneratePlacementKey();
        }

        private bool HasDuplicatePlacementKey()
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (ScenePlacement other in root.GetComponentsInChildren<ScenePlacement>(true))
                    if (other != this && string.Equals(other.PlacementKey, placementKey, System.StringComparison.Ordinal))
                        return true;
            return false;
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
