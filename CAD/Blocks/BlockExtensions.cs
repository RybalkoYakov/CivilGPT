using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace CivilGPT.CAD.Blocks
{
    /// <summary>
    /// Расширения для работы с атрибутами блоков.
    /// </summary>
    public static class BlockExtensions
    {
        /// <summary>
        /// Значение атрибута по тегу. Возвращает null, если атрибут отсутствует.
        /// </summary>
        public static string GetAttributeValue(
            this BlockReference block,
            Transaction transaction,
            string tag)
        {
            if (block == null || transaction == null || string.IsNullOrEmpty(tag)) return null;

            foreach (ObjectId id in block.AttributeCollection)
            {
                var attribute = transaction.GetObject(id, OpenMode.ForRead, false) as AttributeReference;
                if (attribute == null) continue;

                if (string.Equals(attribute.Tag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    return attribute.TextString;
                }
            }

            return null;
        }

        /// <summary>Установка значения атрибута по тегу. Возвращает false, если атрибут не найден.</summary>
        public static bool SetAttributeValue(
            this BlockReference block,
            Transaction transaction,
            string tag,
            string value)
        {
            if (block == null || transaction == null || string.IsNullOrEmpty(tag)) return false;

            foreach (ObjectId id in block.AttributeCollection)
            {
                var attribute = transaction.GetObject(id, OpenMode.ForWrite, false) as AttributeReference;
                if (attribute == null) continue;

                if (string.Equals(attribute.Tag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    attribute.TextString = value ?? string.Empty;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Все теги атрибутов блока.</summary>
        public static IReadOnlyList<string> GetAttributeTags(
            this BlockReference block,
            Transaction transaction)
        {
            var tags = new List<string>();
            if (block == null || transaction == null) return tags;

            foreach (ObjectId id in block.AttributeCollection)
            {
                var attribute = transaction.GetObject(id, OpenMode.ForRead, false) as AttributeReference;
                if (attribute != null)
                {
                    tags.Add(attribute.Tag);
                }
            }

            return tags;
        }

        /// <summary>Является ли блок динамическим.</summary>
        public static bool IsDynamic(this BlockReference block)
        {
            return block != null && block.IsDynamicBlock;
        }
    }
}
