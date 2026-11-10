using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Geometry
{
    /// <summary>
    /// Работа с габаритами (Extents3d): объединение, расширение, центр, углы.
    /// Extents3d — структура, поэтому вместо null используется флаг наличия значения.
    /// </summary>
    public static class ExtentsHelper
    {
        public static Point3d Center(Extents3d extents)
        {
            return new Point3d(
                (extents.MinPoint.X + extents.MaxPoint.X) * 0.5,
                (extents.MinPoint.Y + extents.MaxPoint.Y) * 0.5,
                (extents.MinPoint.Z + extents.MaxPoint.Z) * 0.5);
        }

        public static Extents3d ShrinkToPoint(Point3d point)
        {
            return new Extents3d(point, point);
        }

        public static Extents3d Expand(Extents3d extents, double delta)
        {
            var min = new Point3d(
                extents.MinPoint.X - delta,
                extents.MinPoint.Y - delta,
                extents.MinPoint.Z - delta);

            var max = new Point3d(
                extents.MaxPoint.X + delta,
                extents.MaxPoint.Y + delta,
                extents.MaxPoint.Z + delta);

            return new Extents3d(min, max);
        }

        /// <summary>
        /// Объединяет набор габаритов. Если набор пуст, hasValue = false.
        /// </summary>
        public static Extents3d Union(IEnumerable<Extents3d> items, out bool hasValue)
        {
            Extents3d result = default(Extents3d);
            hasValue = false;

            if (items == null) return result;

            foreach (Extents3d item in items)
            {
                if (!hasValue)
                {
                    result = new Extents3d(item.MinPoint, item.MaxPoint);
                    hasValue = true;
                }
                else
                {
                    result.AddExtents(item);
                }
            }

            return result;
        }

        /// <summary>
        /// Безопасное получение габаритов объекта: у некоторых объектов
        /// (пустые блоки, вырожденные полилинии) GeometricExtents выбрасывает исключение.
        /// </summary>
        public static bool TryGetExtents(Entity entity, out Extents3d extents)
        {
            extents = default(Extents3d);

            if (entity == null) return false;

            try
            {
                extents = entity.GeometricExtents;
                return true;
            }
            catch (System.Exception)
            {
                extents = default(Extents3d);
                return false;
            }
        }

        /// <summary>Восемь угловых точек габаритной рамки.</summary>
        public static Point3dCollection GetCorners(Extents3d extents)
        {
            var corners = new Point3dCollection();
            Point3d min = extents.MinPoint;
            Point3d max = extents.MaxPoint;

            for (int i = 0; i < 8; i++)
            {
                double x = (i & 1) == 0 ? min.X : max.X;
                double y = (i & 2) == 0 ? min.Y : max.Y;
                double z = (i & 4) == 0 ? min.Z : max.Z;
                corners.Add(new Point3d(x, y, z));
            }

            return corners;
        }

        /// <summary>Содержит ли рамка точку (с допуском).</summary>
        public static bool Contains(Extents3d extents, Point3d point, double tolerance = 1e-9)
        {
            return point.X >= extents.MinPoint.X - tolerance && point.X <= extents.MaxPoint.X + tolerance
                && point.Y >= extents.MinPoint.Y - tolerance && point.Y <= extents.MaxPoint.Y + tolerance
                && point.Z >= extents.MinPoint.Z - tolerance && point.Z <= extents.MaxPoint.Z + tolerance;
        }
    }
}
