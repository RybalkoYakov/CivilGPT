using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace CivilGPT.CAD.Blocks
{
    /// <summary>
    /// Чтение и изменение свойств динамических блоков
    /// (например, марка бетона, диаметр арматуры, отметка в блоке-марке).
    /// </summary>
    public static class DynamicBlockHelper
    {
        /// <summary>
        /// Все свойства динамического блока. Многозначные свойства возвращаются первым значением.
        /// </summary>
        public static IReadOnlyDictionary<string, object> GetProperties(BlockReference block)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            if (block == null || !block.IsDynamicBlock) return result;

            foreach (DynamicBlockReferenceProperty property in block.DynamicBlockReferencePropertyCollection)
            {
                object value = property.Value;

                var values = value as object[];
                if (values != null && values.Length > 0)
                {
                    value = values.GetValue(0);
                }

                result[property.PropertyName] = value;
            }

            return result;
        }

        /// <summary>
        /// Установка свойства динамического блока по имени.
        /// Блок должен быть открыт на запись. Возвращает false, если свойство не найдено.
        /// </summary>
        public static bool SetProperty(BlockReference block, string propertyName, object value)
        {
            if (block == null || string.IsNullOrEmpty(propertyName)) return false;
            if (!block.IsDynamicBlock) return false;

            foreach (DynamicBlockReferenceProperty property in block.DynamicBlockReferencePropertyCollection)
            {
                if (!string.Equals(property.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!property.ReadOnly)
                {
                    // Многозначные свойства принимают массив, даже если значение одно.
                    var existing = property.Value as object[];
                    property.Value = existing != null
                        ? new[] { value }
                        : value;
                }

                return true;
            }

            return false;
        }

        /// <summary>Имена всех доступных свойств динамического блока.</summary>
        public static IReadOnlyList<string> GetPropertyNames(BlockReference block)
        {
            var names = new List<string>();

            if (block == null || !block.IsDynamicBlock) return names;

            foreach (DynamicBlockReferenceProperty property in block.DynamicBlockReferencePropertyCollection)
            {
                names.Add(property.PropertyName);
            }

            return names;
        }
    }
}
