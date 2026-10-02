#if UNITY_EDITOR || DEVELOPMENT_BUILD
using DarkNights.Runtime.Objects;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>快速测试的本机选择记忆；仅存稳定测试 ID，项目隔离且不参与正式存档、网络或自动开局。</summary>
    public static class QuickTestSelection
    {
        public static string PreferenceKey => "DarkNights.QuickTest." + Hash128.Compute(Application.dataPath.ToLowerInvariant());
        public static string SelectedId
        {
            get
            {
#if UNITY_EDITOR
                string value = UnityEditor.EditorPrefs.GetString(PreferenceKey, QuickTestPreset.LandedPickaxeId);
#else
                string value = PlayerPrefs.GetString(PreferenceKey, QuickTestPreset.LandedPickaxeId);
#endif
                return value == QuickTestPreset.LandedPickaxeId ? value : QuickTestPreset.LandedPickaxeId;
            }
        }
        public static void Select(string id)
        {
            if (id != QuickTestPreset.LandedPickaxeId) throw new System.ArgumentException("未知快速测试项。", nameof(id));
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetString(PreferenceKey, id);
#else
            PlayerPrefs.SetString(PreferenceKey, id); PlayerPrefs.Save();
#endif
        }
    }
}
#endif
