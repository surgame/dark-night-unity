using DarkNights.Entry;
using DarkNights.View.Lighting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Lighting
{
    /// <summary>正式 Play 手电的 UI Toolkit 调试窗口；只修改当前客户端表现，不改变装备权威、工具定义或存档。</summary>
    public sealed class FlashlightDebugWindow : EditorWindow
    {
        private const string Root = "Assets/DarkNights/Res/Shared/Lighting/Debug/";
        private HeroLightPresentation target;
        private Label status;
        [MenuItem("Dark Nights/Debug/手电光照调试")]
        public static void Open()
        { var window=GetWindow<FlashlightDebugWindow>("手电光照"); window.minSize=new Vector2(470,560); }

        public void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root+"FlashlightDebug.uxml");
            tree.CloneTree(rootVisualElement);
            status = rootVisualElement.Q<Label>("status");
            rootVisualElement.Q<Toggle>("soft").RegisterValueChangedCallback(e => { if (Settings != null) Settings.SoftShadows = e.newValue; });
            Bind("ambient",()=>Settings.Ambient,v=>Settings.Ambient=v);
            Bind("softness",()=>Settings.Softness,v=>Settings.Softness=v);
            Bind("coneFeather",()=>Settings.ConeFeather,v=>Settings.ConeFeather=v);
            Bind("wallDepth",()=>Settings.WallDepth,v=>Settings.WallDepth=v);
            Bind("wallStrength",()=>Settings.WallStrength,v=>Settings.WallStrength=v);
            Bind("near",()=>Settings.NearStrength,v=>Settings.NearStrength=v);
            Bind("bounce",()=>Settings.Bounce,v=>Settings.Bounce=v);
            Bind("relief",()=>Settings.Relief,v=>Settings.Relief=v);
            Bind("range",()=>Settings.RangeScale,v=>Settings.RangeScale=v);
            Bind("cone",()=>Settings.ConeOffset,v=>Settings.ConeOffset=v);
            Bind("intensity",()=>Settings.IntensityScale,v=>Settings.IntensityScale=v);
            rootVisualElement.Q<Button>("bright").clicked += ()=>Preset(.32f);
            rootVisualElement.Q<Button>("balanced").clicked += ()=>Preset(.24f);
            rootVisualElement.Q<Button>("dark").clicked += ()=>Preset(.10f);
            rootVisualElement.Q<Button>("zero").clicked += ()=>Preset(0);
            rootVisualElement.schedule.Execute(Refresh).Every(500);
        }

        private ExplorationLightSettings Settings => target != null ? target.Settings : null;
        private void Preset(float ambient) { if (Settings != null) { Settings.Ambient=ambient; Refresh(); } }
        private void Bind(string name,System.Func<float> read,System.Action<float> write)
        {
            rootVisualElement.Q<Slider>(name).RegisterValueChangedCallback(e=> { if (Settings != null) write(e.newValue); });
        }
        private void Refresh()
        {
            if (target == null && EditorApplication.isPlaying) target=UnityEngine.Object.FindAnyObjectByType<HeroLightPresentation>();
            var settings=Settings;
            status.text=settings==null ? "进入正式游戏并控制主角后可调参。F 开关手电。" : "正式光源："+target.SourceCount+"　F 开关手电；墙内上限 1 格 = 8 原生岩壁像素。";
            rootVisualElement.Q<VisualElement>("controls").SetEnabled(settings!=null);
            if (settings==null) return;
            rootVisualElement.Q<Toggle>("soft").SetValueWithoutNotify(settings.SoftShadows);
            Set("ambient",settings.Ambient); Set("softness",settings.Softness); Set("coneFeather",settings.ConeFeather);
            Set("wallDepth",settings.WallDepth); Set("wallStrength",settings.WallStrength); Set("near",settings.NearStrength);
            Set("bounce",settings.Bounce); Set("relief",settings.Relief);
            Set("range",settings.RangeScale); Set("cone",settings.ConeOffset); Set("intensity",settings.IntensityScale);
        }
        private void Set(string name,float value)=>rootVisualElement.Q<Slider>(name).SetValueWithoutNotify(value);
    }
}
