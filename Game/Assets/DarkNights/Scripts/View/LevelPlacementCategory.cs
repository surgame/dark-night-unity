namespace DarkNights.View
{
    /// <summary>
    /// 区分场景中的初始建筑、资源点和友方单位标记；分类只用于布局导出和编辑器着色，不成为运行时实体身份。
    /// </summary>
    public enum LevelPlacementCategory
    {
        Building,
        Worksite,
        Actor
    }
}
