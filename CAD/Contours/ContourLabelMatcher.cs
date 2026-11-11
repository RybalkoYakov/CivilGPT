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
            out int conflicts,
            out HashSet<ObjectId> uncertain)
        {
            conflicts = 0;
            uncertain = new HashSet<ObjectId>();
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

                // Подпись стоит в разрыве своей линии, то есть рядом с её концом.
                // Соседняя целая линия может проходить ближе к тексту, поэтому
                // сначала ищем линию с близким концом, и только потом ближайшую.
                ObjectId endCurve = ObjectId.Null;
                double endDistance = double.MaxValue;

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

                    double distanceToEnd = DistanceToEnds(curve, label.Position);

                    if (distanceToEnd < endDistance)
                    {
                        endDistance = distanceToEnd;
                        endCurve = curve.ObjectId;
                    }
                }

                ObjectId chosen;
                double chosenDistance;

                // Если рядом есть линия вплотную — верим ей: подпись стоит
                // прямо на линии. К поиску по концам переходим, только когда
                // вплотную ничего нет, то есть подпись висит в разрыве.
                if (nearest <= CloseLineDistance
                    || endCurve.IsNull
                    || endDistance > effectiveTolerance * EndFactor
                    || endDistance >= nearest)
                {
                    if (nearestCurve.IsNull) continue;

                    bool accepted = nearest <= effectiveTolerance
                                    || (nearest <= effectiveTolerance * FallbackFactor
                                        && secondBest > nearest * AmbiguityRatio);

                    if (!accepted) continue;

                    chosen = nearestCurve;
                    chosenDistance = nearest;
                }
                else
                {
                    chosen = endCurve;
                    chosenDistance = endDistance;
                }

                ContourLabel existing;
                if (result.TryGetValue(chosen, out existing))
                {
                    conflicts++;
                    uncertain.Add(chosen);

                    // Оставляем более информативную подпись (полную), затем — ближайшую.
                    if (existing.IsFull && !label.IsFull) continue;
                    if (existing.IsFull == label.IsFull && chosenDistance >= bestDistance[chosen]) continue;
                }

                // Спорная привязка — когда рядом оказалась вторая линия почти
                // так же близко: именно там подпись может уехать на соседнюю.
                if (chosenDistance > UncertainDistance
                    || (secondBest < nearest * AmbiguityRatio && nearest > CloseLineDistance))
                {
                    uncertain.Add(chosen);
                }

                result[chosen] = label;
                bestDistance[chosen] = chosenDistance;
            }

            return result;
        }

        /// <summary>Во сколько раз допускается превысить допуск при близком конце линии.</summary>
        private const double EndFactor = 2.0;

        /// <summary>Расстояние, при котором линия считается лежащей вплотную к подписи, м.</summary>
        private const double CloseLineDistance = 0.4;

        /// <summary>Расстояние, после которого привязка считается спорной, м.</summary>
        private const double UncertainDistance = 0.5;

        /// <summary>Расстояние до ближайшего конца кривой.</summary>
        private static double DistanceToEnds(Curve curve, Point3d point)
        {
            try
            {
                Point3d start = curve.StartPoint;
                Point3d end = curve.EndPoint;

                double toStart = Math.Sqrt(
                    (start.X - point.X) * (start.X - point.X) + (start.Y - point.Y) * (start.Y - point.Y));

                double toEnd = Math.Sqrt(
                    (end.X - point.X) * (end.X - point.X) + (end.Y - point.Y) * (end.Y - point.Y));

                return Math.Min(toStart, toEnd);
            }
            catch (System.Exception)
            {
                return double.MaxValue;
            }
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
