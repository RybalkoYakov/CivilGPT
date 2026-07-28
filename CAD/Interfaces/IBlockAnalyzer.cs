using Autodesk.AutoCAD.DatabaseServices;
using CivilGPT.CAD.Blocks;

namespace CivilGPT.CAD.Interfaces
{
    /// <summary>
    /// Выполняет анализ экземпляра блока.
    /// </summary>
    public interface IBlockAnalyzer
    {
        BlockInfo Analyze(ObjectId id);

        BlockInfo Analyze(BlockReference block);
    }
}