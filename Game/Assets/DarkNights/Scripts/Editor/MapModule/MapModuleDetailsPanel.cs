using System;
using DarkNights.Runtime.Physics;
using DarkNights.View.MapModule;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.MapModule
{
    /// <summary>
    /// 当前模块或吊绳的作者属性面板；修改写回原生Scene并保留Undo与Prefab覆盖。
    /// 不创建运行状态，轮廓和完整交互参数继续由原生Prefab／Inspector编辑。
    /// </summary>
    internal static class MapModuleDetailsPanel
    {
        internal static void Draw(VisualElement parent, MapModuleCaseAuthoring sample,
            MapModuleAuthoring module, HangingRopePlacement rope, bool editable, Action<Action> execute)
        {
            parent.Clear();
            parent.Add(new Label(rope != null ? "吊绳" : "地形模块") { name = "detail-heading" });
            if (sample == null) { parent.Add(new Label("从左侧选择地图。")); return; }
            if (!editable) parent.Add(new Label("点击顶部“切换场景”后可编辑地图，也可点击 Unity ▶ 试玩。"));
            if (rope != null) { DrawRope(parent, rope, editable, execute); return; }
            if (module == null) { parent.Add(new Label("从列表或画布选择模块。")); return; }
            parent.Add(new Label(module.DisplayName));
            parent.Add(new Label("在场景中调整模块的位置和连接。地形轮廓、尺寸和连接口请在模块 Prefab中修改。"));
            var location = new Vector2IntField("网格坐标") { value = module.CellOffset };
            location.SetEnabled(editable && !EditorApplication.isPlayingOrWillChangePlaymode);
            location.RegisterValueChangedCallback(e => execute(() =>
            {
                Undo.RecordObject(module.transform, "移动模块");
                module.transform.position = new Vector3(e.newValue.x * .16f, e.newValue.y * .16f, 0);
                Changed(module.transform);
            }));
            parent.Add(location);
            parent.Add(new Label("1个模块单位（U）=20格=3.2世界单位\n目前仅支持水平连接，连接处需要手动对齐。"));
            foreach (var port in module.Ports)
                parent.Add(new Label(port.Id + "  " + port.From + " → " + port.To));
            parent.Add(new Button(() => execute(() =>
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(module.gameObject);
                if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("请先将模块保存为Prefab。");
                PrefabStageUtility.OpenPrefab(path);
            })) { text = "编辑模块 Prefab" });
            var inspect = new Button(() => Selection.activeGameObject = module.gameObject) { text = "在 Inspector 中查看" };
            inspect.SetEnabled(editable); parent.Add(inspect);
            var spawn = new Button(() => Selection.activeGameObject = sample.Spawn.gameObject) { text = "选择出生位置" };
            spawn.SetEnabled(editable); parent.Add(spawn);
        }

        private static void DrawRope(VisualElement parent, HangingRopePlacement rope, bool editable, Action<Action> execute)
        {
            parent.Add(new Label(rope.name));
            parent.Add(new Label("绳子顶端固定，绳身可以摆动。\n角色可沿绳攀爬，松手后按当前速度继续运动。"));
            var anchor = new Vector2Field("固定点位置") { value = rope.transform.position };
            anchor.SetEnabled(editable && !EditorApplication.isPlayingOrWillChangePlaymode);
            anchor.RegisterValueChangedCallback(e => execute(() =>
            {
                Undo.RecordObject(rope.transform, "移动吊绳固定点");
                rope.transform.position = new Vector3(e.newValue.x, e.newValue.y, 0);
                Changed(rope.transform);
            }));
            parent.Add(anchor);
            parent.Add(new Label(rope.SegmentCount + "节 · 总长 " +
                (rope.SegmentCount * rope.SegmentLength).ToString("0.##") + " 世界单位"));
            parent.Add(new Label("E 抓／放 · W/S 攀爬\nA/D 助摆 · Space 离绳"));
            var inspect = new Button(() => Selection.activeGameObject = rope.gameObject) { text = "在 Inspector 中编辑吊绳" };
            inspect.SetEnabled(editable); parent.Add(inspect);
            parent.Add(new Label("在 Inspector 中调整绳长、绳节数量和摆荡力度。此处预览显示绳子的初始位置。"));
        }

        private static void Changed(Transform value)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(value);
            EditorSceneManager.MarkSceneDirty(value.gameObject.scene);
        }
    }
}
