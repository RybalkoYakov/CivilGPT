using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Geometry
{
    /// <summary>
    /// Пересчёт координат между ПСК (UCS) и МСК (WCS).
    /// Критично для команд, которые смещают точки «по осям чертежа»:
    /// инженер видит ПСК, а Civil 3D хранит координаты в МСК.
    /// </summary>
    public static class CoordinateSystemHelper
    {
        public static Matrix3d GetUcsToWcs(Editor editor)
        {
            return editor != null ? editor.CurrentUserCoordinateSystem : Matrix3d.Identity;
        }

        public static Matrix3d GetUcsToWcs()
        {
            return GetUcsToWcs(CAD.Common.CadApplication.ActiveEditor);
        }

        /// <summary>Точка из ПСК в МСК.</summary>
        public static Point3d ToWcs(Matrix3d ucsToWcs, Point3d point)
        {
            return point.TransformBy(ucsToWcs);
        }

        /// <summary>Точка из МСК в ПСК.</summary>
        public static Point3d ToUcs(Matrix3d ucsToWcs, Point3d point)
        {
            return point.TransformBy(ucsToWcs.Inverse());
        }

        /// <summary>Вектор из МСК в ПСК (без переноса начала координат).</summary>
        public static Vector3d VectorToUcs(Matrix3d ucsToWcs, Vector3d vector)
        {
            return vector.TransformBy(ucsToWcs.Inverse());
        }
    }
}
