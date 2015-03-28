using Dapper;
using NBatch.Main.Common;
using NBatch.Main.Core;
using System.Collections.Generic;

namespace NBatch.Main.Readers.SqlReader
{
    public sealed class SqlDbItemReader<T> : IReader<T> where T : class
    {
        private readonly IDb _db;
        private string _sql;

        public SqlDbItemReader(string connectionStringName)
            : this(new Db(connectionStringName))
        {
        }

        internal SqlDbItemReader(IDb db)
        {
            _sql = @"{0} OFFSET((@PageNumber-1) * @RowsPerPage) Rows FETCH NEXT @RowsPerPage ROWS ONLY";
            _db = db;
        }

        public SqlDbItemReader<T> SetSql(string sql)
        {
            _sql = string.Format(_sql, sql);
            return this;
        }

        public IEnumerable<T> Read(long startIndex, int chunkSize)
        {
            long pageNumber = (startIndex / chunkSize) + 1;
            var items = _db.ExecuteQuery((conn, tx) => conn.Query<T>(_sql, new { PageNumber = pageNumber, RowsPerPage = chunkSize }, tx));
            return items;
        }
    }
}