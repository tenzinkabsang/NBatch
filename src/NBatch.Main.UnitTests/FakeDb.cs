using System;
using System.Data;
using System.Data.SqlClient;
using NBatch.Main.Common;

namespace NBatch.Main.UnitTests
{
    class FakeDb : IDb
    {
        public bool ExecuteCalled;
        public T ExecuteQuery<T>(Func<SqlCommand, T> operation)
        {
            ExecuteCalled = true;
            return default (T);
        }

        public T ExecuteQuery<T>(Func<IDbConnection, IDbTransaction, T> operation)
        {
            ExecuteCalled = true;
            return default (T);
        }
    }
}