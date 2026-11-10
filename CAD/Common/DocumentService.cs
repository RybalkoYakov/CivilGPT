using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace CivilGPT.CAD.Common
{
    /// <summary>
    /// Работа с активным документом: база данных, редактор, пространство модели, блокировка документа.
    /// </summary>
    public class DocumentService
    {
        private readonly Document _document;

        public DocumentService(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public static DocumentService FromActiveDocument()
        {
            Document document = CadApplication.ActiveDocument;
            if (document == null)
            {
                throw new InvalidOperationException("Нет активного документа AutoCAD.");
            }
            return new DocumentService(document);
        }

        public Document Document => _document;

        public Database Database => _document.Database;

        public Editor Editor => _document.Editor;

        /// <summary>Пространство модели текущего чертежа.</summary>
        public ObjectId ModelSpaceId => SymbolUtilityServices.GetBlockModelSpaceId(Database);

        /// <summary>Текущее пространство (модель или лист).</summary>
        public ObjectId CurrentSpaceId => Database.CurrentSpaceId;

        /// <summary>
        /// Блокировка документа на время записи.
        /// Обязательна при изменении чертежа из немодального контекста (панель, ИИ).
        /// </summary>
        public DocumentLock LockDocument()
        {
            return _document.LockDocument();
        }

        /// <summary>Перечисление всех объектов указанного пространства.</summary>
        public IEnumerable<ObjectId> GetEntityIds(Transaction transaction, ObjectId spaceId)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));

            var blockTableRecord =
                transaction.GetObject(spaceId, OpenMode.ForRead) as BlockTableRecord;

            if (blockTableRecord == null)
            {
                yield break;
            }

            foreach (ObjectId id in blockTableRecord)
            {
                yield return id;
            }
        }

        public IEnumerable<ObjectId> GetModelSpaceEntityIds(Transaction transaction)
        {
            return GetEntityIds(transaction, ModelSpaceId);
        }

        public T GetObject<T>(Transaction transaction, ObjectId id, OpenMode mode)
            where T : DBObject
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (id.IsNull || !id.IsValid) return null;

            return transaction.GetObject(id, mode) as T;
        }
    }
}
