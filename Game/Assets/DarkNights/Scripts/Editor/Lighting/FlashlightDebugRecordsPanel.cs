using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Lighting
{
    /// <summary>命名光照记录的虚拟列表；历史只读展示且不自动删除，显式载入经过校验后交窗口恢复草稿。</summary>
    public sealed class FlashlightDebugRecordsPanel
    {
        private readonly string[] files;
        private readonly Label detail;
        private string selected;
        public FlashlightDebugRecordsPanel(FlashlightDebugWindow window, VisualElement root)
        {
            files = FlashlightDebugRecordStore.Files().ToArray();
            var list = new ListView(files, 46, () =>
            { var label = new Label(); label.AddToClassList("history-item"); return label; }, (element, index) =>
            {
                var label = (Label)element;
                try
                {
                    var record = JsonUtility.FromJson<FlashlightDebugRecord>(File.ReadAllText(files[index]));
                    label.text = record.Name + "\n" + DateTime.Parse(record.CapturedUtc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                }
                catch { label.text = Path.GetFileName(files[index]) + "\n无法读取，原文件保留"; }
            }) { selectionType = SelectionType.Single };
            list.AddToClassList("history-list"); root.Add(list);
            detail = new Label(files.Length == 0 ? "尚无记录。在右侧命名，使用底部「保存全部」新增。" : "选择记录可查看地图、位置与保存来源。");
            detail.AddToClassList("history-detail"); root.Add(detail);
            var load = new Button(() =>
            { try { if (selected != null) window.LoadRecord(FlashlightDebugRecordStore.Read(selected)); } catch (Exception error) { detail.text = error.Message; } }) { text = "载入所选记录到草稿" };
            load.SetEnabled(false); root.Add(load);
            list.selectionChanged += values =>
            {
                selected = values.OfType<string>().FirstOrDefault(); load.SetEnabled(selected != null);
                try
                {
                    var record = selected != null ? FlashlightDebugRecordStore.Read(selected) : null;
                    detail.text = record == null ? "请选择记录。" : record.Name + "\n" + record.Preview.MapId + " · " +
                        (record.Preview.AutoRotate ? "自动旋转 " + record.Preview.DegreesPerSecond + "°/秒" : "固定方向 " + record.Preview.Angle + "°") +
                        "\n来源：" + AssetDatabase.GUIDToAssetPath(record.DefinitionGuid) + "\n" + selected;
                }
                catch (Exception error) { detail.text = error.Message; load.SetEnabled(false); }
            };
        }
    }
}
