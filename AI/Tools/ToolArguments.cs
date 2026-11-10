using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CivilGPT.AI.Tools
{
    /// <summary>
    /// Безопасное чтение аргументов инструмента из JSON.
    /// LLM может не передать поле или передать значение неверного типа —
    /// все методы возвращают false вместо исключения.
    /// </summary>
    internal static class ToolArguments
    {
        private static bool IsObject(JsonElement args)
        {
            return args.ValueKind == JsonValueKind.Object;
        }

        public static bool TryGetDouble(JsonElement args, string name, out double value)
        {
            value = 0.0;

            if (!IsObject(args)) return false;
            if (!args.TryGetProperty(name, out JsonElement element)) return false;
            if (element.ValueKind != JsonValueKind.Number) return false;

            return element.TryGetDouble(out value);
        }

        public static double GetDouble(JsonElement args, string name, double defaultValue)
        {
            double value;
            return TryGetDouble(args, name, out value) ? value : defaultValue;
        }

        public static string GetString(JsonElement args, string name, string defaultValue = null)
        {
            if (!IsObject(args)) return defaultValue;
            if (!args.TryGetProperty(name, out JsonElement element)) return defaultValue;
            if (element.ValueKind != JsonValueKind.String) return defaultValue;

            string value = element.GetString();
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        public static bool GetBool(JsonElement args, string name, bool defaultValue)
        {
            if (!IsObject(args)) return defaultValue;
            if (!args.TryGetProperty(name, out JsonElement element)) return defaultValue;

            if (element.ValueKind == JsonValueKind.True) return true;
            if (element.ValueKind == JsonValueKind.False) return false;

            return defaultValue;
        }

        public static bool TryGetUIntList(JsonElement args, string name, out List<uint> values)
        {
            values = null;

            if (!IsObject(args)) return false;
            if (!args.TryGetProperty(name, out JsonElement element)) return false;
            if (element.ValueKind != JsonValueKind.Array) return false;
            if (element.GetArrayLength() == 0) return false;

            var result = new List<uint>();

            foreach (JsonElement item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number) return false;

                uint number;
                if (!item.TryGetUInt32(out number)) return false;

                result.Add(number);
            }

            values = result;
            return true;
        }
    }
}
