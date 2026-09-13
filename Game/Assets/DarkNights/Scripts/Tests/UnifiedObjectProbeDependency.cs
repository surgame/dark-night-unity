namespace DarkNights.Tests
{
    /// <summary>会话容器拥有的测试依赖；换会话后注入必须指向新实例，不能继续解析旧容器。</summary>
    public sealed class UnifiedObjectProbeDependency
    {
        public int Number { get; }
        public UnifiedObjectProbeDependency(int number) { Number = number; }
    }
}
