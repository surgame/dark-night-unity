using System;
using System.Collections.Generic;
using DarkNights.View;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 将经离线校验的逐帧轨道一次性写入原生 AnimationClip；只在显式首版安装中调用。
    /// 精灵与可见性保持离散采样，偏移转换到 Y 向上的米坐标，不生成事件或伤害回调。
    /// </summary>
    public static class NativeAnimationBuilder
    {
        public static PoseClip[] Create(JArray input, string folder)
        {
            var result = new List<PoseClip>();
            foreach (JObject source in input)
            {
                string name = (string)source["name"];
                var clip = new AnimationClip { name = name, frameRate = 60, legacy = false };
                foreach (JObject track in source["tracks"])
                {
                    string path = (string)track["path"] + "/Sprite";
                    string property = (string)track["property"];
                    JArray times = (JArray)track["times"];
                    JArray values = (JArray)track["values"];
                    if (property == "texture")
                    {
                        var keys = new ObjectReferenceKeyframe[times.Count];
                        for (int i = 0; i < times.Count; i++)
                            keys[i] = new ObjectReferenceKeyframe { time = (float)times[i], value = Sprite((string)values[i]) };
                        AnimationUtility.SetObjectReferenceCurve(clip,
                            EditorCurveBinding.PPtrCurve(path, typeof(SpriteRenderer), "m_Sprite"), keys);
                    }
                    else if (property == "visible")
                        SetCurve(clip, path, typeof(SpriteRenderer), "m_Enabled", times, i => (bool)values[i] ? 1 : 0);
                    else if (property == "offset")
                    {
                        SetCurve(clip, path, typeof(Transform), "m_LocalPosition.x", times, i => (float)values[i][0] / 100);
                        SetCurve(clip, path, typeof(Transform), "m_LocalPosition.y", times, i => -(float)values[i][1] / 100);
                    }
                    else throw new InvalidOperationException("Unsupported visual track: " + property);
                }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = (bool)source["loop"];
                settings.stopTime = (float)source["duration"];
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, folder + "/" + name + ".anim");
                result.Add(new PoseClip { Name = name, Clip = clip, Duration = (double)source["duration"], Loop = (bool)source["loop"] });
            }
            return result.ToArray();
        }

        public static Sprite Sprite(string source)
        {
            string path = source.Replace("res://assets/", "Assets/DarkNights/Res/Art/Original/");
            Sprite result = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return result != null ? result : throw new InvalidOperationException("Missing imported sprite: " + path);
        }

        private static void SetCurve(AnimationClip clip, string path, Type type, string property,
            JArray times, Func<int, float> value)
        {
            var keys = new Keyframe[times.Count];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new Keyframe((float)times[i], value(i), float.PositiveInfinity, float.PositiveInfinity);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), new AnimationCurve(keys));
        }
    }
}
