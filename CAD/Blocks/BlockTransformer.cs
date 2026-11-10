using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CivilGPT.CAD.Common;
using CivilGPT.CAD.Interfaces;

namespace CivilGPT.CAD.Blocks
{
    /// <summary>
    /// Применение матрицы преобразования к вхождениям блоков.
    /// </summary>
    public class BlockTransformer : IBlockTransformer
    {
        private readonly TransactionService _transactions;

        public BlockTransformer(TransactionService transactions)
        {
            _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        }

        public bool Transform(ObjectId blockId, Matrix3d matrix)
        {
            using (Transaction transaction = _transactions.Start())
            {
                var block = transaction.GetObject(blockId, OpenMode.ForWrite) as BlockReference;
                if (block == null) return false;

                block.TransformBy(matrix);
                transaction.Commit();

                return true;
            }
        }

        public int Transform(ObjectIdCollection blockIds, Matrix3d matrix)
        {
            if (blockIds == null) return 0;

            int count = 0;

            using (Transaction transaction = _transactions.Start())
            {
                foreach (ObjectId id in blockIds)
                {
                    var block = transaction.GetObject(id, OpenMode.ForWrite) as BlockReference;
                    if (block == null) continue;

                    block.TransformBy(matrix);
                    count++;
                }

                if (count > 0)
                {
                    transaction.Commit();
                }
            }

            return count;
        }
    }
}
