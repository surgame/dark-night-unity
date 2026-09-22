using DarkNights.Core.Logic.Terrain;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>独立点缀生成器的资产入口；子类只在主线程冻结设置，纯算法通过三层布局接口交给已有着色、缓存及生命周期。</summary>
    public abstract class CaveBackgroundGeneratorAsset : ScriptableObject
    {
        public abstract ICaveBackgroundGenerator Capture();
    }
}
