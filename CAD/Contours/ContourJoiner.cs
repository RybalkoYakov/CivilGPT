using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Contours
{
    /// <summary>
    /// Сшивка отрезков в цельные горизонтали.
    /// Отрезки соединяются, если их концы близко, лежат на одной линии
    /// (малый боковой зазор) и направления почти совпадают.
    /// </summary>
    public static class ContourJoiner
    {
        /// <summary>
        /// Расстояние, при котором концы считаются совпадающими: такой стык
        /// соединяется без проверки угла и бокового смещения.
        /// </summary>
        private const double CoincidentGap = 0.1;

        private sealed class EndPoint
        {
            public int CurveIndex;

            /// <summary>Направление внутрь кривой от этой точки.</summary>
            public Vector3d Into;

            public Point3d Point;
        }

        /// <summary>
        /// Построить цепочки.
        /// </summary>
        /// <param name="curves">Отрезки.</param>
        /// <param name="maxGap">Максимальный разрыв между концами, м.</param>
        /// <param name="lateralTolerance">Допустимое боковое смещение концов, м.</param>
        /// <param name="maxAngleDegrees">Допустимый излом направления, градусы.</param>
        public static List<ContourChain> Build(
            IReadOnlyList<Curve> curves,
            double maxGap,
            double lateralTolerance,
            double maxAngleDegrees,
            IDictionary<ObjectId, ContourLabel> labels = null)
        {
            var chains = new List<ContourChain>();
            if (curves == null || curves.Count == 0) return chains;

            int count = curves.Count;

            var ends = new List<EndPoint>(count * 2);

            for (int i = 0; i < count; i++)
            {
                CollectEnds(curves[i], i, ends);
            }

            var components = new Component[count];
            for (int i = 0; i < count; i++)
            {
                components[i] = new Component { Parent = i };

                ContourLabel existing;
                if (labels != null && labels.TryGetValue(curves[i].ObjectId, out existing))
                {
                    components[i].LabelValue = existing.Value;
                }
            }

            double cosLimit = Math.Cos(maxAngleDegrees * Math.PI / 180.0);

            var joinedGaps = new List<JoinedGap>();

            for (int a = 0; a < ends.Count; a++)
            {
                for (int b = a + 1; b < ends.Count; b++)
                {
                    EndPoint first = ends[a];
                    EndPoint second = ends[b];

                    if (first.CurveIndex == second.CurveIndex) continue;

                    int rootFirst = Root(components, first.CurveIndex);
                    int rootSecond = Root(components, second.CurveIndex);
                    if (rootFirst == rootSecond) continue;

                    Vector3d gap = second.Point - first.Point;
                    double distance = gap.Length;
                    if (distance > maxGap) continue;

                    // Стык «конец в конец» соединяем при любом угле: горизонталь
                    // может изломиться у лотка, здания или подпорной стенки.
                    bool touching = distance <= CoincidentGap;

                    if (!touching)
                    {
                        // Направления «внутрь своих кривых» должны быть противоположны:
                        // тогда отрезки продолжают друг друга, а не идут параллельно рядом.
                        double directionDot = first.Into.DotProduct(second.Into);
                        if (directionDot > -cosLimit) continue;

                        // Разрыв должен идти вдоль линии, а не вбок.
                        Vector3d forward = -first.Into;
                        double along = gap.DotProduct(forward);
                        if (along <= 0) continue;

                        Vector3d lateral = gap - forward * along;
                        if (lateral.Length > lateralTolerance) continue;
                    }

                    // Подписи — независимая проверка сшивки: в одной горизонтали
                    // не может быть двух разных отметок. Проверяем по всей цепочке.
                    double? firstLabel = components[rootFirst].LabelValue;
                    double? secondLabel = components[rootSecond].LabelValue;

                    if (firstLabel.HasValue && secondLabel.HasValue
                        && Math.Abs(firstLabel.Value - secondLabel.Value) > 1e-6)
                    {
                        continue;
                    }

                    Union(components, rootFirst, rootSecond);

                    joinedGaps.Add(new JoinedGap
                    {
                        CurveIndex = first.CurveIndex,
                        Midpoint = new Point3d(
                            (first.Point.X + second.Point.X) * 0.5,
                            (first.Point.Y + second.Point.Y) * 0.5,
                            0.0)
                    });
                }
            }

            var byRoot = new Dictionary<int, ContourChain>();
            var chainByCurve = new Dictionary<int, ContourChain>();

            for (int i = 0; i < count; i++)
            {
                int root = Root(components, i);

                ContourChain chain;
                if (!byRoot.TryGetValue(root, out chain))
                {
                    chain = new ContourChain();
                    byRoot[root] = chain;
                    chains.Add(chain);
                }

                chain.SegmentIds.Add(curves[i].ObjectId);
                chainByCurve[i] = chain;
            }

            // Разрывы внутри цепочки — точные места, где стоят подписи.
            foreach (JoinedGap gap in joinedGaps)
            {
                ContourChain chain;
                if (chainByCurve.TryGetValue(gap.CurveIndex, out chain))
                {
                    chain.GapMidpoints.Add(gap.Midpoint);
                }
            }

            return chains;
        }

        /// <summary>Стык, который был соединён: нужен для привязки подписей.</summary>
        private sealed class JoinedGap
        {
            public int CurveIndex;
            public Point3d Midpoint;
        }

        private static void CollectEnds(Curve curve, int index, List<EndPoint> ends)
        {
            if (curve == null) return;

            try
            {
                Point3d start = curve.StartPoint;
                Point3d end = curve.EndPoint;

                // Замкнутые горизонтали не сшиваем — они самодостаточны.
                if (start.DistanceTo(end) < 1e-6) return;

                Vector3d startTangent = ToPlanar(curve.GetFirstDerivative(curve.StartParam));
                Vector3d endTangent = ToPlanar(curve.GetFirstDerivative(curve.EndParam));

                if (startTangent.Length < 1e-9 || endTangent.Length < 1e-9) return;

                ends.Add(new EndPoint
                {
                    CurveIndex = index,
                    Point = start,
                    Into = startTangent.GetNormal()
                });

                ends.Add(new EndPoint
                {
                    CurveIndex = index,
                    Point = end,
                    Into = -endTangent.GetNormal()
                });
            }
            catch (System.Exception)
            {
                // Кривая с неполной геометрией останется отдельной цепочкой.
            }
        }

        private static Vector3d ToPlanar(Vector3d vector)
        {
            return new Vector3d(vector.X, vector.Y, 0.0);
        }

        private static int Root(Component[] components, int index)
        {
            while (components[index].Parent != index)
            {
                components[index].Parent = components[components[index].Parent].Parent;
                index = components[index].Parent;
            }
            return index;
        }

        private static void Union(Component[] components, int rootFirst, int rootSecond)
        {
            if (rootFirst == rootSecond) return;

            components[rootSecond].Parent = rootFirst;

            // Отметка подписи переносится на объединённую цепочку.
            if (!components[rootFirst].LabelValue.HasValue)
            {
                components[rootFirst].LabelValue = components[rootSecond].LabelValue;
            }
        }

        /// <summary>Компонента сшивки: родитель и отметка подписи, если она есть.</summary>
        private sealed class Component
        {
            public int Parent;
            public double? LabelValue;
        }
    }
}
