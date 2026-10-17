namespace DarkNights.Experiments.RichVeinNests
{
    /// <summary>
    /// 富矿伴巢的一次冻结筛选回执，保留设定概率、固定抽样和实际落位／拒绝原因。
    /// 不把数量上限或空间失败当作再次随机的理由，也不在揭露时重做抽样。
    /// </summary>
    public sealed class NestDecision
    {
        public string VeinId { get; }
        public int Tier { get; }
        public double Probability { get; }
        public double Roll { get; }
        public string Reason { get; }

        public NestDecision(VeinInput vein, double probability, double roll, string reason)
        {
            VeinId = vein.Id; Tier = vein.Tier; Probability = probability; Roll = roll; Reason = reason;
        }
    }
}
