using System.Collections.Generic;
using System.Linq;
using DarkNights.View;
using DarkNights.View.Lighting;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Lighting
{
    /// <summary>调参台来源与运行实例控件；预设切换保留临时覆盖，Definition／实例或场景切换只询问对应范围的未保存草稿。</summary>
    public sealed class FlashlightDebugSources
    {
        private readonly FlashlightDebugWindow window;
        private readonly ObjectField owner, preset, scene, instance;
        private readonly PopupField<string> route;
        private readonly Label hint;
        public FlashlightDebugSources(FlashlightDebugWindow window, VisualElement root)
        {
            this.window = window;
            owner = root.Q<ObjectField>("definition"); owner.objectType = typeof(ObjectDefinition); owner.allowSceneObjects = false;
            preset = root.Q<ObjectField>("profile"); preset.objectType = typeof(LightProfile); preset.allowSceneObjects = false;
            scene = root.Q<ObjectField>("sceneProfile"); scene.objectType = typeof(SceneLightingProfile); scene.allowSceneObjects = false;
            instance = root.Q<ObjectField>("runtimeTool"); instance.objectType = typeof(FlashlightView); instance.allowSceneObjects = true;
            owner.RegisterValueChangedCallback(evt => { window.ChangeDefinition(evt.newValue as ObjectDefinition); Refresh(); });
            preset.RegisterValueChangedCallback(evt => { window.ChangePreset(evt.newValue as LightProfile); Refresh(); });
            scene.RegisterValueChangedCallback(evt => { window.ChangeScene(evt.newValue as SceneLightingProfile); Refresh(); });
            instance.RegisterValueChangedCallback(evt => { window.ChangeRuntime(evt.newValue as FlashlightView); Refresh(); });
            route = new PopupField<string>("单灯保存目标", new List<string> { "当前物体 Definition", "共享预设 LightProfile" }, window.SharedTarget ? 1 : 0);
            route.RegisterValueChangedCallback(evt => { window.SharedTarget = evt.newValue == route.choices[1]; Refresh(); });
            root.Q("saveTarget").Add(route); hint = root.Q<Label>("saveTargetHint");
            root.Q<Button>("chooseRuntime").clicked += ChooseRuntime; Refresh();
        }
        public void Refresh()
        {
            owner.SetValueWithoutNotify(window.DefinitionSource); preset.SetValueWithoutNotify(window.PresetSource);
            scene.SetValueWithoutNotify(window.SceneSource); instance.SetValueWithoutNotify(window.RuntimeSource);
            route.SetValueWithoutNotify(route.choices[window.SharedTarget ? 1 : 0]);
            hint.text = window.SharedTarget ? "影响所有预设继承者；Definition 已覆盖项仍优先。无预设时须先选择目标。" : "只写当前 Definition 的本轮覆盖；场景、灯口和调试记录仍一起保存。";
        }
        public static bool Valid(FlashlightView tool) => Application.isPlaying && tool != null && tool.isActiveAndEnabled &&
            !EditorUtility.IsPersistent(tool) && tool.Owner != null && tool.BaseLightProfile != null;
        private void ChooseRuntime()
        {
            var menu = new GenericMenu(); menu.AddItem(new GUIContent("独立预览（不绑定运行实例）"), window.RuntimeSource == null, () => window.ChangeRuntime(null));
            foreach (var tool in UnityEngine.Object.FindObjectsByType<FlashlightView>().Where(Valid))
            { var captured = tool; menu.AddItem(new GUIContent(tool.Owner.Definition.Name + " · " + tool.Owner.InstanceId), tool == window.RuntimeSource, () => window.ChangeRuntime(captured)); }
            menu.ShowAsContext();
        }
    }
}
