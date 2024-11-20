using System;
using Dapper;
using NBatch.Main.Common;
using NBatch.Main.Core;
using System.Collections.Generic;

namespace NBatch.Main.Readers.SqlReader;

public sealed class SqlDbItemReader<T> : IReader<T>
{
    private readonly IDb _db;
    private string _query;
    private string _column;
    private const string Pagination = "OFFSET((@PageNumber-1) * @RowsPerPage) Rows FETCH NEXT @RowsPerPage ROWS ONLY";

    public SqlDbItemReader(string connectionStringName)
        : this(new Db(connectionStringName))
    {
    }

    internal SqlDbItemReader(IDb db)
    {
        _db = db;
    }

    public SqlDbItemReader<T> Query(string query)
    {
        _query = query;
        return this;
    }

    public SqlDbItemReader<T> OrderBy(string column)
    {
        if(string.IsNullOrEmpty(_query))
            throw new Exception("Query must be defined first.");
        _column = column;
        return this;
    }

    public IEnumerable<T> Read(long startIndex, int chunkSize)
    {
        if(string.IsNullOrEmpty(_column))
            throw new Exception("Missing order by clause. Please define a column to order by.");

        string queryWithPagination = string.Format("{0} ORDER BY {1} {2}", _query, _column, Pagination);

        long pageNumber = (startIndex / chunkSize) + 1;
        var items = _db.ExecuteQuery((conn, tx) => conn.Query<T>(queryWithPagination, new { PageNumber = pageNumber, RowsPerPage = chunkSize }, tx));
        return items;
    }
}