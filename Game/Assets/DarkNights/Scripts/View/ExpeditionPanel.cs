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
            for (int i = 0; i < Actions.Length; i++)
            {
                string c = Commands[i]; bool prep = e.Phase is 0 or 4, active = e.Phase is 1 or 2;
                bool personal = c is "unload" or "board" or "relay" or "mine";
                Actions[i].interactable = ready && (personal ? a != null : slot == 0) &&
                    (c is "depart" or "robot" or "cargo" or "crew" or "resupply" ? prep : c == "board" ? active || e.Phase == 3 : active);
            }
        }
    }
}
