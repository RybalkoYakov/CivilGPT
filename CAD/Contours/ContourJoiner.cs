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

            var parent = new int[count];
            for (int i = 0; i < count; i++) parent[i] = i;

            double cosLimit = Math.Cos(maxAngleDegrees * Math.PI / 180.0);

            for (int a = 0; a < ends.Count; a++)
            {
                for (int b = a + 1; b < ends.Count; b++)
                {
                    EndPoint first = ends[a];
                    EndPoint second = ends[b];

                    if (first.CurveIndex == second.CurveIndex) continue;
                    if (Root(parent, first.CurveIndex) == Root(parent, second.CurveIndex)) continue;

                    // Направления «внутрь своих кривых» должны быть противоположны:
                    // тогда отрезки продолжают друг друга, а не идут параллельно рядом.
                    double directionDot = first.Into.DotProduct(second.Into);
                    if (directionDot > -cosLimit) continue;

                    Vector3d gap = second.Point - first.Point;
                    double distance = gap.Length;
                    if (distance > maxGap) continue;

                    // Разрыв должен идти вдоль линии, а не вбок.
                    Vector3d forward = -first.Into;
                    double along = gap.DotProduct(forward);
                    if (along <= 0) continue;

                    Vector3d lateral = gap - forward * along;
                    if (lateral.Length > lateralTolerance) continue;

                    // Подписи — независимая проверка сшивки:
                    // два отрезка с разными отметками не могут быть одной горизонталью.
                    if (labels != null)
                    {
                        ContourLabel firstLabel, secondLabel;
                        bool firstHas = labels.TryGetValue(curves[first.CurveIndex].ObjectId, out firstLabel);
                        bool secondHas = labels.TryGetValue(curves[second.CurveIndex].ObjectId, out secondLabel);

                        if (firstHas && secondHas
                            && Math.Abs(firstLabel.Value - secondLabel.Value) > 1e-6)
                        {
                            continue;
                        }
                    }

                    Union(parent, first.CurveIndex, second.CurveIndex);
                }
            }

            var byRoot = new Dictionary<int, ContourChain>();

            for (int i = 0; i < count; i++)
            {
                int root = Root(parent, i);

                ContourChain chain;
                if (!byRoot.TryGetValue(root, out chain))
                {
                    chain = new ContourChain();
                    byRoot[root] = chain;
                    chains.Add(chain);
                }

                chain.SegmentIds.Add(curves[i].ObjectId);
            }

            return chains;
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

        private static int Root(int[] parent, int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }
            return index;
        }

        private static void Union(int[] parent, int first, int second)
        {
            int rootFirst = Root(parent, first);
            int rootSecond = Root(parent, second);

            if (rootFirst != rootSecond)
            {
                parent[rootSecond] = rootFirst;
            }
        }
    }
}
