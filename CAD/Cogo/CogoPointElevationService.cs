using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using CivilGPT.Models;

namespace CivilGPT.CAD.Cogo
{
    /// <summary>
    /// Применение случайного вертикального отклонения к отметкам точек COGO.
    /// Используется для имитации погрешности измерений относительно проектных значений.
    /// </summary>
    public static class CogoPointElevationService
    {
        public class Result
        {
            public int Processed { get; set; }

            public int SkippedLocked { get; set; }

            public int SkippedNotCogo { get; set; }

            public double MinAppliedOffset { get; set; }

            public double MaxAppliedOffset { get; set; }
        }

        /// <summary>
        /// Применяет случайное смещение к высотам указанных точек COGO.
        /// Транзакция должна быть активной; Commit выполняет вызывающий код.
        /// </summary>
        public static Result RandomizeElevation(
            Transaction transaction,
            IReadOnlyList<ObjectId> pointIds,
            ElevationOffsetOptions options)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var random = new Random();
            var result = new Result
            {
                MinAppliedOffset = double.MaxValue,
                MaxAppliedOffset = double.MinValue
            };

            if (pointIds == null) return Normalize(result);

            foreach (ObjectId pointId in pointIds)
            {
                var point = transaction.GetObject(pointId, OpenMode.ForWrite, false) as CogoPoint;

                if (point == null)
                {
                    result.SkippedNotCogo++;
                    continue;
                }

                if (point.IsLocked)
                {
                    result.SkippedLocked++;
                    continue;
                }

                double offset = options.MinOffset
                                + (random.NextDouble() * (options.MaxOffset - options.MinOffset));

                point.Elevation += offset;
                result.Processed++;

                if (offset < result.MinAppliedOffset) result.MinAppliedOffset = offset;
                if (offset > result.MaxAppliedOffset) result.MaxAppliedOffset = offset;
            }

            return Normalize(result);
        }

        private static Result Normalize(Result result)
        {
            if (result.Processed == 0)
            {
                result.MinAppliedOffset = 0.0;
                result.MaxAppliedOffset = 0.0;
            }

            return result;
        }
    }
}
