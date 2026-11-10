using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CivilGPT.Infrastructure
{
    /// <summary>
    /// Минимальная реализация JSON без внешних зависимостей.
    /// В процессе AutoCAD нельзя применить binding redirect, поэтому плагин
    /// намеренно не использует NuGet-пакеты сериализации: любая такая
    /// зависимость рискует не загрузиться из-за конфликта версий.
    /// Модель данных: Dictionary&lt;string, object&gt;, List&lt;object&gt;, string, double, bool, null.
    /// </summary>
    public static class JsonHelper
    {
        public static object Deserialize(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            var parser = new Parser(json);
            object value = parser.ParseValue();

            parser.SkipWhitespace();
            if (!parser.IsEnd)
            {
                throw new FormatException(
                    "Лишние символы в JSON на позиции " + parser.Position + ".");
            }

            return value;
        }

        public static string Serialize(object value)
        {
            var builder = new StringBuilder(256);
            WriteValue(builder, value);
            return builder.ToString();
        }

        public static IDictionary<string, object> AsDictionary(object value)
        {
            return value as IDictionary<string, object>;
        }

        public static IList<object> AsArray(object value)
        {
            return value as IList<object>;
        }

        public static string GetString(
            IDictionary<string, object> source,
            string key,
            string defaultValue = null)
        {
            object value;
            if (source == null || key == null) return defaultValue;
            if (!source.TryGetValue(key, out value) || value == null) return defaultValue;

            var text = value as string;
            if (text != null) return text;

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static bool TryGetDouble(
            IDictionary<string, object> source,
            string key,
            out double value)
        {
            value = 0.0;

            object raw;
            if (source == null || key == null) return false;
            if (!source.TryGetValue(key, out raw) || raw == null) return false;

            if (raw is bool) return false;

            var text = raw as string;
            if (text != null)
            {
                return double.TryParse(
                    text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
            }

            try
            {
                value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        public static bool GetBool(
            IDictionary<string, object> source,
            string key,
            bool defaultValue = false)
        {
            object raw;
            if (source == null || key == null) return defaultValue;
            if (!source.TryGetValue(key, out raw) || raw == null) return defaultValue;

            if (raw is bool) return (bool)raw;

            var text = raw as string;
            bool parsed;
            if (text != null && bool.TryParse(text, out parsed)) return parsed;

            return defaultValue;
        }

        /// <summary>
        /// Массив целых неотрицательных чисел (номера точек и т.п.).
        /// Возвращает null, если данные некорректны, и пустой список, если поля нет.
        /// </summary>
        public static List<uint> GetUIntList(IDictionary<string, object> source, string key)
        {
            var result = new List<uint>();

            object raw;
            if (source == null || key == null) return result;
            if (!source.TryGetValue(key, out raw)) return result;

            var items = raw as IList<object>;
            if (items == null) return null;

            foreach (object item in items)
            {
                if (item == null) return null;

                double number;
                try
                {
                    number = Convert.ToDouble(item, CultureInfo.InvariantCulture);
                }
                catch (System.Exception)
                {
                    return null;
                }

                if (number < 0 || Math.Abs(number - Math.Round(number)) > 1e-9) return null;

                result.Add((uint)Math.Round(number));
            }

            return result;
        }

        // ---------- запись ----------

        private static void WriteValue(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            var text = value as string;
            if (text != null)
            {
                WriteString(builder, text);
                return;
            }

            if (value is bool)
            {
                builder.Append((bool)value ? "true" : "false");
                return;
            }

            var dictionary = value as IDictionary<string, object>;
            if (dictionary != null)
            {
                WriteObject(builder, dictionary);
                return;
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                WriteArray(builder, enumerable);
                return;
            }

            switch (Convert.GetTypeCode(value))
            {
                case TypeCode.Double:
                case TypeCode.Single:
                case TypeCode.Decimal:
                    builder.Append(Convert.ToDouble(value, CultureInfo.InvariantCulture)
                        .ToString("R", CultureInfo.InvariantCulture));
                    return;

                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
            }

            WriteString(builder, Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static void WriteObject(StringBuilder builder, IDictionary<string, object> source)
        {
            builder.Append('{');

            bool first = true;
            foreach (KeyValuePair<string, object> pair in source)
            {
                if (!first) builder.Append(',');
                first = false;

                WriteString(builder, pair.Key);
                builder.Append(':');
                WriteValue(builder, pair.Value);
            }

            builder.Append('}');
        }

        private static void WriteArray(StringBuilder builder, IEnumerable source)
        {
            builder.Append('[');

            bool first = true;
            foreach (object item in source)
            {
                if (!first) builder.Append(',');
                first = false;

                WriteValue(builder, item);
            }

            builder.Append(']');
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');

            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); continue;
                    case '\\': builder.Append("\\\\"); continue;
                    case '\b': builder.Append("\\b"); continue;
                    case '\f': builder.Append("\\f"); continue;
                    case '\n': builder.Append("\\n"); continue;
                    case '\r': builder.Append("\\r"); continue;
                    case '\t': builder.Append("\\t"); continue;
                }

                if (c < ' ')
                {
                    builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    continue;
                }

                builder.Append(c);
            }

            builder.Append('"');
        }

        // ---------- разбор ----------

        private sealed class Parser
        {
            private readonly string _text;
            private int _index;

            public Parser(string text)
            {
                _text = text;
                _index = 0;
            }

            public int Position => _index;

            public bool IsEnd => _index >= _text.Length;

            public void SkipWhitespace()
            {
                while (!IsEnd && char.IsWhiteSpace(_text[_index])) _index++;
            }

            public object ParseValue()
            {
                SkipWhitespace();

                if (IsEnd) throw Error("Неожиданный конец JSON.");

                char current = _text[_index];

                switch (current)
                {
                    case '{':
                        return ParseObject();

                    case '[':
                        return ParseArray();

                    case '"':
                        return ParseString();

                    case 't':
                        Expect("true");
                        return true;

                    case 'f':
                        Expect("false");
                        return false;

                    case 'n':
                        Expect("null");
                        return null;

                    default:
                        return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                _index++; // '{'
                SkipWhitespace();

                if (!IsEnd && _text[_index] == '}')
                {
                    _index++;
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();

                    if (IsEnd || _text[_index] != '"')
                    {
                        throw Error("Ожидалось имя свойства в кавычках.");
                    }

                    string key = ParseString();

                    SkipWhitespace();
                    if (IsEnd || _text[_index] != ':') throw Error("Ожидалось ':'.");
                    _index++;

                    result[key] = ParseValue();

                    SkipWhitespace();
                    if (IsEnd) throw Error("Неожиданный конец объекта.");

                    if (_text[_index] == ',')
                    {
                        _index++;
                        continue;
                    }

                    if (_text[_index] == '}')
                    {
                        _index++;
                        return result;
                    }

                    throw Error("Ожидалось ',' или '}'.");
                }
            }

            private List<object> ParseArray()
            {
                var result = new List<object>();

                _index++; // '['
                SkipWhitespace();

                if (!IsEnd && _text[_index] == ']')
                {
                    _index++;
                    return result;
                }

                while (true)
                {
                    result.Add(ParseValue());

                    SkipWhitespace();
                    if (IsEnd) throw Error("Неожиданный конец массива.");

                    if (_text[_index] == ',')
                    {
                        _index++;
                        continue;
                    }

                    if (_text[_index] == ']')
                    {
                        _index++;
                        return result;
                    }

                    throw Error("Ожидалось ',' или ']'.");
                }
            }

            private string ParseString()
            {
                var builder = new StringBuilder();

                _index++; // открывающая кавычка

                while (true)
                {
                    if (IsEnd) throw Error("Незавершённая строка.");

                    char current = _text[_index++];

                    if (current == '"') return builder.ToString();

                    if (current != '\\')
                    {
                        builder.Append(current);
                        continue;
                    }

                    if (IsEnd) throw Error("Незавершённая escape-последовательность.");

                    char escaped = _text[_index++];

                    switch (escaped)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;

                        case 'u':
                            if (_index + 4 > _text.Length) throw Error("Некорректный \\u escape.");
                            string hex = _text.Substring(_index, 4);
                            _index += 4;

                            int code;
                            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                            {
                                throw Error("Некорректный \\u escape.");
                            }

                            builder.Append((char)code);
                            break;

                        default:
                            throw Error("Неизвестная escape-последовательность '\\" + escaped + "'.");
                    }
                }
            }

            private double ParseNumber()
            {
                int start = _index;

                while (!IsEnd && IsNumberChar(_text[_index])) _index++;

                if (start == _index) throw Error("Ожидалось значение.");

                string text = _text.Substring(start, _index - start);

                double value;
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    throw Error("Некорректное число '" + text + "'.");
                }

                return value;
            }

            private static bool IsNumberChar(char c)
            {
                return (c >= '0' && c <= '9')
                       || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E';
            }

            private void Expect(string literal)
            {
                if (_index + literal.Length > _text.Length
                    || string.CompareOrdinal(_text, _index, literal, 0, literal.Length) != 0)
                {
                    throw Error("Ожидалось '" + literal + "'.");
                }

                _index += literal.Length;
            }

            private FormatException Error(string message)
            {
                return new FormatException(message + " Позиция " + _index + ".");
            }
        }
    }
}
