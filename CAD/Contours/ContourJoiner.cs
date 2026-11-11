using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Contours
{
    /// <summary>
    /// Сшивка отрезков в цельные горизонтали.
    ///
    /// Алгоритм: берём необработанный отрезок, ищем ближайший конец другого
    /// необработанного отрезка в пределах допуска, присоединяем и продолжаем
    /// с нового конца. То же самое в обратную сторону. Когда продолжения нет —
    /// цепочка собрана, оба отрезка исключаются из необработанных.
    /// </summary>
    public static class ContourJoiner
    {
        /// <summary>
        /// Собрать цепочки.
        /// </summary>
        /// <param name="curves">Отрезки-горизонтали.</param>
        /// <param name="maxGap">Максимальное расстояние между концами, м.</param>
        public static List<ContourChain> Build(IReadOnlyList<Curve> curves, double maxGap)
        {
            var chains = new List<ContourChain>();
            if (curves == null || curves.Count == 0) return chains;

            int count = curves.Count;

            var free = new HashSet<int>(Enumerable.Range(0, count));

            while (free.Count > 0)
            {
                int seed = free.First();
                free.Remove(seed);

                var chain = new ContourChain();
                chain.SegmentIds.Add(curves[seed].ObjectId);

                Point3d headEnd = GetEnd(curves, seed, end: true, fallbackToStart: true);
                Point3d headStart = GetEnd(curves, seed, end: false, fallbackToStart: true);

                bool grew = true;

                while (grew && free.Count > 0)
                {
                    grew = false;

                    int index;
                    bool matchedAtStart;

                    if (TryTakeNearestEnd(curves, free, headEnd, maxGap, out index, out matchedAtStart))
                    {
                        free.Remove(index);
                        chain.SegmentIds.Add(curves[index].ObjectId);

                        headEnd = GetEnd(curves, index, end: !matchedAtStart, fallbackToStart: true);
                        grew = true;
                        continue;
                    }

                    if (TryTakeNearestEnd(curves, free, headStart, maxGap, out index, out matchedAtStart))
                    {
                        free.Remove(index);
                        chain.SegmentIds.Add(curves[index].ObjectId);

                        headStart = GetEnd(curves, index, end: !matchedAtStart, fallbackToStart: true);
                        grew = true;
                    }
                }

                chains.Add(chain);
            }

            return chains;
        }

        /// <summary>
        /// Найти среди свободных отрезков ближайший конец к заданной точке.
        /// </summary>
        private static bool TryTakeNearestEnd(
            IReadOnlyList<Curve> curves,
            HashSet<int> free,
            Point3d point,
            double maxGap,
            out int foundIndex,
            out bool matchedAtStart)
        {
            foundIndex = -1;
            matchedAtStart = false;

            double best = maxGap;
            double bestSquared = maxGap * maxGap;

            foreach (int index in free)
            {
                Point3d start;
                Point3d end;

                if (!GetEnds(curves[index], out start, out end)) continue;

                double toStart = PlanDistance(point, start);
                if (toStart < bestSquared)
                {
                    bestSquared = toStart;
                    best = Math.Sqrt(toStart);
                    foundIndex = index;
                    matchedAtStart = true;
                }

                double toEnd = PlanDistance(point, end);
                if (toEnd < bestSquared)
                {
                    bestSquared = toEnd;
                    foundIndex = index;
                    matchedAtStart = false;
                }
            }

            return foundIndex >= 0 && best <= maxGap;
        }

        private static Point3d GetEnd(
            IReadOnlyList<Curve> curves,
            int index,
            bool end,
            bool fallbackToStart)
        {
            Point3d start;
            Point3d finish;

            if (!GetEnds(curves[index], out start, out finish))
            {
                return Point3d.Origin;
            }

            if (end) return finish;

            return start;
        }

        private static bool GetEnds(Curve curve, out Point3d start, out Point3d end)
        {
            start = Point3d.Origin;
            end = Point3d.Origin;

            if (curve == null) return false;

            try
            {
                start = curve.StartPoint;
                end = curve.EndPoint;
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static double PlanDistance(Point3d first, Point3d second)
        {
            double dx = first.X - second.X;
            double dy = first.Y - second.Y;

            return dx * dx + dy * dy;
        }
    }
}
