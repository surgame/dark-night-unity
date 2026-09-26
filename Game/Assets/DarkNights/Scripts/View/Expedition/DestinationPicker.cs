using System;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;
using GameCore.Interactions;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.View.Expedition
{
    /// <summary>现有 UGUI 面板下的本地星球选择页；目录只来自服务端冻结投影，浏览和关闭不会占用驾驶席或启动生成。</summary>
    public sealed class DestinationPicker : MonoBehaviour
    {
        private const int PageSize = 4;
        private readonly Button[] rows = new Button[PageSize];
        private readonly Text[] labels = new Text[PageSize];
        private GameObject surface;
        private Text title, details, message, pages;
        private Button confirm, previous, next;
        private PlanetDefinition[] planets = Array.Empty<PlanetDefinition>();
        private string selected = "", signature = "";
        private int page;
        private bool allowed;
        private Action<string> submit;
        private IYYInteractionSessionService sessions;
        private YYInteractionSessionHandle modal;

        public void Initialize(ExpeditionPanel owner, IYYInteractionSessionService interactions, Action<string> select)
        {
            submit = select; sessions = interactions ?? throw new ArgumentNullException(nameof(interactions));
            surface = new GameObject("Destination selection", typeof(RectTransform), typeof(Image));
            surface.transform.SetParent(owner.Panel.transform.parent, false);
            var root = (RectTransform)surface.transform;
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
            root.sizeDelta = new Vector2(560, 460);
            surface.GetComponent<Image>().color = new Color(.035f, .055f, .09f, .98f);
            title = Label("目的地导航", 20, 20, 520, 32, owner.Status);
            for (int i = 0; i < PageSize; i++)
            {
                int index = i;
                rows[i] = Button("", 20, 65 + 49 * i, 520, 43, owner.Status,
                    () => { selected = planets[page * PageSize + index].Id; RenderRows(); });
                labels[i] = rows[i].GetComponentInChildren<Text>();
            }
            previous = Button("上一页", 20, 270, 100, 30, owner.Status, () => { page--; RenderRows(); });
            pages = Label("", 140, 273, 280, 28, owner.Status);
            pages.alignment = TextAnchor.MiddleCenter;
            next = Button("下一页", 440, 270, 100, 30, owner.Status, () => { page++; RenderRows(); });
            details = Label("", 20, 310, 520, 45, owner.Status);
            details.fontSize = 14;
            message = Label("", 20, 362, 520, 43, owner.Status);
            message.fontSize = 14;
            confirm = Button("确认目的地并驾驶", 20, 414, 300, 32, owner.Status,
                () => { if (allowed && selected.Length != 0) submit(selected); });
            Button("关闭", 340, 414, 200, 32, owner.Status, Close);
            surface.SetActive(false);
        }

        public void Open()
        {
            if (modal?.Session?.IsActive == true) return;
            Close();
            if (!sessions.TryBegin(new YYInteractionSessionDescriptor
            {
                Kind = "dark_nights.destination", Owner = nameof(DestinationPicker), Priority = 80,
                Blocks = YYInteractionBlockFlags.All
            }, out modal)) { modal = null; return; }
            surface.SetActive(true); surface.transform.SetAsLastSibling(); RenderRows();
        }

        public void Close()
        {
            modal?.Dispose(); modal = null;
            if (surface != null) surface.SetActive(false);
        }

        public void Present(SessionViewData frame, int slot, bool ready, bool pending, string feedback)
        {
            var journey = frame?.World.Expedition?.Journey;
            if (journey?.Enabled != true || journey.Phase != JourneyPhase.Orbit) { Close(); return; }
            if (modal != null && modal.Session.IsEnded) Close();
            string nextSignature = string.Join("|", journey.Planets.Select(p => p.Id + ":" + p.DisplayName + ":" + p.Description + ":" + p.Enabled));
            if (signature != nextSignature)
            {
                signature = nextSignature;
                planets = journey.Planets.Where(p => p.Enabled).ToArray();
                page = Mathf.Clamp(page, 0, Math.Max(0, (planets.Length - 1) / PageSize));
                if (!planets.Any(p => p.Id == selected)) selected = planets.FirstOrDefault()?.Id ?? "";
                RenderRows();
            }
            string blocked = JourneyPresentationRules.SelectionBlock(frame, slot, ready);
            allowed = blocked.Length == 0 && !pending && selected.Length != 0;
            confirm.interactable = allowed;
            message.text = pending ? "正在等待服务端确认…" : blocked.Length != 0 ? blocked :
                feedback.Length != 0 ? feedback : "确认后取得驾驶席；仅浏览不会影响其他乘员。";
        }

        private void RenderRows()
        {
            if (surface == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                int index = page * PageSize + i;
                rows[i].gameObject.SetActive(index < planets.Length);
                if (index >= planets.Length) continue;
                var planet = planets[index];
                labels[i].text = (planet.Id == selected ? "●  " : "○  ") + planet.DisplayName;
            }
            pages.text = planets.Length == 0 ? "暂无启用的目的地" : $"{page + 1} / {(planets.Length + PageSize - 1) / PageSize}";
            previous.interactable = page > 0;
            next.interactable = (page + 1) * PageSize < planets.Length;
            details.text = planets.FirstOrDefault(p => p.Id == selected)?.Description ?? "请在星球配置面板启用至少一个预设。";
        }

        private Text Label(string value, float x, float y, float width, float height, Text template)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(surface.transform, false);
            Place((RectTransform)go.transform, x, y, width, height);
            var label = go.GetComponent<Text>();
            label.font = template.font; label.fontSize = 17; label.color = template.color;
            label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.text = value;
            return label;
        }

        private Button Button(string value, float x, float y, float width, float height, Text template, Action click)
        {
            var go = new GameObject("Navigation button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(surface.transform, false);
            Place((RectTransform)go.transform, x, y, width, height);
            var image = go.GetComponent<Image>(); image.color = new Color(.12f, .2f, .29f, 1);
            var button = go.GetComponent<Button>(); button.targetGraphic = image;
            var label = Label(value, 0, 0, width - 16, height, template);
            label.transform.SetParent(go.transform, false);
            Place(label.rectTransform, 8, 0, width - 16, height);
            label.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(() => click());
            return button;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }

        private void OnDisable() => Close();
        private void OnDestroy() { Close(); if (surface != null) Destroy(surface); }
    }
}
