using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Geometry
{
    /// <summary>
    /// Построение матриц преобразования 3D.
    /// </summary>
    public static class MatrixHelper
    {
        public static Matrix3d Identity => Matrix3d.Identity;

        public static Matrix3d Translation(Vector3d delta)
        {
            return Matrix3d.Displacement(delta);
        }

        public static Matrix3d Translation(double x, double y, double z)
        {
            return Matrix3d.Displacement(new Vector3d(x, y, z));
        }

        public static Matrix3d Scaling(Point3d basePoint, double factor)
        {
            return Matrix3d.Scaling(factor, basePoint);
        }

        /// <summary>Поворот вокруг оси Z на угол в радианах.</summary>
        public static Matrix3d Rotation(Point3d basePoint, double angle)
        {
            return Matrix3d.Rotation(angle, Vector3d.ZAxis, basePoint);
        }

        /// <summary>Поворот относительно произвольной оси.</summary>
        public static Matrix3d Rotation(Point3d basePoint, Vector3d axis, double angle)
        {
            Vector3d normalized = axis.GetNormal();
            return Matrix3d.Rotation(angle, normalized, basePoint);
        }

        /// <summary>Зеркальное отражение относительно прямой, заданной двумя точками в плоскости Z=0.</summary>
        public static Matrix3d Mirror(Point3d first, Point3d second)
        {
            return Matrix3d.Mirroring(new Line3d(first, second));
        }

        /// <summary>Комбинация переноса, поворота и масштаба вокруг базовой точки.</summary>
        public static Matrix3d Compose(Point3d basePoint, double rotationAngle, double scale, Vector3d offset)
        {
            Matrix3d matrix = Matrix3d.Displacement(offset);
            matrix = Matrix3d.Scaling(scale, basePoint) * matrix;
            matrix = Matrix3d.Rotation(rotationAngle, Vector3d.ZAxis, basePoint) * matrix;
            return matrix;
        }
    }
}
