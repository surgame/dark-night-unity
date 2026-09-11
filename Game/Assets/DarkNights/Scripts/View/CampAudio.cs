using System;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 原生音乐和音效 Prefab 的显式音频绑定，音量及命中冷却沿用原 AudioPresenter。
    /// 音频由本地表现生命周期拥有，暂停模拟不暂停声音，静音由本地菜单控制。
    /// </summary>
    public sealed class CampAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource music;
        [SerializeField] private AudioSource effects;
        [SerializeField] private string[] keys;
        [SerializeField] private AudioClip[] clips;
        private double cooldown;
        public void Play(string key, float decibels)
        {
            if (AudioListener.pause || (Time.unscaledTimeAsDouble < cooldown && key.Contains("hit"))) return;
            int index = Array.IndexOf(keys, key);
            if (index < 0) throw new InvalidOperationException("Missing original sound binding: " + key);
            effects.PlayOneShot(clips[index], Mathf.Pow(10, decibels / 20));
            cooldown = Time.unscaledTimeAsDouble + 0.06;
        }
        private void OnEnable() { if (!AudioListener.pause) music.Play(); }
        private void OnDisable() { music.Stop(); effects.Stop(); }
    }
}
