using DarkNights.Core.Config.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 矿巢制作预览的本机工具预设；字段编辑支持原生Undo，仅显式保存到UserSettings。
    /// 不属于WorldSession配置、作者地图或审核版本，启用不会改变正式开局。
    /// </summary>
    [FilePath("UserSettings/DarkNights/TerrainVeinStudy.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class TerrainVeinStudySettings : ScriptableSingleton<TerrainVeinStudySettings>
    {
        public bool Enabled;
        public TerrainVeinShape Shape;
        public int Capacity = 256;
        public bool HasNest = true;
        public int SiteIndex;
        public int Variation;
        public bool XRay = true;
        public bool Automatic = true;
        public int JointVeinCount = 6;
        public bool JointNests = true;
        [SerializeField, HideInInspector] private string savedPreset;
        [System.NonSerialized] private string savedBaseline;
        public bool HasChanges => savedBaseline != PresetJson();
        private void OnEnable()
        {
            if (string.IsNullOrEmpty(savedPreset)) savedPreset = PresetJson();
            else if (savedPreset.Split('|').Length == 8) savedPreset += "|6|True";
            savedBaseline = savedPreset;
            Undo.undoRedoPerformed += RestoreSavedBaseline;
        }
        private void OnDisable() { Undo.undoRedoPerformed -= RestoreSavedBaseline; }
        private void RestoreSavedBaseline() { savedPreset = savedBaseline; }
        private string PresetJson() => Enabled + "|" + Shape + "|" + Capacity + "|" + HasNest + "|" +
            SiteIndex + "|" + Variation + "|" + XRay + "|" + Automatic + "|" + JointVeinCount + "|" + JointNests;
        public void SavePreset()
        {
            Undo.FlushUndoRecordObjects(); savedBaseline = PresetJson(); savedPreset = savedBaseline;
            Save(true); EditorUtility.ClearDirty(this);
        }
        public void DiscardPreset()
        {
            var values = savedBaseline.Split('|');
            Undo.RecordObject(this, "取消矿巢工具预设修改");
            Enabled = bool.Parse(values[0]); Shape = (TerrainVeinShape)System.Enum.Parse(typeof(TerrainVeinShape), values[1]);
            Capacity = int.Parse(values[2]); HasNest = bool.Parse(values[3]); SiteIndex = int.Parse(values[4]);
            Variation = int.Parse(values[5]); XRay = bool.Parse(values[6]); Automatic = bool.Parse(values[7]);
            JointVeinCount = values.Length > 8 ? int.Parse(values[8]) : 6;
            JointNests = values.Length <= 8 || bool.Parse(values[9]);
            EditorUtility.ClearDirty(this);
        }
    }
}
