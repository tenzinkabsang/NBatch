using NBatch.Core.ItemWriter;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Dapper;

namespace NBatch.Core.Writer.SqlWriter
{
    public class SqlDbItemWriter<TItem> : IWriter<TItem>
    {
        private readonly string _conn;
        private string _sql;

        public SqlDbItemWriter(string connectionStringName)
        {
            _conn = ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString;
        }

        public bool Write(IEnumerable<TItem> items)
        {
            var itemsToInsert = items != null ? items.ToArray() : Enumerable.Empty<TItem>().ToArray();
            int result = BulkInsertContacts(itemsToInsert);
            return result == itemsToInsert.Length;
        }

        public SqlDbItemWriter<TItem> SetSql(string sql)
        {
            _sql = sql;
            return this;
        }

        private int BulkInsertContacts(params TItem[] items)
        {
            using (var conn = new SqlConnection(_conn))
            {
                conn.Open();
                using (var tranx = conn.BeginTransaction())
                {
                    try
                    {
                        int result = conn.Execute(_sql, items, tranx);
                        tranx.Commit();
                        return result;
                    }
                    catch (Exception)
                    {
                        tranx.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
