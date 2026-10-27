using DarkNights.Entry.Terrain;

namespace DarkNights.Editor.Terrain
{
    /// <summary>CL02大小混合版塌方测试场景的显式打开入口；只打开已保存的原生场景，不创建旧版资源、不重排作者布局，也不自动进入Play。</summary>
    public static class GeologyLabSetup
    {
        public static void Open() => MapWorkbenchScenes.Open(GeologyLabEntry.ScenePath);
    }
}
