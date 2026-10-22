using System;
using System.Linq;
using DarkNights.Runtime.Objects;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 原人工一点塌方的UITK作者属性与分步画布工具；效果引用、触发、源岩及限制仍写原Geology草稿。
    /// 常用操作先展示，精确参数分组保留；取格不宣称空间合法，最终冲突沿原完整候选检查。
    /// </summary>
    internal static class TerrainWorkbenchGeologyPage
    {
        internal static void Build(TerrainWorkbenchFields form)
        {
            var owner = form.Owner;
            const string p = "Config.Geology.";
            form.Help("现有能力：每图一个人工塌方点。没有自动／多点分布或伤害玩法。");
            if (!owner.Preview.IsCandidate)
            {
                form.Heading("当前冻结版本（只读）");
                var sites = owner.Preview.Displayed?.GeologySites;
                var selected = sites?.FirstOrDefault(s => s.SiteId == owner.State.SelectedSite) ?? sites?.FirstOrDefault();
                if (selected == null) form.Note("当前冻结版本没有塌方站点。");
                else
                {
                    var frozen = JsonUtility.FromJson<GeologyEventConfig>(selected.Configuration);
                    form.Note(selected.SiteId + " · " + (frozen.EnterRegion ? "进入区域触发" : "挖断支撑触发"));
                    form.Note("源岩 U " + frozen.SourceU + " / V " + frozen.SourceV +
                        "；落地 U " + frozen.LandingU + " / V " + frozen.LandingV);
                    form.Button("定位此冻结站点", () => owner.Preview.FocusCell(selected.AllowedU, -selected.AllowedV));
                }
                form.Help("冻结版本不反写作者输入。先查看／生成作者候选，再编辑本图设计点。");
                form.Button("查看／生成作者候选以编辑", owner.Commands.ViewCandidate, primary: true);
                return;
            }
            form.Field(p + "Enabled", "启用本图塌方点");
            var edit = new VisualElement(); form.Root.Add(edit); form.ShowWhen(p + "Enabled", edit);
            form.Help("画布彩框是当前作者输入的编辑草图，不是已生成站点。更新候选通过静态检查后才进入冻结版。",
                parent: edit);
            form.Button("定位当前塌方草图", () =>
            {
                var c = owner.Draft.Config.Geology.Configuration;
                owner.Preview.FocusCell(c.SourceU, -c.SourceV);
            }, edit);
            form.Field(p + "SiteId", "站点身份", edit);
            form.Field(p + "Definition", "效果Definition", edit);
            form.Button("读取效果配方到本图草稿", () =>
            {
                var definition = owner.Draft.Config.Geology.Definition;
                if (definition == null) throw new InvalidOperationException("先选择效果Definition。");
                owner.Commands.Edit("读取塌方效果配方", () =>
                    owner.Draft.Config.Geology.Configuration = ExpeditionFlowDraft.Clone(
                        definition.SharedConfigs.OfType<GeologyEventConfig>().Single()));
                owner.RebuildPage();
            }, edit);
            form.Field(p + "Configuration.EnterRegion", "角色进入区域触发", edit);
            form.Note("关闭时由支撑格真正被挖空触发。", edit);
            form.Button("① 在画布指定源岩左下格", () => Pick(owner, 1, "源岩左下格"), edit);
            form.Button("② 在画布指定落地基准格", () => Pick(owner, 2, "落地基准格"), edit);
            var support = new VisualElement(); edit.Add(support);
            form.ShowWhen(p + "Configuration.EnterRegion", support, false);
            form.Button("③ 指定支撑格", () => Pick(owner, 3, "支撑格"), support);
            var region = new VisualElement(); edit.Add(region);
            form.ShowWhen(p + "Configuration.EnterRegion", region);
            form.Button("③ 指定进入区左下格", () => Pick(owner, 4, "进入区左下格"), region);
            form.Button("④ 指定退避区左下格", () => Pick(owner, 5, "退避区左下格"), edit);
            form.Button("⑤ 指定允许改形左下格", () => Pick(owner, 6, "允许改形左下格"), edit);
            form.Button("退出画布取格工具（Esc）", () =>
            { owner.State.Pick = 0; owner.Notice = "已退出取格工具，恢复普通画布选择。"; }, edit);
            form.Note("U向右，V向上，地下V为负；源岩／落点限制保持，不自动迁移坐标。", edit);
            var precise = form.Fold("精确坐标与范围", parent: edit);
            Field(form, precise, "Configuration.SourceU", "源岩左下U");
            Field(form, precise, "Configuration.SourceV", "源岩左下V");
            Field(form, precise, "Configuration.Width", "源岩宽（格）");
            Field(form, precise, "Configuration.SourceRows", "源岩高（格）");
            Field(form, precise, "Configuration.LandingU", "落地基准U");
            Field(form, precise, "Configuration.LandingV", "落地基准V");
            Field(form, precise, "Configuration.RubbleWidth", "允许散落宽");
            Field(form, precise, "Configuration.SupportU", "支撑U");
            Field(form, precise, "Configuration.SupportV", "支撑V");
            Field(form, precise, "Configuration.RegionU", "进入区U");
            Field(form, precise, "Configuration.RegionV", "进入区V");
            Field(form, precise, "Configuration.RegionWidth", "进入区宽");
            Field(form, precise, "Configuration.RegionHeight", "进入区高");
            Field(form, precise, "AllowedU", "允许改形U");
            Field(form, precise, "AllowedV", "允许改形V");
            Field(form, precise, "AllowedWidth", "允许改形宽");
            Field(form, precise, "AllowedHeight", "允许改形高");
            Field(form, precise, "RetreatU", "退避区U");
            Field(form, precise, "RetreatV", "退避区V");
            Field(form, precise, "RetreatWidth", "退避区宽");
            Field(form, precise, "RetreatHeight", "退避区高");
            Field(form, precise, "Association", "洞室／矿体关联");
            var effect = form.Fold("前兆、运动与完整效果参数", parent: edit);
            form.Field(p + "Configuration", "事件配方", effect);
            form.Help(owner.Generation.GeologyConflict.Length == 0
                ? "静态空间状态以检查与使用为准；只选择坐标不代表通过。"
                : "静态冲突：" + owner.Generation.GeologyConflict,
                owner.Generation.GeologyConflict.Length == 0 ? HelpBoxMessageType.Info : HelpBoxMessageType.Error);
            form.Button("检查与使用 →", () => owner.SelectPage("release"));
        }

        private static void Field(TerrainWorkbenchFields form, VisualElement parent, string path, string label)
        { form.Field("Config.Geology." + path, label, parent); }

        private static void Pick(TerrainStylePreviewWindow owner, int mode, string label)
        {
            owner.Commands.Guard();
            if (!owner.Preview.IsCandidate) throw new InvalidOperationException("先点击草稿候选，再在该图上指定设计坐标。");
            owner.Preview.UsePan(); owner.State.Pick = mode; owner.State.Sites = true;
            owner.Notice = "接下来左键点击：" + label + "。Esc取消；取格后更新候选检查。";
        }
    }
}
