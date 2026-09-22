using System;
using System.Linq;
using DarkNights.Core.ViewData;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>原生远征 HUD 的显式绑定；只显示冻结库存和阶段，按钮只提交意图，隐藏时不拦截世界输入。</summary>
    public sealed class ExpeditionPanel : MonoBehaviour
    {
        public GameObject Panel;
        public Text Status;
        public Button[] Actions;
        public string[] Commands;
        public void Bind(Action<string> command)
        {
            if (Actions.Length != Commands.Length) throw new InvalidOperationException("远征按钮绑定不完整。");
            for (int i = 0; i < Actions.Length; i++)
            { string name = Commands[i]; Actions[i].onClick.AddListener(() => command(name)); }
        }
        public void Present(WorldViewData world, int slot, bool ready)
        {
            var e = world?.Expedition; Panel.SetActive(e != null && ready);
            if (e == null) return;
            var a = e.Crew.FirstOrDefault(c => c.OwnerSlot == slot);
            var shipId = world.Buildings.FirstOrDefault(b => b.Kind == "ship")?.Id ?? 0;
            var ship = e.Devices.FirstOrDefault(d => d.Id == shipId);
            int forward = e.Devices.Where(d => d.Id != shipId).Sum(d => d.Iron + d.Gold);
            int exposed = e.Crew.Where(c => !c.Boarded && c.Role != 3).Sum(c => c.Iron + c.Gold) + forward;
            int devices = e.Devices.Count(d => d.Id != shipId && d.Stage != 0 && d.Stage != 6);
            string[] stages = { "整备", "探索", "撤收", "起飞倒计时", "结算" };
            Status.text = $"远征 {e.Run} · {stages[e.Phase]}  警戒 {e.Risk:0}\n" +
                $"氧气 {a?.Oxygen ?? 0:0}  携带 {(a?.Iron ?? 0) + (a?.Gold ?? 0)}  船仓 {(ship?.Iron ?? 0) + (ship?.Gold ?? 0)}  前线 {forward}\n" +
                $"可用铁 {world.Camp.Stock.Iron} / 金 {world.Camp.Stock.Gold}  舱段 机器人{e.RobotModule} 货舱{e.CargoModule} 船员{e.CrewModule}\n" +
                (e.Phase == 3 ? $"{e.Clock:0.0} 秒后起飞 · 当前未登船 {e.Crew.Count(c => !c.Boarded)}" :
                e.Phase == 4 ? $"损失：货物 {e.LostCargo}，设备 {e.LostDevices}；补充需 {e.ResupplyCost} 铁。自动存档 10。" :
                $"未归队：人员 {e.Crew.Count(c => !c.Boarded && c.Role != 3)} · 货物 {exposed} · 设备 {devices}；中继在脚下，派工选近矿。");
            var flight = e.Ship;
            bool piloting = flight != null && flight.PilotId == a?.Id;
            string[] flightStages = { "泊位", "等待归队", "关闭舱门", "悬停飞行" };
            Status.text += $"\n飞船：{flightStages[flight?.Phase ?? 0]} · {(flight?.PilotId > 0 ? "驾驶位已占用" : "驾驶位空闲")}\n" +
                (piloting ? "A/D 平移 · 空格上升 · S 下降 · 松开悬停；仅可在原泊位着陆。" : "左坡道进舱 → 短梯到驾驶位；坡道前按 S 可贴地绕行。");
            for (int i = 0; i < Actions.Length; i++)
            {
                string c = Commands[i]; bool prep = e.Phase is 0 or 4, active = e.Phase is 1 or 2;
                bool personal = c is "unload" or "board" or "relay" or "mine" or "pilot" or "takeoff" or "land" or "cancel-flight" or "deploy";
                Actions[i].interactable = ready && (personal ? a != null : slot == 0) &&
                    (c == "pilot" ? e.Phase != 3 && a.Boarded :
                     c == "takeoff" ? piloting && flight.Phase == 0 && e.Phase != 2 :
                     c == "land" ? piloting && flight.Phase == 3 :
                     c == "cancel-flight" ? piloting && flight.Phase is 1 or 2 :
                     c == "deploy" ? piloting && flight.Phase == 0 && e.Phase == 1 :
                     c is "depart" or "robot" or "cargo" or "crew" or "resupply" ? prep && flight?.Phase == 0 :
                     c == "board" ? (active || e.Phase == 3) && a.Boarded : active);
            }
        }
    }
}
