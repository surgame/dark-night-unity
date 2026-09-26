using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>运行时工作台的样式草稿；源资产只读，共享 Modifier 保持同一草稿，应用仅建立本次运行的取消基线。</summary>
    public sealed class CaveStyleDraft : IDisposable
    {
        private readonly Dictionary<ScriptableObject, CaveStyleDraftItem> items = new Dictionary<ScriptableObject, CaveStyleDraftItem>();
        public CaveTerrainStyle Source { get; }
        public IEnumerable<CaveStyleDraftItem> Items => items.Values;
        public CaveTerrainStyle Style => Get(Source);
        public CaveBackgroundStyle Background => Get(Style?.Background);
        public bool HasChanges
        {
            get
            {
                foreach (var item in items.Values)
                    if (JsonUtility.ToJson(item.Working) != JsonUtility.ToJson(item.Baseline)) return true;
                return false;
            }
        }
        public CaveStyleDraft(CaveTerrainStyle source)
        {
            Source = source; Get(source);
            if (Style == null) return;
            Register(Style.Modifiers);
            if (Background == null) return;
            Get(Background.Generator); Register(Background.NearModifiers);
            Register(Background.MiddleModifiers); Register(Background.DeepModifiers);
        }
        private void Register(CaveModifierAsset[] sources)
        { if (sources != null) foreach (var source in sources) Get(source); }
        public T Get<T>(T source) where T : ScriptableObject
        {
            if (source == null) return null;
            if (!items.TryGetValue(source, out var item))
            {
                item = new CaveStyleDraftItem { Source = source, Working = Copy(source), Baseline = Copy(source), Original = Copy(source) };
                items.Add(source, item);
            }
            return (T)item.Working;
        }
        public void ResetField(ScriptableObject source, FieldInfo field)
        {
            Get(source); var item = items[source];
            object value = field.GetValue(item.Baseline);
            field.SetValue(item.Working, value is Array array ? array.Clone() : value);
        }
        public bool FieldChanged(ScriptableObject source, FieldInfo field)
        { Get(source); return !Equals(field.GetValue(items[source].Working), field.GetValue(items[source].Baseline)); }
        public void Apply()
        {
            foreach (var item in items.Values)
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(item.Working), item.Baseline);
        }
        public void Cancel()
        {
            foreach (var item in items.Values)
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(item.Baseline), item.Working);
        }
        public CaveTerrainStyle Capture(out CaveBackgroundStyle background)
        {
            background = null;
            if (Style == null) return null;
            var result = (CaveTerrainStyle)Copy(Style);
            result.Modifiers = CaptureArray(Style.Modifiers);
            if (Background != null)
            {
                background = (CaveBackgroundStyle)Copy(Background);
                background.Generator = Get(Background.Generator);
                background.NearModifiers = CaptureArray(Background.NearModifiers);
                background.MiddleModifiers = CaptureArray(Background.MiddleModifiers);
                background.DeepModifiers = CaptureArray(Background.DeepModifiers);
                result.Background = background;
            }
            return result;
        }
        private CaveModifierAsset[] CaptureArray(CaveModifierAsset[] sources)
        {
            var result = new CaveModifierAsset[sources?.Length ?? 0];
            for (int i = 0; i < result.Length; i++) result[i] = Get(sources[i]);
            return result;
        }
        private static ScriptableObject Copy(ScriptableObject source)
        { var result = UnityEngine.Object.Instantiate(source); result.name = source.name; result.hideFlags = HideFlags.HideAndDontSave; return result; }
        public static void Release(UnityEngine.Object value)
        { if (value == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
        public void Dispose()
        {
            foreach (var item in items.Values) { Release(item.Working); Release(item.Baseline); Release(item.Original); }
            items.Clear();
        }
    }
}
