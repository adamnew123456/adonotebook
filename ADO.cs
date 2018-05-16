using System;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;

namespace ADONotebook
{
    public abstract class ADOQueryExecutor : QueryExecutor
    {
        public QueryOutput Output { get; set; }

        protected IDbConnection Connection;
        private readonly int PageSize = 100;

        abstract public void Open();

        /// <summary>
        ///   Closes the current connection (which must have been Open()'d first.
        /// </summary>
        public void Close()
        {
            Connection.Close();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            Connection.Dispose();
        }

        /// <summary>
        ///   Builds an empty data table from the column metadata in a reader.
        /// </summary>
        private DataTable BuildDataTable(IDataReader reader)
        {
            var table = new DataTable();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var column = new DataColumn(reader.GetName(i), reader.GetFieldType(i));
                table.Columns.Add(column);
            }

            return table;
        }

        /// <summary>
        ///   Reads results from the reader, passing them to the output once an
        ///   entire page is built.
        /// </summary>
        private void PaginateResults(IDataReader reader)
        {
            var table = BuildDataTable(reader);
            while (reader.Read())
            {
                if (table.Rows.Count == PageSize)
                {
                    if (!Output.DisplayPage(table)) return;
                    table.Rows.Clear();
                }

                var row = table.NewRow();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = reader[i];
                }

                table.Rows.Add(row);
            }

            Output.DisplayLastPage(table);
        }

        /// <summary>
        ///   Executes a single query against the connection, and passes the
        ///   results to the output. The connection must have been opened first.
        /// </summary>
        public void ProcessQuery(string sql)
        {
            var command = Connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = sql;

            try
            {
                using (var reader = command.ExecuteReader())
                {
                    var table = BuildDataTable(reader);

                    if (reader.FieldCount == 0)
                    {
                        Output.DisplayResultCount(reader.RecordsAffected);
                    }
                    else
                    {
                        PaginateResults(reader);
                    }
                }
            }
            catch (Exception error)
            {
                Output.DisplayError(error.ToString());
            }
        }
    }

    /// <summary>
    ///   An executor based upon ADO.NET providers, that get access to a
    ///   provider using DbProviderFactories.
    /// </summary>
    public class ADOProviderFactoryExecutor : ADOQueryExecutor {
        private string ProviderInvariant;
        private string ConnectionString;

        public ADOProviderFactoryExecutor(string provider, string connectionString)
        {
            ProviderInvariant = provider;
            ConnectionString = connectionString;
        }

        /// <summary>
        ///   Opens a connection with the given provider and connection string.
        /// </summary>
        public override void Open()
        {
            var factory = DbProviderFactories.GetFactory(ProviderInvariant);
            Connection = factory.CreateConnection();
            Connection.ConnectionString = ConnectionString;
            Connection.Open();
        }
    }

    /// <summary>
    ///   An executor based upon ADO.NET providers, that get access to a
    ///   provider using reflection.
    /// </summary>
    public class ADOReflectionExecutor : ADOQueryExecutor {
        private string AssemblyFile;
        private string FactoryClass;
        private string ConnectionString;

        public ADOReflectionExecutor(string assembly, string factoryClass, string connectionString)
        {
            AssemblyFile = assembly;
            FactoryClass = factoryClass;
            ConnectionString = connectionString;
        }

        /// <summary>
        ///   Opens a connection with the given provider and connection string.
        /// </summary>
        public override void Open()
        {
            var providerAssembly = Assembly.LoadFile(AssemblyFile);
            var factoryClass = providerAssembly.GetType(FactoryClass);
            var factoryInstance = Activator.CreateInstance(factoryClass) as DbProviderFactory;
            Connection = factoryInstance.CreateConnection();
            Connection.ConnectionString = ConnectionString;
            Connection.Open();
        }
    }
}
