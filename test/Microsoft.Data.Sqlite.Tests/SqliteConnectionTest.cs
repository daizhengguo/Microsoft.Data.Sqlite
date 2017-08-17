﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Microsoft.Data.Sqlite.Properties;
using SQLitePCL;
using Xunit;

namespace Microsoft.Data.Sqlite
{
    public class SqliteConnectionTest
    {
        [Fact]
        public void Ctor_sets_connection_string()
        {
            var connectionString = "Data Source=test.db";

            var connection = new SqliteConnection(connectionString);

            Assert.Equal(connectionString, connection.ConnectionString);
        }

        [Fact]
        public void ConnectionString_setter_throws_when_open()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var ex = Assert.Throws<InvalidOperationException>(() => connection.ConnectionString = "Data Source=test.db");

                Assert.Equal(Resources.ConnectionStringRequiresClosedConnection, ex.Message);
            }
        }

        [Fact]
        public void ConnectionString_gets_and_sets_value()
        {
            var connection = new SqliteConnection();
            var connectionString = "Data Source=test.db";

            connection.ConnectionString = connectionString;

            Assert.Equal(connectionString, connection.ConnectionString);
        }

        [Fact]
        public void Database_returns_value()
        {
            var connection = new SqliteConnection();

            Assert.Equal("main", connection.Database);
        }

        [Fact]
        public void DataSource_returns_connection_string_data_source_when_closed()
        {
            var connection = new SqliteConnection("Data Source=test.db");

            Assert.Equal("test.db", connection.DataSource);
        }

        [Fact]
        public void DataSource_returns_actual_filename_when_open()
        {
            using (var connection = new SqliteConnection("Data Source=test.db"))
            {
                connection.Open();

                var result = connection.DataSource;

                Assert.True(Path.IsPathRooted(result));
                Assert.Equal("test.db", Path.GetFileName(result));
            }
        }

        [Fact]
        public void ServerVersion_returns_value()
        {
            var connection = new SqliteConnection();

            var version = connection.ServerVersion;

            Assert.StartsWith("3.", version);
        }

        [Fact]
        public void State_closed_by_default()
        {
            var connection = new SqliteConnection();

            Assert.Equal(ConnectionState.Closed, connection.State);
        }

        [Fact]
        public void Open_throws_when_no_connection_string()
        {
            var connection = new SqliteConnection();

            var ex = Assert.Throws<InvalidOperationException>(() => connection.Open());

            Assert.Equal(Resources.OpenRequiresSetConnectionString, ex.Message);
        }

        [Fact]
        public void Open_adjusts_relative_path()
        {
            using (var connection = new SqliteConnection("Data Source=local.db"))
            {
                connection.Open();

                Assert.Equal(Path.Combine(AppContext.BaseDirectory, "local.db"), connection.DataSource);
            }
        }

        [Fact]
        public void Open_throws_when_error()
        {
            using (var connection = new SqliteConnection("Data Source=file:data.db?mode=invalidmode"))
            {
                var ex = Assert.Throws<SqliteException>(() => connection.Open());

                Assert.Equal(raw.SQLITE_ERROR, ex.SqliteErrorCode);
            }
        }

        [Fact]
        public void Open_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var raised = false;
                StateChangeEventHandler handler = (sender, e) =>
                    {
                        raised = true;

                        Assert.Equal(connection, sender);
                        Assert.Equal(ConnectionState.Closed, e.OriginalState);
                        Assert.Equal(ConnectionState.Open, e.CurrentState);
                    };

                connection.StateChange += handler;
                try
                {
                    connection.Open();

                    Assert.True(raised);
                    Assert.Equal(ConnectionState.Open, connection.State);
                }
                finally
                {
                    connection.StateChange -= handler;
                }
            }
        }

        [Fact]
        public void Open_works_when_readonly()
        {
            using (var connection = new SqliteConnection("Data Source=readonly.db"))
            {
                connection.Open();

                connection.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS Idomic (Word TEXT);");
            }

            using (var connection = new SqliteConnection("Data Source=readonly.db;Mode=ReadOnly"))
            {
                connection.Open();

                var ex = Assert.Throws<SqliteException>(
                    () => connection.ExecuteNonQuery("INSERT INTO Idomic VALUES ('arimfexendrapuse');"));

                Assert.Equal(raw.SQLITE_READONLY, ex.SqliteErrorCode);
            }
        }

        [Fact]
        public void Open_works_when_readwrite()
        {
            using (var connection = new SqliteConnection("Data Source=readwrite.db;Mode=ReadWrite"))
            {
                var ex = Assert.Throws<SqliteException>(() => connection.Open());

                Assert.Equal(raw.SQLITE_CANTOPEN, ex.SqliteErrorCode);
            }
        }

        [Fact]
        public void Open_works_when_memory_shared()
        {
            var connectionString = "Data Source=people;Mode=Memory;Cache=Shared";

            using (var connection1 = new SqliteConnection(connectionString))
            {
                connection1.Open();

                connection1.ExecuteNonQuery(
                    "CREATE TABLE Person (Name TEXT);" +
                    "INSERT INTO Person VALUES ('Waldo');");

                using (var connection2 = new SqliteConnection(connectionString))
                {
                    connection2.Open();

                    var name = connection2.ExecuteScalar<string>("SELECT Name FROM Person;");
                    Assert.Equal("Waldo", name);
                }
            }
        }

        [Fact]
        public void Open_works_when_uri()
        {
            using (var connection = new SqliteConnection("Data Source=file:readwrite.db?mode=rw"))
            {
                var ex = Assert.Throws<SqliteException>(() => connection.Open());

                Assert.Equal(raw.SQLITE_CANTOPEN, ex.SqliteErrorCode);
            }
        }

        [Fact]
        public void Close_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var raised = false;
                StateChangeEventHandler handler = (sender, e) =>
                    {
                        raised = true;

                        Assert.Equal(connection, sender);
                        Assert.Equal(ConnectionState.Open, e.OriginalState);
                        Assert.Equal(ConnectionState.Closed, e.CurrentState);
                    };

                connection.StateChange += handler;
                try
                {
                    connection.Close();

                    Assert.True(raised);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
                finally
                {
                    connection.StateChange -= handler;
                }
            }
        }

        [Fact]
        public void Close_can_be_called_before_open()
        {
            var connection = new SqliteConnection();

            connection.Close();
        }

        [Fact]
        public void Close_can_be_called_more_than_once()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                connection.Close();
                connection.Close();
            }
        }

        [Fact]
        public void Dispose_closes_connection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();

            connection.Dispose();

            Assert.Equal(ConnectionState.Closed, connection.State);
        }

        [Fact]
        public void Dispose_can_be_called_more_than_once()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();

            connection.Dispose();
            connection.Dispose();
        }

        [Fact]
        public void CreateCommand_returns_command()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    var command = connection.CreateCommand();

                    Assert.NotNull(command);
                    Assert.Same(connection, command.Connection);
                    Assert.Same(transaction, command.Transaction);
                }
            }
        }

        [Fact]
        public void CreateCollation_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                connection.CreateCollation("MY_NOCASE", (s1, s2) => string.Compare(s1, s2, StringComparison.OrdinalIgnoreCase));

                Assert.Equal(1L, connection.ExecuteScalar<long>("SELECT 'Νικοσ' = 'ΝΙΚΟΣ' COLLATE MY_NOCASE;"));
            }
        }

        [Fact]
        public void CreateCollation_with_null_comparer_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                connection.CreateCollation("MY_NOCASE", (s1, s2) => string.Compare(s1, s2, StringComparison.OrdinalIgnoreCase));
                connection.CreateCollation("MY_NOCASE", null);

                var ex = Assert.Throws<SqliteException>(
                    () => connection.ExecuteScalar<long>("SELECT 'Νικοσ' = 'ΝΙΚΟΣ' COLLATE MY_NOCASE;"));

                Assert.Equal(raw.SQLITE_ERROR, ex.SqliteErrorCode);
            }
        }

        [Fact]
        public void CreateCollation_throws_when_closed()
        {
            var connection = new SqliteConnection();

            var ex = Assert.Throws<InvalidOperationException>(() => connection.CreateCollation("NOCOL", (s1, s2) => -1));

            Assert.Equal(Resources.CallRequiresOpenConnection("CreateCollation"), ex.Message);
        }

        [Fact]
        public void CreateCollation_throws_with_empty_name()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                var ex = Assert.Throws<ArgumentNullException>(() => connection.CreateCollation(null, null));

                Assert.Equal("name", ex.ParamName);
            }
        }

        [Fact]
        public void CreateCollation_works_with_state()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                var list = new List<string>();
                connection.CreateCollation<List<string>>(
                    "MY_NOCASE",
                    list,
                    (l, s1, s2) =>
                    {
                        l.Add("Invoked");
                        return string.Compare(s1, s2, StringComparison.OrdinalIgnoreCase);
                    });

                Assert.Equal(1L, connection.ExecuteScalar<long>("SELECT 'Νικοσ' = 'ΝΙΚΟΣ' COLLATE MY_NOCASE;"));
                var item = Assert.Single(list);
                Assert.Equal("Invoked", item);
            }
        }

        [Fact]
        public void BeginTransaction_throws_when_closed()
        {
            var connection = new SqliteConnection();

            var ex = Assert.Throws<InvalidOperationException>(() => connection.BeginTransaction());

            Assert.Equal(Resources.CallRequiresOpenConnection("BeginTransaction"), ex.Message);
        }

        [Fact]
        public void BeginTransaction_throws_when_parallel_transaction()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (connection.BeginTransaction())
                {
                    var ex = Assert.Throws<InvalidOperationException>(() => connection.BeginTransaction());

                    Assert.Equal(Resources.ParallelTransactionsNotSupported, ex.Message);
                }
            }
        }

        [Fact]
        public void BeginTransaction_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    Assert.NotNull(transaction);
                    Assert.Equal(connection, transaction.Connection);
                    Assert.Equal(IsolationLevel.Serializable, transaction.IsolationLevel);
                }
            }
        }

        [Fact]
        public void ChangeDatabase_not_supported()
        {
            using (var connection = new SqliteConnection())
            {
                Assert.Throws<NotSupportedException>(() => connection.ChangeDatabase("new"));
            }
        }

        [Fact]
        public void Mars_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var command1 = connection.CreateCommand();
                command1.CommandText = "SELECT '1A' UNION SELECT '1B';";

                using (var reader1 = command1.ExecuteReader())
                {
                    reader1.Read();
                    Assert.Equal("1A", reader1.GetString(0));

                    var command2 = connection.CreateCommand();
                    command2.CommandText = "SELECT '2A' UNION SELECT '2B';";

                    using (var reader2 = command2.ExecuteReader())
                    {
                        reader2.Read();
                        Assert.Equal("2A", reader2.GetString(0));

                        reader1.Read();
                        Assert.Equal("1B", reader1.GetString(0));

                        reader2.Read();
                        Assert.Equal("2B", reader2.GetString(0));
                    }
                }
            }
        }

        [Fact]
        public void EnableExtensions_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var sql = "SELECT load_extension('unknown');";

                var ex = Assert.Throws<SqliteException>(() => connection.ExecuteNonQuery(sql));
                var originalError = ex.Message;

                connection.EnableExtensions();

                ex = Assert.Throws<SqliteException>(() => connection.ExecuteNonQuery(sql));
                var enabledError = ex.Message;

                connection.EnableExtensions(enable: false);

                ex = Assert.Throws<SqliteException>(() => connection.ExecuteNonQuery(sql));
                var disabledError = ex.Message;

                Assert.NotEqual(originalError, enabledError);
                Assert.Equal(originalError, disabledError);
            }
        }

        [Fact]
        public void EnableExtensions_throws_when_closed()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var ex = Assert.Throws<InvalidOperationException>(() => connection.EnableExtensions());
                Assert.Equal(Resources.CallRequiresOpenConnection("EnableExtensions"), ex.Message);
            }
        }
    }
}
