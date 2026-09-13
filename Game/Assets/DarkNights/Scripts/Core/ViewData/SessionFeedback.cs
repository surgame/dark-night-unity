using System;
using System.Collections.Generic;
using System.Linq;
namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 每局显式连接的反馈出口，转达消息、横幅、声音和一次性视觉请求。没有全局订阅或规则处理；Runtime 适配者负责在会话结束时解绑。
    /// </summary>
    public sealed class SessionFeedback
    {
        public string LastMessage { get; private set; } = "";
        public event Action<string, bool> Message;
        public event Action<string, string> Banner;
        public event Action<string, float> Sound;
        public event Action<VisualCue> Effect;

        public void Notify(string message, bool warning = false)
        {
            LastMessage = message;
            Message?.Invoke(message, warning);
        }

        public void ShowBanner(string title, string detail) => Banner?.Invoke(title, detail);

        public void PlaySound(string id, float volume = -11) => Sound?.Invoke(id, volume);

        public void Emit(VisualCue cue) => Effect?.Invoke(cue);

        public void Reset() => LastMessage = "";
    }
}
