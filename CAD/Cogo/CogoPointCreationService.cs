using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using CivilGPT.Models;

namespace CivilGPT.CAD.Cogo
{
    /// <summary>
    /// Создание новых точек COGO со случайным смещением от выбранных
    /// в осях текущей ПСК, с помещением результата в новую группу точек.
    /// </summary>
    public static class CogoPointCreationService
    {
        public class Result
        {
            public int CreatedCount { get; set; }

            public ObjectId GroupId { get; set; }
        }

        /// <summary>
        /// Создаёт новые точки COGO со случайным смещением от исходных
        /// и помещает их в новую группу точек.
        /// </summary>
        /// <param name="ucsToWcs">Матрица перехода из ПСК в МСК (Editor.CurrentUserCoordinateSystem).</param>
        public static Result CreateOffsetPoints(
            CivilDocument civilDocument,
            Transaction transaction,
            List<ObjectId> selectedPoints,
            PointCreationOptions options,
            Matrix3d ucsToWcs)
        {
            if (civilDocument == null) throw new ArgumentNullException(nameof(civilDocument));
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var random = new Random();
            var result = new Result();

            // 1. Создаём новую группу точек
            ObjectId groupId = civilDocument.PointGroups.Add(options.GroupName);
            result.GroupId = groupId;

            var group = transaction.GetObject(groupId, OpenMode.ForWrite) as PointGroup;

            // 2. Готовим обратную матрицу (МСК -> ПСК)
            Matrix3d wcsToUcs = ucsToWcs.Inverse();

            // 3. Создаём новые точки
            CogoPointCollection cogoPoints = civilDocument.CogoPoints;
            var newPointNumbers = new List<uint>();

            if (selectedPoints != null)
            {
                foreach (ObjectId pointId in selectedPoints)
                {
                    var original = transaction.GetObject(pointId, OpenMode.ForRead, false) as CogoPoint;
                    if (original == null) continue;

                    // Случайные смещения в осях ПСК
                    double dx = options.MinOffsetX
                                + (random.NextDouble() * (options.MaxOffsetX - options.MinOffsetX));
                    double dy = options.MinOffsetY
                                + (random.NextDouble() * (options.MaxOffsetY - options.MinOffsetY));
                    double dz = options.MinOffsetZ
                                + (random.NextDouble() * (options.MaxOffsetZ - options.MinOffsetZ));

                    // Координаты исходной точки из МСК в ПСК
                    var wcsPoint = new Point3d(original.Easting, original.Northing, original.Elevation);
                    Point3d ucsPoint = wcsPoint.TransformBy(wcsToUcs);

                    // Применяем смещение в осях ПСК
                    var shiftedUcs = new Point3d(ucsPoint.X + dx, ucsPoint.Y + dy, ucsPoint.Z + dz);

                    // Обратно в МСК
                    Point3d shiftedWcs = shiftedUcs.TransformBy(ucsToWcs);

                    // Создаём точку
                    ObjectId newPointId = cogoPoints.Add(shiftedWcs, false);
                    result.CreatedCount++;

                    // Записываем описание со ссылкой на исходную точку и величинами смещений
                    var newPoint = transaction.GetObject(newPointId, OpenMode.ForWrite, false) as CogoPoint;
                    if (newPoint != null)
                    {
                        newPoint.RawDescription =
                            $"№{original.PointNumber}, \ndx: {dx:F3}, \ndy: {dy:F3}, \ndz: {dz:F3}";

                        newPointNumbers.Add(newPoint.PointNumber);
                    }
                }
            }

            // 4. Настраиваем запрос группы
            if (group != null && newPointNumbers.Count > 0)
            {
                var query = new StandardPointGroupQuery();
                query.IncludeNumbers = BuildNumberList(newPointNumbers);
                group.SetQuery(query);
                group.Update();
            }

            return result;
        }

        /// <summary>
        /// Сжимает список номеров в диапазоны: 1,2,3,7,8 → "1-3,7-8".
        /// </summary>
        private static string BuildNumberList(List<uint> numbers)
        {
            numbers.Sort();

            var builder = new StringBuilder();
            int i = 0;

            while (i < numbers.Count)
            {
                uint start = numbers[i];
                uint end = start;

                while (i + 1 < numbers.Count && numbers[i + 1] == end + 1)
                {
                    end = numbers[i + 1];
                    i++;
                }

                if (builder.Length > 0) builder.Append(',');

                if (start == end)
                {
                    builder.Append(start.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append(start.ToString(CultureInfo.InvariantCulture))
                        .Append('-')
                        .Append(end.ToString(CultureInfo.InvariantCulture));
                }

                i++;
            }

            return builder.ToString();
        }
    }
}
