namespace CivilGPT.Models
{
    /// <summary>
    /// Параметры случайного вертикального отклонения точек COGO
    /// от их проектных (текущих) отметок.
    /// </summary>
    public class ElevationOffsetOptions
    {
        public double MinOffset { get; set; } = -0.02;

        public double MaxOffset { get; set; } = 0.02;

        public bool IsValid(out string error)
        {
            if (double.IsNaN(MinOffset) || double.IsNaN(MaxOffset))
            {
                error = "Значения смещения не могут быть NaN.";
                return false;
            }

            if (double.IsInfinity(MinOffset) || double.IsInfinity(MaxOffset))
            {
                error = "Значения смещения не могут быть бесконечными.";
                return false;
            }

            if (MinOffset > MaxOffset)
            {
                error = $"Минимальное значение ({MinOffset}) больше максимального ({MaxOffset}).";
                return false;
            }

            error = null;
            return true;
        }
    }
}
