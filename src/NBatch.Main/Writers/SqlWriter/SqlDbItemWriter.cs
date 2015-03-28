using Dapper;
using NBatch.Main.Common;
using NBatch.Main.Core;
using System.Collections.Generic;
using System.Linq;

namespace NBatch.Main.Writers.SqlWriter
{
    public sealed class SqlDbItemWriter<TItem> : IWriter<TItem>
    {
        private string _sql;
        private readonly IDb _db;

        public SqlDbItemWriter(string connectionStringName)
            :this(new Db(connectionStringName))
        {
        }

        internal SqlDbItemWriter(IDb db)
        {
            _db = db;
        }

        public SqlDbItemWriter<TItem> SetSql(string sql)
        {
            _sql = sql;
            return this;
        }

        public bool Write(IEnumerable<TItem> items)
        {
            TItem[] itemsToInsert = items != null ? items.ToArray() : Enumerable.Empty<TItem>().ToArray();

            int result = _db.ExecuteQuery((conn, tx) => conn.Execute(_sql, itemsToInsert, tx));

            return result == itemsToInsert.Length;
        }
    }
}
