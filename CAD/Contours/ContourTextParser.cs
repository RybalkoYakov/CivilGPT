using System.Globalization;

namespace CivilGPT.CAD.Contours
{
    /// <summary>
    /// Разбор подписей горизонталей.
    /// В топографии подпись бывает полной (205.00) и сокращённой (70 = 204.70),
    /// поэтому одного числа недостаточно — нужно знать, какой это вид.
    /// </summary>
    public static class ContourTextParser
    {
        /// <summary>
        /// Разобрать подпись.
        /// </summary>
        /// <param name="raw">Текст подписи.</param>
        /// <param name="value">Полная отметка или две последние цифры.</param>
        /// <param name="isFull">true — отметка полная.</param>
        public static bool TryParse(string raw, out double value, out bool isFull)
        {
            value = 0.0;
            isFull = false;

            if (string.IsNullOrWhiteSpace(raw)) return false;

            string text = raw.Trim().Replace(',', '.').Replace(' ', '\0').Replace("\0", string.Empty);

            // Любая буква означает инженерную отметку или подпись:
            // «д.к.» (дно колодца), «о.к.», «ПК 12+50», «Ворота» — это не горизонтали.
            foreach (char c in text)
            {
                if (char.IsLetter(c)) return false;
            }

            // Допускаем только цифры и одну десятичную точку.
            int dots = 0;
            foreach (char c in text)
            {
                if (c == '.')
                {
                    dots++;
                    continue;
                }

                if (c < '0' || c > '9') return false;
            }

            if (dots > 1) return false;

            if (dots == 1)
            {
                // «205.00», «204.8» — полная отметка
                if (!double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value))
                {
                    return false;
                }

                // Отметки рельефа в Балтийской системе — порядка сотен метров.
                if (value < 0 || value > 2000) return false;

                isFull = true;
                return true;
            }

            // Без точки: 3 цифры — полная целая отметка (205), 1–2 цифры — сокращённая (70).
            if (!double.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value))
            {
                return false;
            }

            if (text.Length >= 3)
            {
                if (value < 100 || value > 2000) return false;
                isFull = true;
                return true;
            }

            if (value > 99) return false;

            isFull = false;
            return true;
        }

        /// <summary>
        /// Развернуть сокращённую подпись, опираясь на ближайшую полную отметку.
        /// Например, 70 при опорной отметке 205.00 даёт 204.70 (а не 205.70),
        /// потому что это значение ближе к опорному.
        /// </summary>
        public static double ExpandShort(double shortValue, double anchorElevation)
        {
            double fraction = shortValue / 100.0;
            double baseMeter = System.Math.Floor(anchorElevation);

            double best = baseMeter + fraction;
            double bestDelta = System.Math.Abs(best - anchorElevation);

            for (int shift = -2; shift <= 2; shift++)
            {
                if (shift == 0) continue;

                double candidate = baseMeter + shift + fraction;
                double delta = System.Math.Abs(candidate - anchorElevation);

                if (delta < bestDelta)
                {
                    best = candidate;
                    bestDelta = delta;
                }
            }

            return best;
        }
    }
}
