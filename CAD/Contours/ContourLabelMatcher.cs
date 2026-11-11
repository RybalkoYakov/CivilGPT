using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Contours
{
    /// <summary>
    /// Привязка подписи к ближайшему отрезку горизонтали.
    /// Подпись ставится в разрыве линии или у её конца, поэтому критерий —
    /// плановое расстояние до кривой. Если до ближайшей линии далеко, но она
    /// заметно ближе остальных, привязка всё равно принимается: в плотной
    /// группе горизонталей это важнее формального допуска.
    /// </summary>
    public static class ContourLabelMatcher
    {
        /// <summary>Во сколько раз допускается превысить допуск при явном лидере по расстоянию.</summary>
        private const double FallbackFactor = 3.0;

        /// <summary>Во сколько раз ближайшая линия должна быть ближе второй.</summary>
        private const double AmbiguityRatio = 2.0;

        public static Dictionary<ObjectId, ContourLabel> Match(
            IReadOnlyList<Curve> curves,
            IReadOnlyList<ContourLabel> labels,
            double tolerance,
            out int conflicts)
        {
            conflicts = 0;
            var result = new Dictionary<ObjectId, ContourLabel>();

            if (curves == null || labels == null || labels.Count == 0 || curves.Count == 0)
            {
                return result;
            }

            var bestDistance = new Dictionary<ObjectId, double>();

            foreach (ContourLabel label in labels)
            {
                double effectiveTolerance = Math.Max(tolerance, label.Height * 5.0);

                ObjectId nearestCurve = ObjectId.Null;
                double nearest = double.MaxValue;
                double secondBest = double.MaxValue;

                foreach (Curve curve in curves)
                {
                    double distance = PlanDistanceToCurve(curve, label.Position);

                    if (distance < nearest)
                    {
                        secondBest = nearest;
                        nearest = distance;
                        nearestCurve = curve.ObjectId;
                    }
                    else if (distance < secondBest)
                    {
                        secondBest = distance;
                    }
                }

                if (nearestCurve.IsNull) continue;

                bool accepted = nearest <= effectiveTolerance
                                || (nearest <= effectiveTolerance * FallbackFactor
                                    && secondBest > nearest * AmbiguityRatio);

                if (!accepted) continue;

                ContourLabel existing;
                if (result.TryGetValue(nearestCurve, out existing))
                {
                    conflicts++;

                    // Оставляем более информативную подпись (полную), затем — ближайшую.
                    if (existing.IsFull && !label.IsFull) continue;
                    if (existing.IsFull == label.IsFull && nearest >= bestDistance[nearestCurve]) continue;
                }

                result[nearestCurve] = label;
                bestDistance[nearestCurve] = nearest;
            }

            return result;
        }

        private static double PlanDistanceToCurve(Curve curve, Point3d point)
        {
            try
            {
                Point3d closest = curve.GetClosestPointTo(point, false);

                double dx = closest.X - point.X;
                double dy = closest.Y - point.Y;

                return Math.Sqrt(dx * dx + dy * dy);
            }
            catch (System.Exception)
            {
                return double.MaxValue;
            }
        }
    }
}
