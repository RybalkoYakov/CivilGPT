using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Geometry
{
    /// <summary>
    /// Преобразование координат и углов.
    /// </summary>
    public static class TransformHelper
    {
        public static Point3d Transform(Point3d point, Matrix3d matrix)
        {
            return point.TransformBy(matrix);
        }

        public static Point3dCollection Transform(Point3dCollection points, Matrix3d matrix)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));

            var result = new Point3dCollection();
            foreach (Point3d point in points)
            {
                result.Add(point.TransformBy(matrix));
            }
            return result;
        }

        public static List<Point3d> Transform(IEnumerable<Point3d> points, Matrix3d matrix)
        {
            var result = new List<Point3d>();
            if (points == null) return result;

            foreach (Point3d point in points)
            {
                result.Add(point.TransformBy(matrix));
            }
            return result;
        }

        /// <summary>Приведение угла к диапазону [0, 2π).</summary>
        public static double NormalizeAngle(double angle)
        {
            double twoPi = Math.PI * 2.0;
            double result = angle % twoPi;
            return result < 0 ? result + twoPi : result;
        }

        public static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }

        public static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        /// <summary>Поворот точки вокруг центра в плоскости XY.</summary>
        public static Point3d RotateAround(Point3d point, Point3d center, double angle)
        {
            return point.TransformBy(Matrix3d.Rotation(angle, Vector3d.ZAxis, center));
        }
    }
}
