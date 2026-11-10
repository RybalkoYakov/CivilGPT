using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Contours
{
    /// <summary>
    /// Привязка подписи к ближайшему отрезку горизонтали.
    /// Подпись в топографических чертежах ставится в разрыве линии,
    /// поэтому критерий — минимальное плановое расстояние до кривой.
    /// </summary>
    public static class ContourLabelMatcher
    {
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
                double nearestDistance = double.MaxValue;

                foreach (Curve curve in curves)
                {
                    double distance = PlanDistanceToCurve(curve, label.Position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestCurve = curve.ObjectId;
                    }
                }

                if (nearestCurve.IsNull || nearestDistance > effectiveTolerance) continue;

                ContourLabel existing;
                if (result.TryGetValue(nearestCurve, out existing))
                {
                    conflicts++;

                    // Оставляем более информативную подпись (полную), затем — ближайшую.
                    bool existingIsFull = existing.IsFull;
                    bool newIsFull = label.IsFull;

                    if (existingIsFull && !newIsFull) continue;
                    if (existingIsFull == newIsFull && nearestDistance >= bestDistance[nearestCurve]) continue;
                }

                result[nearestCurve] = label;
                bestDistance[nearestCurve] = nearestDistance;
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
