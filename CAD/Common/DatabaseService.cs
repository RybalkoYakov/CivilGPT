using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace CivilGPT.CAD.Common
{
    /// <summary>
    /// Операции уровня базы данных чертежа: транзакции, таблицы символов, проверка режима записи.
    /// </summary>
    public class DatabaseService
    {
        private readonly Database _database;

        public DatabaseService(Database database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public Database Database => _database;

        public ObjectId ModelSpaceId => SymbolUtilityServices.GetBlockModelSpaceId(_database);

        public ObjectId PaperSpaceId => SymbolUtilityServices.GetBlockPaperSpaceId(_database);

        public ObjectId BlockTableId => _database.BlockTableId;

        public ObjectId LayerTableId => _database.LayerTableId;

        public ObjectId TextStyleTableId => _database.TextStyleTableId;

        public Transaction StartTransaction()
        {
            return _database.TransactionManager.StartTransaction();
        }

        /// <summary>
        /// Имя записи блока по её ObjectId (например, "MODEL_SPACE" или имя блока).
        /// </summary>
        public string GetBlockName(Transaction transaction, ObjectId blockTableRecordId)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));

            var record = transaction.GetObject(blockTableRecordId, OpenMode.ForRead) as BlockTableRecord;
            return record?.Name;
        }
    }
}
