namespace DarkNights.Runtime.Diagnostics
{
    /// <summary>
    /// 显式性能验收的客户端接收阶段计时，分别覆盖完整解码、对象准备和展示副本提交。
    /// 不持有投影或业务状态；记录器启用时才创建，重置不会改变连接、Ready 或世界。
    /// </summary>
    public sealed class ClientMeasurements
    {
        public MeasurementSeries DecodeMilliseconds { get; } = new MeasurementSeries();
        public MeasurementSeries ObjectsMilliseconds { get; } = new MeasurementSeries();
        public MeasurementSeries ApplyMilliseconds { get; } = new MeasurementSeries();

        public void Clear()
        {
            DecodeMilliseconds.Clear();
            ObjectsMilliseconds.Clear();
            ApplyMilliseconds.Clear();
        }
    }
}
