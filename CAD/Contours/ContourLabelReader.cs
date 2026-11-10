using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Contours
{
    /// <summary>
    /// Чтение подписей горизонталей.
    /// В исходных чертежах подпись — это вставка блока с атрибутом
    /// (у контуров — тег «50», у точечных отметок — тег «ОТМЕТКА»),
    /// поэтому текстовых объектов на слое почти нет.
    /// </summary>
    public static class ContourLabelReader
    {
        /// <summary>Тег атрибута точечных отметок — их нельзя считать подписями горизонталей.</summary>
        private const string SpotMarkTagFragment = "отметк";

        public static ContourLabel TryRead(Transaction transaction, Entity entity)
        {
            if (entity == null) return null;

            var text = entity as DBText;
            if (text != null)
            {
                return Create(entity.ObjectId, text.TextString, text.Position, text.Height);
            }

            var mtext = entity as MText;
            if (mtext != null)
            {
                return Create(entity.ObjectId, mtext.Text, mtext.Location, mtext.TextHeight);
            }

            var mleader = entity as MLeader;
            if (mleader != null)
            {
                string content = null;

                try
                {
                    var inner = mleader.MText;
                    if (inner != null) content = inner.Text;
                }
                catch (System.Exception)
                {
                    // Мультивыноска с блоком вместо текста — пропускаем.
                }

                return Create(entity.ObjectId, content, GetCenter(mleader), 0.0);
            }

            var block = entity as BlockReference;
            if (block != null)
            {
                return ReadFromAttributes(transaction, block);
            }

            return null;
        }

        private static ContourLabel ReadFromAttributes(Transaction transaction, BlockReference block)
        {
            foreach (ObjectId attributeId in block.AttributeCollection)
            {
                var attribute = transaction.GetObject(attributeId, OpenMode.ForRead, false)
                    as AttributeReference;

                if (attribute == null) continue;

                string tag = attribute.Tag ?? string.Empty;

                // Точечные отметки (тег «ОТМЕТКА») к горизонталям не относятся.
                if (tag.IndexOf(SpotMarkTagFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                ContourLabel label = Create(
                    block.ObjectId,
                    attribute.TextString,
                    attribute.Position,
                    attribute.Height);

                if (label != null) return label;
            }

            return null;
        }

        private static ContourLabel Create(
            ObjectId sourceId,
            string rawText,
            Point3d position,
            double height)
        {
            double value;
            bool isFull;

            if (!ContourTextParser.TryParse(rawText, out value, out isFull)) return null;

            return new ContourLabel
            {
                SourceId = sourceId,
                Position = position,
                RawText = rawText,
                Value = value,
                IsFull = isFull,
                Height = height
            };
        }

        private static Point3d GetCenter(Entity entity)
        {
            try
            {
                Extents3d extents = entity.GeometricExtents;

                return new Point3d(
                    (extents.MinPoint.X + extents.MaxPoint.X) * 0.5,
                    (extents.MinPoint.Y + extents.MaxPoint.Y) * 0.5,
                    extents.MinPoint.Z);
            }
            catch (System.Exception)
            {
                return entity is MLeader ? ((MLeader)entity).TextLocation : Point3d.Origin;
            }
        }
    }
}
