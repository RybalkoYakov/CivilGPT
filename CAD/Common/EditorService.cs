using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace CivilGPT.CAD.Common
{
    /// <summary>
    /// Ввод-вывод в командной строке AutoCAD: сообщения, запросы значений, подтверждения.
    /// </summary>
    public class EditorService
    {
        private readonly Editor _editor;

        public EditorService(Editor editor)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        }

        public static EditorService FromActiveDocument()
        {
            Document document = CadApplication.ActiveDocument;
            if (document == null)
            {
                throw new InvalidOperationException("Нет активного документа AutoCAD.");
            }
            return new EditorService(document.Editor);
        }

        public Editor Editor => _editor;

        public void WriteMessage(string message)
        {
            _editor.WriteMessage(message ?? string.Empty);
        }

        public void WriteLine(string message)
        {
            _editor.WriteMessage("\n" + (message ?? string.Empty));
        }

        public void WriteError(string message)
        {
            _editor.WriteMessage("\n[Ошибка] " + (message ?? string.Empty));
        }

        /// <summary>
        /// Запрос числа с значением по умолчанию (Enter — принять умолчание).
        /// Возвращает null, если пользователь отменил ввод.
        /// </summary>
        public double? GetDouble(string message, double defaultValue, bool allowNegative = false)
        {
            var options = new PromptDoubleOptions(message)
            {
                AllowNegative = allowNegative,
                AllowZero = true,
                DefaultValue = defaultValue,
                UseDefaultValue = true
            };

            PromptDoubleResult result = _editor.GetDouble(options);
            return result.Status == PromptStatus.OK ? result.Value : (double?)null;
        }

        public string GetString(string message, bool allowSpaces = true)
        {
            var options = new PromptStringOptions(message)
            {
                AllowSpaces = allowSpaces
            };

            PromptResult result = _editor.GetString(options);
            return result.Status == PromptStatus.OK ? result.StringResult : null;
        }

        /// <summary>
        /// Выбор одного объекта. Возвращает ObjectId.Null, если выбор отменён.
        /// </summary>
        public ObjectId GetEntity(string message, bool allowNone = true)
        {
            var options = new PromptEntityOptions(message) { AllowNone = allowNone };
            PromptEntityResult result = _editor.GetEntity(options);

            return result.Status == PromptStatus.OK ? result.ObjectId : ObjectId.Null;
        }

        /// <summary>
        /// Запрос подтверждения (Y/N). Enter не подтверждает операцию.
        /// Список ключевых слов AutoCAD выводит сам, дублировать его в тексте не нужно.
        /// </summary>
        public bool AskConfirmation(string message)
        {
            var options = new PromptKeywordOptions(message + "\nПодтвердить?")
            {
                AllowNone = false
            };
            options.Keywords.Add("Yes", "Да", "Да");
            options.Keywords.Add("No", "Нет", "Нет");
            options.Keywords.Default = "No";

            PromptResult result = _editor.GetKeywords(options);
            return result.Status == PromptStatus.OK
                   && string.Equals(result.StringResult, "Yes", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Запрос выбора объектов с фильтром по DXF-типам.
        /// Возвращает пустой массив, если выбор отменён.
        /// </summary>
        public ObjectId[] GetSelection(string message, params string[] dxfTypes)
        {
            var options = new PromptSelectionOptions { MessageForAdding = message };

            PromptSelectionResult result =
                dxfTypes != null && dxfTypes.Length > 0
                    ? _editor.GetSelection(options, BuildFilter(dxfTypes))
                    : _editor.GetSelection(options);

            return result.Status == PromptStatus.OK
                ? result.Value.GetObjectIds()
                : new ObjectId[0];
        }

        /// <summary>
        /// Фильтр выбора по DXF-типам объектов.
        /// Несколько значений одного кода 0 трактуются AutoCAD как «ИЛИ».
        /// </summary>
        public static SelectionFilter BuildFilter(params string[] dxfTypes)
        {
            if (dxfTypes == null || dxfTypes.Length == 0) return null;

            var values = new List<TypedValue>();
            foreach (string type in dxfTypes)
            {
                if (string.IsNullOrWhiteSpace(type)) continue;
                values.Add(new TypedValue((int)DxfCode.Start, type));
            }

            return values.Count > 0 ? new SelectionFilter(values.ToArray()) : null;
        }
    }
}
