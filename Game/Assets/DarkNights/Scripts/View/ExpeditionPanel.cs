using System;
using System.Linq;
using DarkNights.Core.ViewData;
using DarkNights.View.Expedition;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 保留飞船操作的只读展示与原生绑定；当前只开放驾驶、起飞、降落和取消收舱。
    /// 作者按钮资源及 GUID 保留，退出的远征按钮不显示，服务端仍独立验证每次操作。
    /// </summary>
    public sealed class ExpeditionPanel : MonoBehaviour
    {
        public GameObject Panel;
        public Text Status;
        public Button[] Actions;
        public string[] Commands;
        private Text[] labels;
        public bool DebugAvailable { get; private set; }
        public string ActionLabel(int index) => labels[index].text;

        public void Bind(Action<string> command)
        {
            Panel.SetActive(false);
            if (Actions.Length != Commands.Length) throw new InvalidOperationException("飞船按钮绑定不完整。");
            labels = Actions.Select(a => a.GetComponentInChildren<Text>(true)).ToArray();
            for (int i = 0; i < Actions.Length; i++)
            { string operation = Commands[i]; Actions[i].onClick.AddListener(() => command(operation)); }
        }

        public void Present(WorldViewData world, int slot, bool ready, bool hostOnly = false, bool paused = false)
        {
            Panel.SetActive(false);
            var expedition = world?.Expedition;
            var flight = expedition?.Ship;
            DebugAvailable = flight != null;
            if (flight == null) return;
            var actor = expedition.Crew.FirstOrDefault(a => a.OwnerSlot == slot);
            var device = expedition.Devices.FirstOrDefault(d => d.Id == flight.Id);
            bool pilot = flight.PilotId != 0 && flight.PilotId == actor?.Id;
            bool cockpit = JourneyPresentationRules.AtCockpit(world, slot);
            bool allowed = ready && !paused && (!hostOnly || slot == 0);
            string[] stages = { "已停泊", "等待全员登船", "关闭坡道", "飞行中" };
            Status.text = (expedition.Journey?.ActivePlanet?.DisplayName ?? "星球地面") + " · " + stages[flight.Phase] + "\n" +
                $"信用点 {world.Camp.Credits} · {(pilot ? "你在驾驶" : flight.PilotId == 0 ? "驾驶位空闲" : "驾驶位已占用")}\n" +
                $"高度 {(device?.Height ?? 0) - flight.DockHeight:0} · 速度 {flight.VelocityX:0}/{flight.VelocityY:0}\n" +
                (pilot ? "A/D 平移 · 空格上升 · S下降 · 松手悬停；安全接近地面后点击降落。" :
                    "从左侧坡道步行进船，靠近驾驶台按E接管；船内可购买装备。") +
                (!ready ? "\n正在同步，操作尚未开放。" : "") + (paused ? "\n会话已暂停。" : "");
            for (int i = 0; i < Commands.Length; i++)
            {
                string command = Commands[i];
                bool show = command == "pilot" || command == "takeoff" && flight.Phase == 0 ||
                    command == "land" && flight.Phase == 3 || command == "cancel-flight" && flight.Phase is 1 or 2;
                Actions[i].gameObject.SetActive(show);
                if (!show) continue;
                labels[i].text = command switch
                {
                    "pilot" => pilot ? "离开驾驶位" : "接管驾驶",
                    "takeoff" => "起飞",
                    "land" => "降落",
                    _ => "取消收舱"
                };
                Actions[i].interactable = allowed && (command == "pilot" ?
                    pilot ? flight.Phase == 0 : cockpit && flight.PilotId == 0 : pilot);
            }
        }
    }
}
