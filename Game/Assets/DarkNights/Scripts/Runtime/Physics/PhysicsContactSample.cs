namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 已求解接触的只读诊断值；0表示权威地形，其他身份沿角色／道具合同。
    /// 点为逻辑像素，法线指向本体，冲量沿Unity的kg·单位／秒，仅在显式录制时采集。
    /// </summary>
    public readonly struct PhysicsContactSample
    {
        public int BodyId { get; }
        public int OtherId { get; }
        public float X { get; }
        public float Height { get; }
        public float NormalX { get; }
        public float NormalY { get; }
        public float NormalImpulse { get; }
        public PhysicsContactSample(int body, int other, float x, float height, float nx, float ny, float impulse)
        {
            BodyId = body; OtherId = other; X = x; Height = height;
            NormalX = nx; NormalY = ny; NormalImpulse = impulse;
        }
    }
}
