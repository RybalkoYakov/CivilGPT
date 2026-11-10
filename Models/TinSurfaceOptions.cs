using System;

namespace CivilGPT.Models
{
    /// <summary>
    /// Параметры построения TIN-поверхности по горизонталям и отметкам.
    /// Значения по умолчанию соответствуют типовой топосъёмке М 1:500.
    /// </summary>
    public class TinSurfaceOptions
    {
        public string SurfaceName { get; set; } = "";

        /// <summary>Расстояние по срединной ординате для сглаживания кривых, м.</summary>
        public double MidOrdinateDistance { get; set; } = 0.1;

        /// <summary>Максимальное расстояние между вершинами вдоль кривой, м.</summary>
        public double MaximumDistance { get; set; } = 10.0;

        /// <summary>Расстояние отсеивания (weeding) — прореживание вершин, м.</summary>
        public double WeedingDistance { get; set; } = 0.5;

        /// <summary>Дополняющее расстояние (supplementing) для горизонталей, м.</summary>
        public double SupplementingDistance { get; set; } = 0.5;

        /// <summary>Использовать кривые (полилинии, линии, сплайны) как горизонтали.</summary>
        public bool UseCurvesAsContours { get; set; } = true;

        /// <summary>Использовать тексты и мультитексты как отметки.</summary>
        public bool UseTextsAsElevationMarks { get; set; } = true;

        /// <summary>Использовать вхождения блоков как отметки.</summary>
        public bool UseBlocksAsElevationMarks { get; set; } = true;

        /// <summary>Использовать точки AutoCAD как отметки.</summary>
        public bool UsePointsAsElevationMarks { get; set; } = true;

        /// <summary>Предупреждать, если у горизонтали нулевая отметка.</summary>
        public bool WarnOnZeroElevation { get; set; } = true;

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(SurfaceName))
            {
                error = "Имя поверхности не может быть пустым.";
                return false;
            }

            if (MidOrdinateDistance <= 0)
            {
                error = "Расстояние по срединной ординате должно быть больше нуля.";
                return false;
            }

            if (MaximumDistance <= 0)
            {
                error = "Максимальное расстояние должно быть больше нуля.";
                return false;
            }

            if (WeedingDistance < 0)
            {
                error = "Расстояние отсеивания не может быть отрицательным.";
                return false;
            }

            if (SupplementingDistance < 0)
            {
                error = "Дополняющее расстояние не может быть отрицательным.";
                return false;
            }

            error = null;
            return true;
        }

        public static string SuggestSurfaceName()
        {
            return "TIN_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }
    }
}
