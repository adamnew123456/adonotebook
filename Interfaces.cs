using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;


namespace ADONotebook
{
    public class TableMetadata
    {
        public string Catalog { get; private set; }
        public string Table { get; private set; }

        public TableMetadata(string catalog, string table)
        {
            Catalog = catalog;
            Table = table;
        }
    }

    public class ColumnMetadata
    {
        public string Catalog { get; private set; }
        public string Table { get; private set; }
        public string Column { get; private set; }
        public string DataType { get; private set; }

        public ColumnMetadata(string catalog, string table, string column, string datatype)
        {
            Catalog = catalog;
            Table = table;
            Column = column;
            DataType = datatype;
        }
    }

    public interface QueryInput
    {
        QueryExecutor Executor {get; set;}

        // The main loop, which passes user input into the executor
        void Run();
    }

    public interface QueryExecutor : IDisposable
    {
        QueryOutput Output {get; set;}

        // Manages the lifetime of the underlying connection
        void Open();
        void Close();

        // Executes the query, and passes results into the output
        void ProcessQuery(string sql);

        // Gets the tables from the data source
        List<TableMetadata> Tables();

        // Gets the views from the data source
        List<TableMetadata> Views();

        // Gets the tables/views from the data source for the given entity
        List<ColumnMetadata> Columns();
    }

    public interface QueryOutput
    {
        // Displays an error message from the provider
        void DisplayError(string message);

        // Displays the number of affected records, for queries that don't
        // produce results
        void DisplayResultCount(int count);

        // Displays the column metadata for a result
        void DisplayColumns(DataColumn[] columns);

        // Displays a page of result records, and prompts for whether to
        // display the next page
        bool DisplayPage(DataTable table);

        // Displays the last page of result records, without prompting
        // for continuation.
        void DisplayLastPage(DataTable table);
    }
}
