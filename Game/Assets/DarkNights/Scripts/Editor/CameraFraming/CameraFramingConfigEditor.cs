using DarkNights.View.CameraFraming;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.CameraFraming
{
    /// <summary>
    /// 镜头共享配置的原生Inspector；选择预设填入比例，手动修改比例自动切换为自定义。
    /// 使用SerializedProperty提交成组变更，支持Undo、保存重开及运行期间本地取景调节。
    /// </summary>
    [CustomEditor(typeof(CameraFramingConfig))]
    internal sealed class CameraFramingConfigEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var preset = serializedObject.FindProperty("preset");
            var percent = serializedObject.FindProperty("heroHeightPercent");
            var reference = serializedObject.FindProperty("referenceHeroHeight");
            var presetField = new EnumField("镜头预设", CameraFramingPreset.Screenshot105);
            var percentField = new FloatField("人物占屏高度（%）") { isDelayed = true };
            var referenceField = new FloatField("参考人高（Unity单位）") { isDelayed = true };
            root.Add(presetField);
            root.Add(percentField);
            root.Add(referenceField);
            root.Add(new HelpBox("预设填入起始比例。直接修改人物占屏高度会切换为“自定义”。数值越小，镜头越远。"
                + "参考人高用于校准人物外观尺寸，当前主角为0.48单位。", HelpBoxMessageType.Info));

            void Synchronize()
            {
                serializedObject.Update();
                presetField.SetValueWithoutNotify((CameraFramingPreset)preset.intValue);
                percentField.SetValueWithoutNotify(percent.floatValue);
                referenceField.SetValueWithoutNotify(reference.floatValue);
            }

            presetField.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                var selected = (CameraFramingPreset)evt.newValue;
                preset.intValue = (int)selected;
                if (selected != CameraFramingPreset.Custom)
                    percent.floatValue = CameraFramingConfig.PresetHeightPercent(selected);
                serializedObject.ApplyModifiedProperties();
                Synchronize();
            });
            percentField.RegisterValueChangedCallback(evt =>
            {
                if (float.IsNaN(evt.newValue) || float.IsInfinity(evt.newValue)) { Synchronize(); return; }
                serializedObject.Update();
                percent.floatValue = Mathf.Clamp(evt.newValue, 0.1f, 100);
                preset.intValue = (int)CameraFramingPreset.Custom;
                serializedObject.ApplyModifiedProperties();
                Synchronize();
            });
            referenceField.RegisterValueChangedCallback(evt =>
            {
                if (float.IsNaN(evt.newValue) || float.IsInfinity(evt.newValue)) { Synchronize(); return; }
                serializedObject.Update();
                reference.floatValue = Mathf.Max(0.001f, evt.newValue);
                serializedObject.ApplyModifiedProperties();
                Synchronize();
            });
            root.TrackSerializedObjectValue(serializedObject, _ => Synchronize());
            Synchronize();
            return root;
        }
    }
}
