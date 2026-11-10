using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Contours
{
    /// <summary>
    /// Связь «горизонталь — её подпись».
    /// Подпись в топографических чертежах обычно стоит в разрыве линии,
    /// поэтому критерий — минимальное плановое расстояние от текста до кривой.
    /// </summary>
    public sealed class ContourAssignment
    {
        public ObjectId CurveId { get; set; }

        public ObjectId TextId { get; set; }

        public Point3d Position { get; set; }

        public double RawValue { get; set; }

        public bool IsFull { get; set; }

        /// <summary>Восстановленная абсолютная отметка, null если восстановить не удалось.</summary>
        public double? Elevation { get; set; }
    }

    public static class ContourLabelMatcher
    {
        /// <summary>
        /// Для каждой горизонтали найти ближайшую подпись в пределах допуска.
        /// </summary>
        /// <param name="curves">Кривые-горизонтали.</param>
        /// <param name="labels">Разобранные подписи.</param>
        /// <param name="tolerance">Максимальное плановое расстояние текст↔линия.</param>
        /// <param name="conflicts">Количество случаев, когда на линию претендовали несколько подписей.</param>
        public static List<ContourAssignment> Match(
            IReadOnlyList<Curve> curves,
            IReadOnlyList<ContourLabel> labels,
            double tolerance,
            out int conflicts)
        {
            conflicts = 0;
            var assignments = new List<ContourAssignment>();

            if (curves == null || labels == null || labels.Count == 0) return assignments;

            // Для каждой линии — лучшая (самая близкая) подпись
            var bestByCurve = new Dictionary<ObjectId, int>();
            var bestDistance = new Dictionary<ObjectId, double>();

            for (int i = 0; i < labels.Count; i++)
            {
                ContourLabel label = labels[i];
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

                if (nearestCurve.IsNull || nearestDistance > effectiveTolerance)
                {
                    continue;
                }

                int existing;
                if (bestByCurve.TryGetValue(nearestCurve, out existing))
                {
                    conflicts++;

                    // Оставляем подпись, которая стоит ближе к линии
                    if (nearestDistance >= bestDistance[nearestCurve]) continue;
                }

                bestByCurve[nearestCurve] = i;
                bestDistance[nearestCurve] = nearestDistance;
            }

            foreach (KeyValuePair<ObjectId, int> pair in bestByCurve)
            {
                ContourLabel label = labels[pair.Value];

                assignments.Add(new ContourAssignment
                {
                    CurveId = pair.Key,
                    TextId = label.TextId,
                    Position = label.Position,
                    RawValue = label.Value,
                    IsFull = label.IsFull
                });
            }

            return assignments;
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
