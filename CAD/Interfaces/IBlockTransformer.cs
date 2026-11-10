using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Interfaces
{
    /// <summary>
    /// Преобразование вхождений блоков (перемещение, поворот, масштаб, зеркало).
    /// </summary>
    public interface IBlockTransformer
    {
        /// <summary>Применить матрицу преобразования к одному вхождению блока.</summary>
        bool Transform(ObjectId blockId, Matrix3d matrix);

        /// <summary>Применить матрицу преобразования к набору вхождений блоков.</summary>
        int Transform(ObjectIdCollection blockIds, Matrix3d matrix);
    }
}
