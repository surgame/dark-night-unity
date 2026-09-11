using System;
using GameCore.Objects.Definition;
using UnityEngine;

namespace DarkNights.Runtime.Framework
{
    /// <summary>
    /// 保存一个规则 ContentId 到 YYGC DefinitionReference 的稳定映射项。
    /// ContentId 属于权威规则目录，DefinitionReference 只负责定位表现对象定义，两种身份不得互相替代。
    /// </summary>
    [Serializable]
    public sealed class ContentDefinitionEntry
    {
        [SerializeField] private string contentId;
        [SerializeField] private DefinitionReference definition;

        public string ContentId => contentId;
        public DefinitionReference Definition => definition;

        public ContentDefinitionEntry(string contentId, DefinitionReference definition)
        {
            this.contentId = contentId;
            this.definition = definition;
        }
    }
}
