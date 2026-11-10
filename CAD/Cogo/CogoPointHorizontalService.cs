using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using CivilGPT.Models;

namespace CivilGPT.CAD.Cogo
{
    /// <summary>
    /// Массовое случайное горизонтальное смещение точек COGO в осях текущей ПСК.
    /// </summary>
    public static class CogoPointHorizontalService
    {
        public class Result
        {
            public int Processed { get; set; }

            public int SkippedLocked { get; set; }
        }

        /// <summary>
        /// Применяет случайное смещение X/Y ко всем точкам COGO документа в осях ПСК.
        /// Транзакция должна быть активной; Commit выполняет вызывающий код.
        /// </summary>
        /// <param name="ucsToWcs">Матрица перехода из ПСК в МСК (Editor.CurrentUserCoordinateSystem).</param>
        public static Result RandomizeHorizontal(
            CivilDocument civilDocument,
            Transaction transaction,
            HorizontalOffsetOptions options,
            Matrix3d ucsToWcs)
        {
            if (civilDocument == null) throw new ArgumentNullException(nameof(civilDocument));
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var random = new Random();
            var result = new Result();

            Matrix3d wcsToUcs = ucsToWcs.Inverse();

            foreach (ObjectId pointId in civilDocument.CogoPoints)
            {
                var point = transaction.GetObject(pointId, OpenMode.ForWrite, false) as CogoPoint;
                if (point == null) continue;

                if (point.IsLocked)
                {
                    result.SkippedLocked++;
                    continue;
                }

                // 1. Координаты точки из МСК в ПСК
                var wcsPoint = new Point3d(point.Easting, point.Northing, point.Elevation);
                Point3d ucsPoint = wcsPoint.TransformBy(wcsToUcs);

                // 2. Случайные смещения в осях ПСК
                double dx = options.MinOffsetX
                            + (random.NextDouble() * (options.MaxOffsetX - options.MinOffsetX));
                double dy = options.MinOffsetY
                            + (random.NextDouble() * (options.MaxOffsetY - options.MinOffsetY));

                var shiftedUcs = new Point3d(ucsPoint.X + dx, ucsPoint.Y + dy, ucsPoint.Z);

                // 3. Обратно в МСК
                Point3d shiftedWcs = shiftedUcs.TransformBy(ucsToWcs);

                // 4. Записываем новые координаты, высоту не меняем
                point.Easting = shiftedWcs.X;
                point.Northing = shiftedWcs.Y;

                result.Processed++;
            }

            return result;
        }
    }
}
