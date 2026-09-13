using GameCore.Objects.Behaviours.Interfaces;
using GameCore.Objects.Runner;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// YYGC 运行实体的只读查询能力；索引保存能力引用，不复制任何位置、生命或订单。
    /// EntityId 属于当前会话，DefinitionGuid 和场景放置键分别描述内容与制作来源。
    /// </summary>
    public interface IEntityBehaviour : IBehaviour
    {
        ObjectInstance Object { get; }
        int Id { get; }
        string RuleKey { get; }
        string DefinitionGuid { get; }
        string PlacementKey { get; }
        float X { get; }
    }
}
