using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Windows.Media.Media3D;

namespace CivilGPT.CAD.Blocks
{
    /// <summary>
    /// Полное описание экземпляра блока.
    /// Получается один раз через BlockAnalyzer
    /// и далее используется всеми командами.
    /// </summary>
    public sealed class BlockInfo
    {
        /// <summary>
        /// ObjectId блока.
        /// </summary>
        public ObjectId Id { get; init; }

        /// <summary>
        /// Имя блока.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Является ли блок динамическим.
        /// </summary>
        public bool IsDynamic { get; init; }

        /// <summary>
        /// Точка вставки.
        /// </summary>
        public Point3d Position { get; init; }

        /// <summary>
        /// Угол поворота.
        /// </summary>
        public double Rotation { get; init; }

        /// <summary>
        /// Масштаб по X.
        /// </summary>
        public double ScaleX { get; init; }

        /// <summary>
        /// Масштаб по Y.
        /// </summary>
        public double ScaleY { get; init; }

        /// <summary>
        /// Масштаб по Z.
        /// </summary>
        public double ScaleZ { get; init; }

        /// <summary>
        /// Геометрические границы блока.
        /// </summary>
        public Extents3d Extents { get; init; }

        /// <summary>
        /// Центр геометрических границ.
        /// </summary>
        public Point3d Center =>
            new Point3d(
                (Extents.MinPoint.X + Extents.MaxPoint.X) * 0.5,
                (Extents.MinPoint.Y + Extents.MaxPoint.Y) * 0.5,
                (Extents.MinPoint.Z + Extents.MaxPoint.Z) * 0.5);

        /// <summary>
        /// Ширина блока.
        /// </summary>
        public double Width =>
            Extents.MaxPoint.X - Extents.MinPoint.X;

        /// <summary>
        /// Высота блока.
        /// </summary>
        public double Height =>
            Extents.MaxPoint.Y - Extents.MinPoint.Y;

        /// <summary>
        /// Глубина блока.
        /// </summary>
        public double Depth =>
            Extents.MaxPoint.Z - Extents.MinPoint.Z;
    }
}