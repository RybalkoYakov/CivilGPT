using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CivilGPT.CAD.Contours
{
    /// <summary>
    /// Разобранная подпись у горизонтали.
    /// В исходных чертежах подпись — это не текст, а вставка блока с атрибутом
    /// (например, блок с тегом «50» и значением «60»), поэтому SourceId указывает
    /// на объект-носитель: текст, мультивыноску или вставку блока.
    /// </summary>
    public sealed class ContourLabel
    {
        public ObjectId SourceId { get; set; }

        /// <summary>Точка вставки подписи.</summary>
        public Point3d Position { get; set; }

        /// <summary>Исходная строка подписи.</summary>
        public string RawText { get; set; }

        /// <summary>
        /// Для полной отметки — её значение (205.00),
        /// для сокращённой — только две последние цифры (70 → 70).
        /// </summary>
        public double Value { get; set; }

        /// <summary>true — отметка записана полностью, false — сокращённо.</summary>
        public bool IsFull { get; set; }

        /// <summary>Высота текста в единицах чертежа — для допуска привязки.</summary>
        public double Height { get; set; }
    }
}
