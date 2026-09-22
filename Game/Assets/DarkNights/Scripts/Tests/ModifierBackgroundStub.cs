using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Tests
{
    /// <summary>验证替换能力的独立背景生成器；故意不继承 v16.1 生成器，返回三条水平层供同一修饰及分页器消费。</summary>
    public sealed class ModifierBackgroundStub : ICaveBackgroundGenerator, ICaveBackgroundLayout
    {
        public string Identity => "test-bands-1";
        public int Width => 128;
        public int Height => 128;
        public int Top => 0;
        public uint LayoutSeed => 987654;
        public ICaveBackgroundLayout Build(BackgroundBakeDescriptor source, CaveOutlineSettings outline, Action checkpoint = null) => this;
        public bool Solid(int layer, int x, int y) => x >= 0 && x < Width && y >= layer * 20 && y < layer * 20 + 15;
    }
}
