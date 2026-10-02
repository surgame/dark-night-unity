using System;
using GameCore.Objects.Definition;

namespace DarkNights.Editor
{
    /// <summary>原生 Definition 浏览器的窗口视图偏好；Unity 序列化作者资产引用和查询，域重载后恢复同一编辑目标。</summary>
    [Serializable]
    internal sealed class DarkNightsDefinitionBrowserState
    {
        public ObjectDefinition Selected;
        public string Query = "";
    }
}
