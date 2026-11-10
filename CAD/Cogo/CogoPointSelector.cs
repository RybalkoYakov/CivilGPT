using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CivilGPT.CAD.Cogo
{
    /// <summary>
    /// Отбор точек COGO для обработки: по выделению, по номерам точек.
    /// </summary>
    public static class CogoPointSelector
    {
        /// <summary>DXF-имя объекта «точка COGO».</summary>
        public const string CogoPointDxfType = "AECC_COGO_POINT";

        /// <summary>
        /// Оставить только точки COGO.
        /// </summary>
        public static List<ObjectId> FilterCogoPoints(Transaction transaction, IEnumerable<ObjectId> ids)
        {
            var result = new List<ObjectId>();

            if (transaction == null || ids == null) return result;

            foreach (ObjectId id in ids)
            {
                if (!id.IsValid) continue;

                var point = transaction.GetObject(id, OpenMode.ForRead, false) as CogoPoint;
                if (point != null)
                {
                    result.Add(id);
                }
            }

            return result;
        }

        /// <summary>
        /// Найти точки COGO по их номерам.
        /// </summary>
        public static List<ObjectId> ByNumbers(
            Transaction transaction,
            CivilDocument civilDocument,
            IEnumerable<uint> numbers)
        {
            var result = new List<ObjectId>();

            if (transaction == null || civilDocument == null || numbers == null) return result;

            var wanted = new HashSet<uint>(numbers);

            foreach (ObjectId cogoId in civilDocument.CogoPoints)
            {
                var point = transaction.GetObject(cogoId, OpenMode.ForRead, false) as CogoPoint;
                if (point != null && wanted.Contains(point.PointNumber))
                {
                    result.Add(cogoId);
                }
            }

            return result;
        }

        /// <summary>
        /// Ранее выбранные (implied) точки COGO. Пустой массив, если выделения нет.
        /// </summary>
        public static ObjectId[] GetImpliedPointIds(Editor editor)
        {
            if (editor == null) return new ObjectId[0];

            PromptSelectionResult selection = editor.SelectImplied();

            return selection.Status == PromptStatus.OK && selection.Value != null
                ? selection.Value.GetObjectIds()
                : new ObjectId[0];
        }

        /// <summary>
        /// Все точки COGO текущего документа.
        /// </summary>
        public static List<ObjectId> All(CivilDocument civilDocument)
        {
            var result = new List<ObjectId>();
            if (civilDocument == null) return result;

            foreach (ObjectId id in civilDocument.CogoPoints)
            {
                result.Add(id);
            }

            return result;
        }

        /// <summary>Имя точки для отчёта.</summary>
        public static string Describe(Transaction transaction, ObjectId id)
        {
            var point = transaction.GetObject(id, OpenMode.ForRead, false) as CogoPoint;
            if (point == null) return id.ToString();

            return $"№{point.PointNumber}";
        }
    }
}
