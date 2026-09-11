using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using GameCore.Objects.Views;
using GameCore.UI.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 正式 HUD 的生成绑定与只读展示，所有费用和条件读取同一目录及权威投影。
    /// 选择和建造意图由客户端传入，按钮仅发意图；禁用提示不能替代服务端校验。
    /// </summary>
    public sealed partial class CampHudBehaviour : MenuBehaviour
    {
        [ViewComponent("FoodValue")] private Text foodValue;
        [ViewComponent("WoodValue")] private Text woodValue;
        [ViewComponent("StoneValue")] private Text stoneValue;
        [ViewComponent("IronValue")] private Text ironValue;
        [ViewComponent("GoldValue")] private Text goldValue;
        [ViewComponent("Population")] private Text population;
        [ViewComponent("Phase")] private Text phase;
        [ViewComponent("Detail")] private Text detail;
        [ViewComponent("Title")] private Text title;
        [ViewComponent("State")] private Text state;
        [ViewComponent("Info")] private Text info;
        [ViewComponent("Objective")] private Text objective;
        [ViewComponent("Hint")] private Text hint;
        [ViewComponent("Toast")] private Text toast;
        [ViewComponent("BannerTitle")] private Text bannerTitle;
        [ViewComponent("BannerDetail")] private Text bannerDetail;
        [ViewComponent("Progress")] private Image progress;
        [ViewComponent("Hp")] private Image hp;
        [ViewComponent("Portrait")] private Image portrait;
        [ViewComponent("ToastPanel")] private RectTransform toastPanel;
        [ViewComponent("Banner")] private RectTransform banner;
        [ViewComponent("Map")] private CampMap map;
        [ViewComponent("Overlay")] private CampOverlay overlay;
        private GameCatalog catalog;
        private double toastRemaining, bannerRemaining;
        private CanvasGroup toastFade, bannerFade;
        public void PresentWorld(SessionViewData frame, CampInput input, IEntityVisuals entities, PinewatchStage stage, bool ready)
        {
            map.Present(frame, stage, stage.WorldWidth, ready);
            overlay.Present(frame, input, entities, stage, catalog);
        }

        public void Configure(GameCatalog value)
        {
            catalog = value;
            toastFade = View.Get<CanvasGroup>("ToastFade");
            bannerFade = View.Get<CanvasGroup>("BannerFade");
            foreach (string kind in new[] { "house", "farm", "barracks", "tower" })
                Label("Build" + Capital(kind)).text = catalog.Balance.Buildings[kind].Name + "\n" + GameText.Cost(catalog.Balance.Buildings[kind].Cost);
            foreach (string kind in new[] { "spearman", "archer" })
                Label("Train" + Capital(kind)).text = catalog.Balance.Units[kind].Name + "\n" + GameText.Cost(catalog.Balance.Units[kind].Cost);
            Label("Repair").text = "修缮建筑\n" + GameText.Cost(catalog.Balance.Economy.RepairCost);
            banner.gameObject.SetActive(false);
            toastPanel.gameObject.SetActive(false);
        }

        public void ShowMessage(string value, bool warning = true, double remaining = 5)
        {
            toast.text = value;
            toast.color = warning ? new Color32(239, 180, 156, 255) : new Color32(228, 229, 215, 255);
            toastRemaining = remaining;
        }

        public void ResetMessages() { toastRemaining = bannerRemaining = 0; }
        public void PresentEvent(PresentationEvent value, double age)
        {
            if (value.Type == "message") ShowMessage(value.Text, value.Warning, Math.Max(0, 5 - age));
            else if (value.Type == "banner")
            {
                bannerTitle.text = value.Text;
                bannerDetail.text = value.Detail;
                bannerRemaining = Math.Max(0, 5 - age);
            }
        }

        public void Present(SessionViewData frame, IReadOnlyList<int> selected, string buildKind, int hover,
            bool ready, int slot, Sprite selectedPortrait, bool modal)
        {
            CampViewData camp = frame.World.Camp;
            Text[] resources = { foodValue, woodValue, stoneValue, ironValue, goldValue };
            for (int i = 0; i < resources.Length; i++) resources[i].text = Math.Floor(camp.Stock.Get(GameText.ResourceIds[i])).ToString();
            population.text = $"{camp.Population} / {camp.Capacity}";
            bool day = camp.WavePhase == "Day";
            var wave = catalog.Level.Waves[camp.WaveIndex];
            phase.text = day ? $"第 {camp.WaveIndex + 1} 日 · 白昼" : $"第 {camp.WaveIndex + 1} 夜 · 守住防线";
            detail.text = day ? "距入夜 " + GameText.Clock(camp.DayRemaining) : $"敌人 {camp.EnemyCount}  ·  已来袭 {camp.NextSpawn} / {wave.Enemies.Count}";
            if (frame.Paused) detail.text = "已暂停 · 空格继续";
            progress.fillAmount = day ? (float)(camp.DayRemaining / wave.DaySeconds) : (float)camp.NextSpawn / wave.Enemies.Count;
            var readout = SelectionReadout.Describe(frame.World, selected, catalog);
            title.text = readout.Title;
            state.text = readout.State;
            info.text = readout.Info;
            hp.gameObject.SetActive(readout.Hp >= 0);
            hp.fillAmount = readout.Hp;
            portrait.sprite = selectedPortrait;
            portrait.color = selectedPortrait == null ? Color.clear : Color.white;
            objective.text = day ? $"准备营地  ·  生产中的工人 {frame.World.Actors.Count(a => a.Activity == "Work" || a.Activity == "WorkMove")}  ·  守卫 {frame.World.Actors.Count(a => !a.Enemy && a.Kind != "worker")}  ·  建造守望塔加固东侧" :
                $"守住酒馆  ·  第 {camp.WaveIndex + 1} / {catalog.Level.Waves.Count} 次夜袭  ·  东侧来敌 →";
            hint.text = buildKind.Length > 0 ? $"{catalog.Balance.Buildings[buildKind].Name}放置中 · 左键确认 · 右键取消" : SelectionReadout.Hint(frame.World, hover, catalog);
            Buttons(frame, selected, ready, slot);
            toastRemaining = Math.Max(0, toastRemaining - Time.unscaledDeltaTime);
            bannerRemaining = Math.Max(0, bannerRemaining - Time.unscaledDeltaTime);
            toastPanel.gameObject.SetActive(toastRemaining > 0 && !modal);
            banner.gameObject.SetActive(bannerRemaining > 0 && !modal);
            toastFade.alpha = (float)Math.Min(1, toastRemaining);
            bannerFade.alpha = (float)Math.Min(1, bannerRemaining);
        }

        private void Buttons(SessionViewData frame, IReadOnlyList<int> selected, bool ready, int slot)
        {
            CampViewData camp = frame.World.Camp;
            bool permission = ready && (!frame.HostOnly || slot == 0);
            bool worker = frame.World.Actors.Any(a => selected.Contains(a.Id) && a.Kind == "worker" && a.Activity != "Training" && a.Activity != "TrainingMove");
            foreach (string kind in new[] { "house", "farm", "barracks", "tower" })
                Button("Build" + Capital(kind)).interactable = permission && CanPay(camp.Stock, catalog.Balance.Buildings[kind].Cost);
            foreach (string kind in new[] { "spearman", "archer" })
                Button("Train" + Capital(kind)).interactable = permission && worker && CanPay(camp.Stock, catalog.Balance.Units[kind].Cost);
            var rules = catalog.Balance.Economy;
            Button("Recruit").interactable = permission && camp.Population < camp.Capacity && camp.RecruitCooldown <= 0 && CanPay(camp.Stock, rules.RecruitCost);
            Label("Recruit").text = camp.RecruitCooldown > 0 ? $"招募 {Math.Ceiling(camp.RecruitCooldown)}s\n{GameText.Cost(rules.RecruitCost)}" : "招募工人\n" + GameText.Cost(rules.RecruitCost);
            var building = selected.Count == 1 ? frame.World.Buildings.FirstOrDefault(b => b.Id == selected[0]) : null;
            Button("Repair").interactable = permission && building != null && building.Progress >= 1 && building.Hp < catalog.Balance.Buildings[building.Kind].Hp && CanPay(camp.Stock, rules.RepairCost);
            Button("Pause").interactable = Button("Speed").interactable = ready && slot == 0;
            Button("Night").interactable = ready && slot == 0 && camp.WavePhase == "Day" && !frame.Paused;
            Label("Pause").text = frame.Paused ? "▶ 继续" : "Ⅱ 暂停";
            Label("Speed").text = frame.Speed == 2 ? "2×" : "1×";
            Label("Night").text = camp.WavePhase == "Night" ? "夜袭进行中" : "提前入夜";
        }

        private Button Button(string key) => View.Get<Button>(key);
        private Text Label(string key) => View.Get<Text>(key + "Label");
        private static string Capital(string value) => char.ToUpperInvariant(value[0]) + value.Substring(1);
        private static bool CanPay(ResourceAmounts stock, ResourceAmounts cost) => GameText.ResourceIds.All(id => stock.Get(id) >= cost.Get(id));
        [UGUIOnClick("BuildHouse")] private void OnBuildHouse() => Raise("BuildHouse");
        [UGUIOnClick("BuildFarm")] private void OnBuildFarm() => Raise("BuildFarm");
        [UGUIOnClick("BuildBarracks")] private void OnBuildBarracks() => Raise("BuildBarracks");
        [UGUIOnClick("BuildTower")] private void OnBuildTower() => Raise("BuildTower");
        [UGUIOnClick("TrainSpearman")] private void OnTrainSpearman() => Raise("TrainSpearman");
        [UGUIOnClick("TrainArcher")] private void OnTrainArcher() => Raise("TrainArcher");
        [UGUIOnClick("Recruit")] private void OnRecruit() => Raise("Recruit");
        [UGUIOnClick("Repair")] private void OnRepair() => Raise("Repair");
        [UGUIOnClick("Pause")] private void OnPause() => Raise("Pause");
        [UGUIOnClick("Speed")] private void OnSpeed() => Raise("Speed");
        [UGUIOnClick("Night")] private void OnNight() => Raise("Night");
        [UGUIOnClick("Help")] private void OnHelp() => Raise("Help");
        [UGUIOnClick("Menu")] private void OnMenu() => Raise("Menu");
    }
}
