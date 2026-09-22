using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>现有 v16.1 点缀算法的独立生成器；其他背景算法实现相同合同即可替换，既有分页器不按具体类型分支。</summary>
    public sealed class ContourBackgroundGenerator : ICaveBackgroundGenerator
    {
        private readonly BackgroundContourSettings settings;
        public ContourBackgroundGenerator(BackgroundContourSettings settings = null)
        { this.settings = settings ?? new BackgroundContourSettings(); }
        public string Identity => settings.Identity;
        public ICaveBackgroundLayout Build(BackgroundBakeDescriptor source, CaveOutlineSettings outline, Action checkpoint = null)
            => BackgroundContourBaker.Build(source, checkpoint, outline, settings);
    }
}
