#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using GameCore.Debugging;
using GameCore.Objects.Definition;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>
    /// 主菜单专属快速测试宿主；按状态变化注册 Hub 页，绘制只排队一次启动，Update 串行处理。
    /// 使用主菜单原有模态，离开、失败和卸载释放面板，不持有权威世界或修改普通开局。
    /// </summary>
    public sealed class QuickTestHub : MonoBehaviour
    {
        private SessionNetwork network;
        private SessionUiController ui;
        private QuickTestPanel panel;
        private bool registered, requested, launching;
        public bool Available => ui != null && ui.Page == "MainMenu" && network != null && network.CanStartSession &&
            network.Client.Replica.Current == null && network.Terrain != null && !network.Terrain.Selecting && !requested && !launching;
        public bool PanelRegistered => registered;

        public static void Install(SessionNetwork network, SessionUiController ui, bool expedition)
        {
            if (!expedition) return;
            var hub = network.gameObject.AddComponent<QuickTestHub>();
            hub.network = network; hub.ui = ui; hub.panel = new QuickTestPanel(hub);
        }
        public bool RequestLaunch()
        {
            if (!Available) return false;
            requested = true; return true;
        }
        private void Update()
        {
            bool visible = Available;
            if (visible != registered)
            {
                if (visible) RuntimeDebugHub.RegisterPanel(panel);
                else RuntimeDebugHub.UnregisterPanel(panel);
                registered = visible;
            }
            if (requested) { requested = false; Launch(); }
        }
        private async void Launch()
        {
            launching = true;
            try
            {
                var flow = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch")
                    .SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
                var preset = QuickTestPreset.Create(QuickTestSelection.SelectedId, flow);
                QuickTestSelection.Select(preset.Id);
                await network.Connect(true, "127.0.0.1", 27777, preset);
            }
            catch (Exception error) { if (network != null) network.Fail(error); }
            finally { launching = false; }
        }
        private void OnDisable()
        {
            if (panel != null) RuntimeDebugHub.UnregisterPanel(panel);
            registered = requested = false;
        }
    }
}
#endif
