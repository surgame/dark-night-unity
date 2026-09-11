using System;

namespace DarkNights.View
{
    /// <summary>
    /// 一次本地输入的冻结参数，显式携带选择 ID、目标、坐标和建造种类。
    /// Entry 将其转为统一网络请求，后续选择变化不能更改已发出的意图，也不能在客户端支付。
    /// </summary>
    public sealed class InputIntent
    {
        public string Action { get; }
        private readonly int[] actors;
        public int[] Actors => (int[])actors.Clone();
        public int Target { get; }
        public float X { get; }
        public string Kind { get; }
        public InputIntent(string action, int[] actors, int target = 0, float x = 0, string kind = "")
        {
            Action = action;
            this.actors = actors == null ? Array.Empty<int>() : (int[])actors.Clone();
            Target = target;
            X = x;
            Kind = kind;
        }
    }
}
