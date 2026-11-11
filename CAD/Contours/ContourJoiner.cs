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
    /// необработанного отрезка, присоединяем и продолжаем с нового конца.
    /// То же в обратную сторону. Когда продолжения нет — цепочка собрана,
    /// её отрезки исключаются из необработанных, а вершины складываются
    /// в один упорядоченный список для итоговой полилинии.
    /// </summary>
    public static class ContourJoiner
    {
        /// <summary>Дальний предел поиска: конец может быть далеко, если он явно ближайший.</summary>
        private const double FarGap = 30.0;

        /// <summary>Во сколько раз ближайший конец должен быть ближе второго, чтобы брать дальний.</summary>
        private const double Prominence = 0.5;

        public static List<ContourChain> Build(IReadOnlyList<Curve> curves, double maxGap)
        {
            var chains = new List<ContourChain>();
            if (curves == null || curves.Count == 0) return chains;

            var free = new HashSet<int>(Enumerable.Range(0, curves.Count));

            while (free.Count > 0)
            {
                int seed = free.First();
                free.Remove(seed);

                var chain = new ContourChain();
                chain.SegmentIds.Add(curves[seed].ObjectId);

                List<Point3d> points = GetPoints(curves[seed]);
                if (points.Count < 2) continue;

                chain.Points.AddRange(points);

                Point3d headStart = chain.Points[0];
                Point3d headEnd = chain.Points[chain.Points.Count - 1];

                bool grew = true;

                while (grew && free.Count > 0)
                {
                    grew = false;

                    int index;
                    bool matchedAtStart;

                    if (TryTakeNearestEnd(curves, free, headEnd, maxGap, out index, out matchedAtStart))
                    {
                        free.Remove(index);

                        List<Point3d> oriented = Orient(GetPoints(curves[index]), matchedAtStart);
                        if (oriented.Count >= 2)
                        {
                            chain.SegmentIds.Add(curves[index].ObjectId);
                            chain.Points.AddRange(oriented.Skip(1));
                            headEnd = chain.Points[chain.Points.Count - 1];
                            grew = true;
                            continue;
                        }
                    }

                    if (TryTakeNearestEnd(curves, free, headStart, maxGap, out index, out matchedAtStart))
                    {
                        free.Remove(index);

                        // Пристраиваем спереди: разворачиваем так, чтобы последняя
                        // точка совпала с началом цепочки.
                        List<Point3d> oriented = Orient(GetPoints(curves[index]), !matchedAtStart);
                        oriented.Reverse();

                        if (oriented.Count >= 2)
                        {
                            chain.SegmentIds.Add(curves[index].ObjectId);

                            var prefix = oriented.Take(oriented.Count - 1).ToList();
                            chain.Points.InsertRange(0, prefix);
                            headStart = chain.Points[0];
                            grew = true;
                        }
                    }
                }

                chains.Add(chain);
            }

            return chains;
        }

        /// <summary>
        /// Развернуть точки отрезка так, чтобы первой шла точка, которой он
        /// примыкает к цепочке.
        /// </summary>
        private static List<Point3d> Orient(List<Point3d> points, bool atStart)
        {
            if (!atStart) points.Reverse();

            return points;
        }

        /// <summary>
        /// Ближайший свободный конец. Берём в пределах maxGap; если он дальше,
        /// но заметно ближе всех остальных — тоже берём.
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

            double best = double.MaxValue;
            double second = double.MaxValue;
            int bestIndex = -1;
            bool bestAtStart = false;

            foreach (int index in free)
            {
                List<Point3d> points = GetPoints(curves[index]);
                if (points.Count < 2) continue;

                double toStart = PlanDistance(point, points[0]);
                double toEnd = PlanDistance(point, points[points.Count - 1]);

                AddCandidate(toStart, true, ref best, ref second, ref bestIndex, ref bestAtStart, index);
                AddCandidate(toEnd, false, ref best, ref second, ref bestIndex, ref bestAtStart, index);
            }

            if (bestIndex < 0) return false;

            bool close = best <= maxGap;
            bool prominent = best <= FarGap && best < second * Prominence;

            if (!close && !prominent) return false;

            foundIndex = bestIndex;
            matchedAtStart = bestAtStart;
            return true;
        }

        private static void AddCandidate(
            double distance,
            bool atStart,
            ref double best,
            ref double second,
            ref int bestIndex,
            ref bool bestAtStart,
            int index)
        {
            if (distance < best)
            {
                second = best;
                best = distance;
                bestIndex = index;
                bestAtStart = atStart;
            }
            else if (distance < second)
            {
                second = distance;
            }
        }

        /// <summary>
        /// Вершины кривой по порядку. Дуги не поддерживаются — горизонтали
        /// на этих чертежах состоят из отрезков.
        /// </summary>
        private static List<Point3d> GetPoints(Curve curve)
        {
            var points = new List<Point3d>();
            if (curve == null) return points;

            try
            {
                var polyline = curve as Polyline;
                if (polyline != null)
                {
                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        Point2d vertex = polyline.GetPoint2dAt(i);
                        points.Add(new Point3d(vertex.X, vertex.Y, 0.0));
                    }

                    return points;
                }

                // Прочие кривые: снимаем точки по параметру.
                double start = curve.StartParam;
                double end = curve.EndParam;
                const int steps = 20;

                for (int i = 0; i <= steps; i++)
                {
                    double parameter = start + (end - start) * i / steps;
                    Point3d point = curve.GetPointAtParameter(parameter);
                    points.Add(new Point3d(point.X, point.Y, 0.0));
                }
            }
            catch (System.Exception)
            {
                points.Clear();
            }

            return points;
        }

        private static double PlanDistance(Point3d first, Point3d second)
        {
            double dx = first.X - second.X;
            double dy = first.Y - second.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
