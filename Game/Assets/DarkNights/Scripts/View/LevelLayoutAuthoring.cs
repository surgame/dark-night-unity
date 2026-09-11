using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 持有灰松谷的边界和三组初始摆放标记，是布局的唯一可编辑来源。导出时按显式顺序冻结数据并执行结构及核心占地校验。
    /// </summary>
    [ExecuteAlways]
    public sealed class LevelLayoutAuthoring : MonoBehaviour
    {
        private const float AlignmentTolerance = 0.001f;

        [SerializeField] private Transform groundBaseline;
        [SerializeField] private Transform worldEnd;
        [SerializeField] private Transform buildStart;
        [SerializeField] private Transform buildEnd;
        [SerializeField] private Transform enemySpawn;
        [SerializeField] private Transform cameraStart;
        [SerializeField] private Transform buildings;
        [SerializeField] private Transform worksites;
        [SerializeField] private Transform actors;

        public LevelLayout CreateLayout(GameCatalog catalog)
        {
            RequireReferences();
            Vector3 ground = Local(groundBaseline);
            if (Math.Abs(ground.x) > AlignmentTolerance)
                throw new InvalidOperationException("The gameplay ground baseline must start at world x=0.");
            RequireAligned(ground.y, worldEnd, buildStart, buildEnd, enemySpawn, cameraStart);
            IReadOnlyList<PlacementDefinition> buildingEntries = Read(buildings, LevelPlacementCategory.Building, ground.y);
            IReadOnlyList<PlacementDefinition> worksiteEntries = Read(worksites, LevelPlacementCategory.Worksite, ground.y);
            IReadOnlyList<PlacementDefinition> actorEntries = Read(actors, LevelPlacementCategory.Actor, ground.y);
            var layout = new LevelLayout(Local(worldEnd).x, ground.y, Local(buildStart).x, Local(buildEnd).x,
                Local(enemySpawn).x, Local(cameraStart).x, buildingEntries, worksiteEntries, actorEntries);
            layout.Validate(catalog);
            return layout;
        }

        private IReadOnlyList<PlacementDefinition> Read(
            Transform group,
            LevelPlacementCategory expected,
            float groundY)
        {
            LevelPlacementMarker[] markers = group.GetComponentsInChildren<LevelPlacementMarker>(true)
                .OrderBy(marker => marker.SpawnOrder).ToArray();
            if (markers.Length == 0 || markers.Select(marker => marker.SpawnOrder).Distinct().Count() != markers.Length)
                throw new InvalidOperationException(group.name + " must contain uniquely ordered placement markers.");
            var entries = new List<PlacementDefinition>(markers.Length);
            foreach (LevelPlacementMarker marker in markers)
            {
                Vector3 point = Local(marker.transform);
                if (marker.Category != expected || Math.Abs(point.y - groundY) > AlignmentTolerance)
                    throw new InvalidOperationException(marker.name + " has the wrong category or ground alignment.");
                entries.Add(marker.CreatePlacement(point.x));
            }
            return entries.AsReadOnly();
        }

        private void RequireReferences()
        {
            if (groundBaseline == null || worldEnd == null || buildStart == null || buildEnd == null ||
                enemySpawn == null || cameraStart == null || buildings == null || worksites == null || actors == null)
                throw new InvalidOperationException("Assign every Pinewatch layout boundary and placement group.");
        }

        private void RequireAligned(float groundY, params Transform[] anchors)
        {
            foreach (Transform anchor in anchors)
                if (Math.Abs(Local(anchor).y - groundY) > AlignmentTolerance)
                    throw new InvalidOperationException(anchor.name + " must align with the gameplay ground baseline.");
        }

        private Vector3 Local(Transform value)
        {
            Vector3 point = Vector3.zero;
            while (value != transform)
            {
                if (value == null) throw new InvalidOperationException("Layout reference must belong to the authoring root.");
                point = value.localPosition + value.localRotation * Vector3.Scale(point, value.localScale);
                value = value.parent;
            }
            return point;
        }

        private void OnDrawGizmos()
        {
            if (groundBaseline == null || worldEnd == null)
                return;
            Gizmos.color = new Color(0.7f, 0.8f, 0.65f, 0.8f);
            Gizmos.DrawLine(groundBaseline.position, worldEnd.position);
            if (buildStart == null || buildEnd == null)
                return;
            Vector3 offset = transform.TransformVector(new Vector3(0, 12, 0));
            Gizmos.color = new Color(0.85f, 0.75f, 0.55f, 0.8f);
            Gizmos.DrawLine(buildStart.position + offset, buildEnd.position + offset);
        }
    }
}
