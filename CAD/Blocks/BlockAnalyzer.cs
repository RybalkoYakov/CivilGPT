using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CivilGPT.CAD.Interfaces;
using Microsoft.VisualBasic;

namespace CivilGPT.CAD.Blocks
{
    /// <summary>
    /// Выполняет анализ блока.
    /// </summary>
    public sealed class BlockAnalyzer : IBlockAnalyzer
    {
        private readonly TransactionService _transactions;

        public BlockAnalyzer(TransactionService transactions)
        {
            _transactions = transactions;
        }

        public BlockInfo Analyze(ObjectId id)
        {
            using var tr = _transactions.Start();

            var block =
                (BlockReference)tr.GetObject(
                    id,
                    OpenMode.ForRead);

            var info = Analyze(block);

            tr.Commit();

            return info;
        }

        public BlockInfo Analyze(BlockReference block)
        {
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

                Extents = block.GeometricExtents
            };
        }
    }
}