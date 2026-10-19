using System;
using System.Linq;
using DarkNights.Runtime.Network;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>
    /// 显式开发回放中的原生UIToolkit导航操作与只读诊断；触发原按钮事件，不写业务状态。
    /// 每次报告重新读取当前视图，跨连接和世界不缓存元素；正式Player不提供自动化入口。
    /// </summary>
    internal static class NavigationAutomation
    {
        internal static void Apply(SessionNetwork network, string name)
        {
            var root = Root(network);
            var button = root?.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy || button.panel == null ||
                button.resolvedStyle.display == DisplayStyle.None)
                throw new InvalidOperationException("导航按钮当前不可操作：" + name);
            using var submit = NavigationSubmitEvent.GetPooled();
            submit.target = button; button.SendEvent(submit);
        }

        internal static JToken Capture(SessionNetwork network)
        {
            var root = Root(network);
            if (root == null) return JValue.CreateNull();
            var status = root.Q<Label>("flight-status");
            return new JObject
            {
                ["visible"] = root.panel != null && root.resolvedStyle.display != DisplayStyle.None,
                ["open"] = root.Q("navigation").ClassListContains("is-open"),
                ["status"] = status?.text ?? "",
                ["detail"] = root.Q<Label>("navigation-detail")?.text ?? "",
                ["confirmEnabled"] = root.Q<Button>("confirm-destination").enabledInHierarchy,
                ["selected"] = root.Query<Button>(className: "navigation-row").ToList()
                    .FirstOrDefault(row => row.ClassListContains("is-selected"))?.name ?? "",
                ["shipMarker"] = root.Q("ship-position") != null,
                ["markerOverlapsNode"] = root.Q("ship-position") != null && root.Query<Button>(className: "star-node")
                    .ToList().Any(b => b.worldBound.Overlaps(root.Q("ship-position").worldBound))
            };
        }

        private static VisualElement Root(SessionNetwork network) =>
            network.GetComponent<ShipTradeHud>()?.GetComponent<UIDocument>()?.rootVisualElement
                .Q("ship-ui-root").parent;
    }
}
