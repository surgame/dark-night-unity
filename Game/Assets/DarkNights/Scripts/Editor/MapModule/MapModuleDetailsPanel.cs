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
            parent.Add(new Label(rope != null ? "吊绳 · 环境交互物" : "模块 · 原生作者对象") { name = "detail-heading" });
            if (sample == null) { parent.Add(new Label("从左侧选择地图。")); return; }
            if (!editable) parent.Add(new Label("只读预览；点击“打开作者场景”后编辑原生布局。"));
            if (rope != null) { DrawRope(parent, rope, editable, execute); return; }
            if (module == null) { parent.Add(new Label("从列表或画布选择模块。")); return; }
            parent.Add(new Label(module.DisplayName));
            parent.Add(new Label("场景保存位置与连接；Prefab保存内部轮廓、尺寸与端口。"));
            var location = new Vector2IntField("格点位置") { value = module.CellOffset };
            location.SetEnabled(editable && !EditorApplication.isPlayingOrWillChangePlaymode);
            location.RegisterValueChangedCallback(e => execute(() =>
            {
                Undo.RecordObject(module.transform, "移动模块");
                module.transform.position = new Vector3(e.newValue.x * .16f, e.newValue.y * .16f, 0);
                Changed(module.transform);
            }));
            parent.Add(location);
            parent.Add(new Label("1U = 20格 = 3.2世界单位\n当前仅验证水平端口；不自动填过渡。"));
            foreach (var port in module.Ports)
                parent.Add(new Label(port.Id + "  " + port.From + " → " + port.To));
            parent.Add(new Button(() => execute(() =>
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(module.gameObject);
                if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("模块需保存为原生Prefab。");
                PrefabStageUtility.OpenPrefab(path);
            })) { text = "编辑模块Prefab" });
            var inspect = new Button(() => Selection.activeGameObject = module.gameObject) { text = "在Inspector查看实例" };
            inspect.SetEnabled(editable); parent.Add(inspect);
            var spawn = new Button(() => Selection.activeGameObject = sample.Spawn.gameObject) { text = "选择出生锚点" };
            spawn.SetEnabled(editable); parent.Add(spawn);
        }

        private static void DrawRope(VisualElement parent, HangingRopePlacement rope, bool editable, Action<Action> execute)
        {
            parent.Add(new Label(rope.name));
            parent.Add(new Label("固定顶端 · 动态绳身\n抓点沿绳攀爬，离绳保留实际速度。"));
            var anchor = new Vector2Field("锚点位置") { value = rope.transform.position };
            anchor.SetEnabled(editable && !EditorApplication.isPlayingOrWillChangePlaymode);
            anchor.RegisterValueChangedCallback(e => execute(() =>
            {
                Undo.RecordObject(rope.transform, "移动吊绳锚点");
                rope.transform.position = new Vector3(e.newValue.x, e.newValue.y, 0);
                Changed(rope.transform);
            }));
            parent.Add(anchor);
            parent.Add(new Label(rope.SegmentCount + "节 · 总长 " +
                (rope.SegmentCount * rope.SegmentLength).ToString("0.##") + " 世界单位"));
            parent.Add(new Label("E 抓／放 · W/S 攀爬\nA/D 助摆 · Space 离绳"));
            var inspect = new Button(() => Selection.activeGameObject = rope.gameObject) { text = "在Inspector编辑吊绳" };
            inspect.SetEnabled(editable); parent.Add(inspect);
            parent.Add(new Label("绳长、分节和助摆参数由此原生实例配置。画布只显示初始绳形，不模拟运动。"));
        }

        private static void Changed(Transform value)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(value);
            EditorSceneManager.MarkSceneDirty(value.gameObject.scene);
        }
    }
}
