using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CivilGPT.CAD.Common;
using CivilGPT.CAD.Geometry;
using CivilGPT.CAD.Interfaces;

namespace CivilGPT.CAD.Blocks
{
    /// <summary>
    /// Выполняет анализ блока: имя, положение, поворот, масштабы, габариты.
    /// </summary>
    public sealed class BlockAnalyzer : IBlockAnalyzer
    {
        private readonly TransactionService _transactions;

        public BlockAnalyzer(TransactionService transactions)
        {
            _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        }

        public BlockInfo Analyze(ObjectId id)
        {
            using (Transaction transaction = _transactions.Start())
            {
                var block = transaction.GetObject(id, OpenMode.ForRead) as BlockReference;
                if (block == null) return null;

                BlockInfo info = Analyze(block);
                transaction.Commit();

                return info;
            }
        }

        public BlockInfo Analyze(BlockReference block)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));

            Extents3d extents;
            if (!ExtentsHelper.TryGetExtents(block, out extents))
            {
                extents = ExtentsHelper.ShrinkToPoint(block.Position);
            }

            return new BlockInfo
            {
                Id = block.ObjectId,
                Name = block.Name,
                Position = block.Position,
                Rotation = block.Rotation,
                ScaleX = block.ScaleFactors.X,
                ScaleY = block.ScaleFactors.Y,
                ScaleZ = block.ScaleFactors.Z,
                IsDynamic = block.IsDynamicBlock,
                Extents = extents
            };
        }
    }
}
