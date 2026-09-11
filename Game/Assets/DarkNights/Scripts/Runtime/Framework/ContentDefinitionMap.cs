using System;
using System.Collections.Generic;
using GameCore.Objects.Behaviours.Interfaces;
using GameCore.Objects.Definition;
using UnityEngine;

namespace DarkNights.Runtime.Framework
{
    /// <summary>
    /// 作为 ObjectDefinition 共享配置保存规则内容到本地对象定义的映射。
    /// 运行时只读解析，不复制 HP、成本或实例进度；重复 ContentId、空引用和未知定义均视为内容错误。
    /// </summary>
    [Serializable]
    public sealed class ContentDefinitionMap : IConfigData
    {
        [SerializeField] private List<ContentDefinitionEntry> entries = new List<ContentDefinitionEntry>();

        public string Name => "Dark Nights 内容定义映射";
        public IReadOnlyList<ContentDefinitionEntry> Entries => entries.AsReadOnly();

        public ContentDefinitionMap()
        {
        }

        public ContentDefinitionMap(IEnumerable<ContentDefinitionEntry> entries)
        {
            this.entries = new List<ContentDefinitionEntry>(entries ?? Array.Empty<ContentDefinitionEntry>());
        }

        public ObjectDefinition GetRequired(string contentId, ObjectDefinitionDatabase database = null)
        {
            if (string.IsNullOrWhiteSpace(contentId))
                throw new ArgumentException("ContentId cannot be empty.", nameof(contentId));
            ObjectDefinition result = null;
            foreach (ContentDefinitionEntry entry in entries)
            {
                if (entry == null || !string.Equals(entry.ContentId, contentId, StringComparison.Ordinal)) continue;
                if (result != null) throw new InvalidOperationException("Duplicate ContentId mapping: " + contentId);
                result = entry.Definition.Resolve(database);
            }
            return result ?? throw new KeyNotFoundException("Unknown or unresolved ContentId mapping: " + contentId);
        }

        public void Validate(ObjectDefinitionDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            var contentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ContentDefinitionEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ContentId) || !contentIds.Add(entry.ContentId))
                    throw new InvalidOperationException("Content definition map contains an empty or duplicate ContentId.");
                if (entry.Definition.IsEmpty || entry.Definition.Resolve(database) == null)
                    throw new InvalidOperationException("Content definition map has an unresolved definition: " + entry.ContentId);
            }
        }
    }
}
