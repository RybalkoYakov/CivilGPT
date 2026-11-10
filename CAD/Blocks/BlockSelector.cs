using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace CivilGPT.CAD.Blocks
{
    /// <summary>
    /// Поиск вхождений блоков в пространстве чертежа по имени.
    /// </summary>
    public class BlockSelector
    {
        private readonly Func<Transaction> _startTransaction;

        public BlockSelector(Func<Transaction> startTransaction)
        {
            _startTransaction = startTransaction ?? throw new ArgumentNullException(nameof(startTransaction));
        }

        /// <summary>
        /// Найти вхождения блоков в указанном пространстве.
        /// </summary>
        /// <param name="spaceId">ObjectId пространства (модель или лист).</param>
        /// <param name="namePattern">Имя блока или его часть. null — любые блоки.</param>
        /// <param name="exactMatch">true — точное совпадение имени (без учёта регистра).</param>
        public IReadOnlyList<ObjectId> Find(ObjectId spaceId, string namePattern = null, bool exactMatch = false)
        {
            var result = new List<ObjectId>();

            using (Transaction transaction = _startTransaction())
            {
                var space = transaction.GetObject(spaceId, OpenMode.ForRead) as BlockTableRecord;
                if (space == null) return result;

                foreach (ObjectId id in space)
                {
                    var block = transaction.GetObject(id, OpenMode.ForRead, false) as BlockReference;
                    if (block == null) continue;

                    if (Matches(transaction, block, namePattern, exactMatch))
                    {
                        result.Add(id);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Только динамические блоки.
        /// </summary>
        public IReadOnlyList<ObjectId> FindDynamic(ObjectId spaceId)
        {
            var result = new List<ObjectId>();

            using (Transaction transaction = _startTransaction())
            {
                var space = transaction.GetObject(spaceId, OpenMode.ForRead) as BlockTableRecord;
                if (space == null) return result;

                foreach (ObjectId id in space)
                {
                    var block = transaction.GetObject(id, OpenMode.ForRead, false) as BlockReference;
                    if (block != null && block.IsDynamicBlock)
                    {
                        result.Add(id);
                    }
                }
            }

            return result;
        }

        private static bool Matches(
            Transaction transaction,
            BlockReference block,
            string namePattern,
            bool exactMatch)
        {
            if (string.IsNullOrWhiteSpace(namePattern)) return true;

            string effectiveName = block.Name ?? string.Empty;

            if (exactMatch && string.Equals(effectiveName, namePattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!exactMatch && effectiveName.IndexOf(namePattern, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // Для динамических блоков эффективное имя анонимное (*U12),
            // поэтому дополнительно проверяем имя определения блока.
            if (block.IsDynamicBlock)
            {
                var definition = transaction.GetObject(
                    block.DynamicBlockTableRecord, OpenMode.ForRead, false) as BlockTableRecord;

                if (definition != null)
                {
                    string definitionName = definition.Name ?? string.Empty;

                    return exactMatch
                        ? string.Equals(definitionName, namePattern, StringComparison.OrdinalIgnoreCase)
                        : definitionName.IndexOf(namePattern, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }

            return false;
        }
    }
}
