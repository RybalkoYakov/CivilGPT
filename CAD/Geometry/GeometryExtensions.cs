using System;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Geometry
{
    /// <summary>
    /// Расширения для точек и векторов.
    /// </summary>
    public static class GeometryExtensions
    {
        /// <summary>Точка с заменённой отметкой (Z).</summary>
        public static Point3d WithElevation(this Point3d point, double elevation)
        {
            return new Point3d(point.X, point.Y, elevation);
        }

        /// <summary>Точка с обнулённой отметкой — для плановых построений.</summary>
        public static Point3d Flatten(this Point3d point)
        {
            return new Point3d(point.X, point.Y, 0.0);
        }

        /// <summary>Проекция на плоскость XY.</summary>
        public static Point2d ToPoint2d(this Point3d point)
        {
            return new Point2d(point.X, point.Y);
        }

        public static Point3d ToPoint3d(this Point2d point)
        {
            return new Point3d(point.X, point.Y, 0.0);
        }

        public static Point3d ToPoint3d(this Point2d point, double elevation)
        {
            return new Point3d(point.X, point.Y, elevation);
        }

        /// <summary>Плановое (горизонтальное) расстояние между точками.</summary>
        public static double PlanDistanceTo(this Point3d point, Point3d other)
        {
            double dx = point.X - other.X;
            double dy = point.Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Плановый азимут от точки до другой точки, радианы.</summary>
        public static double PlanAngleTo(this Point3d point, Point3d other)
        {
            return Math.Atan2(other.Y - point.Y, other.X - point.X);
        }

        public static bool IsPlanEqualTo(this Point3d point, Point3d other, double tolerance)
        {
            return point.PlanDistanceTo(other) <= tolerance
                   && Math.Abs(point.Z - other.Z) <= tolerance;
        }
    }
}
