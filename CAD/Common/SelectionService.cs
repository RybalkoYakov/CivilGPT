using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CivilGPT.CAD.Interfaces;

namespace CivilGPT.CAD.Common
{
    /// <summary>
    /// Получение выделения из редактора AutoCAD.
    /// </summary>
    public class SelectionService : ISelectionService
    {
        private readonly Editor _editor;

        public SelectionService(Editor editor)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        }

        public static SelectionService FromActiveDocument()
        {
            Editor editor = CadApplication.ActiveEditor;
            if (editor == null)
            {
                throw new InvalidOperationException("Нет активного документа AutoCAD.");
            }
            return new SelectionService(editor);
        }

        public ObjectId[] GetSelection(string message, params string[] dxfTypes)
        {
            var options = new PromptSelectionOptions { MessageForAdding = message };
            SelectionFilter filter = EditorService.BuildFilter(dxfTypes);

            PromptSelectionResult result = filter != null
                ? _editor.GetSelection(options, filter)
                : _editor.GetSelection(options);

            return result.Status == PromptStatus.OK
                ? result.Value.GetObjectIds()
                : new ObjectId[0];
        }

        public ObjectId[] GetImpliedSelection()
        {
            PromptSelectionResult result = _editor.SelectImplied();

            return result.Status == PromptStatus.OK && result.Value != null
                ? result.Value.GetObjectIds()
                : new ObjectId[0];
        }

        public ObjectId[] GetSelectionOrImplied(string message, params string[] dxfTypes)
        {
            ObjectId[] implied = GetImpliedSelection();
            return implied.Length > 0 ? implied : GetSelection(message, dxfTypes);
        }

        /// <summary>
        /// Отфильтровать набор ObjectId по DXF-типу без обращения к пользователю.
        /// </summary>
        public static List<ObjectId> FilterByType(
            Transaction transaction,
            IEnumerable<ObjectId> ids,
            params string[] dxfTypes)
        {
            var result = new List<ObjectId>();
            if (transaction == null || ids == null) return result;

            var allowed = new HashSet<string>(dxfTypes ?? new string[0], StringComparer.OrdinalIgnoreCase);

            foreach (ObjectId id in ids)
            {
                if (!id.IsValid) continue;

                DBObject obj = transaction.GetObject(id, OpenMode.ForRead, false);
                if (obj == null) continue;

                if (allowed.Count == 0)
                {
                    result.Add(id);
                    continue;
                }

                // Сравниваем по DXF-имени класса (LINE, LWPOLYLINE, TEXT…),
                // а не по имени CLR-типа — они не совпадают.
                string dxfName = obj.GetRXClass()?.DxfName;
                if (dxfName != null && allowed.Contains(dxfName))
                {
                    result.Add(id);
                }
            }

            return result;
        }
    }
}
