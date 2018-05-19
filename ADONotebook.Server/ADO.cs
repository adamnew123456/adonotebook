using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;

using Newtonsoft.Json;

namespace ADONotebook
{
    public class TableMetadata
    {
        [JsonProperty("catalog")]
        public string Catalog { get; private set; }

        [JsonProperty("table")]
        public string Table { get; private set; }

        public TableMetadata(string catalog, string table)
        {
            Catalog = catalog;
            Table = table;
        }
    }

    public class ColumnMetadata
    {
        [JsonProperty("catalog")]
        public string Catalog { get; private set; }

        [JsonProperty("table")]
        public string Table { get; private set; }

        [JsonProperty("column")]
        public string Column { get; private set; }

        [JsonProperty("datatype")]
        public string DataType { get; private set; }

        public ColumnMetadata(string catalog, string table, string column, string datatype)
        {
            Catalog = catalog;
            Table = table;
            Column = column;
            DataType = datatype;
        }
    }

    public class ADORequestPaginator
    {
        public DataColumn[] Columns { get; private set; }
        public int ResultCount;

        private IDataReader Reader;
        private DataTable CurrentPage;
        private int PageSize = 100;

        public ADORequestPaginator(IDataReader reader)
        {
            Reader = reader;

            CurrentPage = new DataTable();
            ReadColumnMetadata();
            ReadResultCount();
        }

        private void ReadColumnMetadata()
        {
            for (var i = 0; i < Reader.FieldCount; i++)
            {
                var column = new DataColumn(Reader.GetName(i), Reader.GetFieldType(i));
                CurrentPage.Columns.Add(column);
            }

            Columns = new DataColumn[CurrentPage.Columns.Count];
            CurrentPage.Columns.CopyTo(Columns, 0);
        }

        private void ReadResultCount()
        {
            if (CurrentPage.Columns.Count == 0)
            {
                ResultCount = Reader.RecordsAffected;
            }
            else
            {
                ResultCount = -1;
            }
        }

        /// <summary>
        ///   Returns a page of results from the data source, possibly empty.
        /// </summary>
        public DataTable NextPage()
        {
            while (Reader.Read())
            {
                if (CurrentPage.Rows.Count == PageSize)
                {
                    break;
                }

                var row = CurrentPage.NewRow();
                for (int i = 0; i < Columns.Length; i++)
                {
                    row[i] = Reader[i];
                }

                CurrentPage.Rows.Add(row);
            }

            var result = CurrentPage.Copy();
            CurrentPage.Clear();
            return result;
        }

        /// <summary>
        ///   Closes the current request to the data source.
        /// </summary>
        public void Close()
        {
            Reader.Close();
        }
    }

    public abstract class ADOQueryExecutor
    {
        protected IDbConnection Connection;

        abstract public void Open();

        /// <summary>
        ///   Closes the current connection (which must have been Open()'d first.
        /// </summary>
        public void Close()
        {
            if (Connection != null)
            {
                Connection.Close();
            }
        }

        /// <summary>
        ///   Executes a single query against the connection, and returns a
        ///   result paginator.
        /// </summary>
        public ADORequestPaginator Execute(string sql)
        {
            var command = Connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = sql;

            var reader = command.ExecuteReader();
            return new ADORequestPaginator(reader);
        }

        /// <summary>
        ///   Gets a list of tables from the data source.
        /// </summary>
        public List<TableMetadata> Tables()
        {
            var commonConnection = Connection as DbConnection;
            var dataTable = commonConnection.GetSchema("Tables");

            var tables = new List<TableMetadata>();
            foreach (DataRow row in dataTable.Rows)
            {
                var entry = new TableMetadata(row["TABLE_CATALOG"] as string,
                                              row["TABLE_NAME"] as string);
                tables.Add(entry);
            }
            return tables;
        }

        /// <summary>
        ///   Gets a list of views from the data source.
        /// </summary>
        public List<TableMetadata> Views()
        {
            var commonConnection = Connection as DbConnection;
            var dataTable = commonConnection.GetSchema("Views");

            var tables = new List<TableMetadata>();
            foreach (DataRow row in dataTable.Rows)
            {
                var entry = new TableMetadata(row["TABLE_CATALOG"] as string,
                                              row["TABLE_NAME"] as string);
                tables.Add(entry);
            }
            return tables;
        }

        /// <summary>
        ///   Gets a list of columns from the data source.
        /// </summary>
        public List<ColumnMetadata> Columns()
        {
            var commonConnection = Connection as DbConnection;
            var dataTable = commonConnection.GetSchema("Columns");

            var columns = new List<ColumnMetadata>();
            foreach (DataRow row in dataTable.Rows)
            {
                var entry = new ColumnMetadata(row["TABLE_CATALOG"] as string,
                                                row["TABLE_NAME"] as string,
                                                row["COLUMN_NAME"] as string,
                                                row["DATA_TYPE"] as string);
                columns.Add(entry);
            }
            return columns;
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
