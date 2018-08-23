using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ADONotebook
{
    /// <summary>
    ///   The main class.
    /// </summary>
    public class Application
    {
        /// <summary>
        ///   Scans the data page to figure out how wide each column needs to
        ///   be to tabulate all the data.
        /// </summary>
        private static int[] ComputePrintingWidth(List<ReaderMetadata> columns, List<Dictionary<string, string>> page)
        {
            var maxLengths = new int[columns.Count];
            for (var i = 0; i < columns.Count; i++)
            {
                maxLengths[i] = Math.Max(maxLengths[i], columns[i].Column.Length);
            }

            for (var i = 0; i < columns.Count; i++)
            {
                maxLengths[i] = Math.Max(maxLengths[i], columns[i].DataType.Length);
            }

            foreach (var row in page)
            {
                for (var i = 0; i < columns.Count; i++)
                {
                    var entry = row[columns[i].Column] ?? "<null>";
                    maxLengths[i] = Math.Max(maxLengths[i], entry.Length);
                }
            }

            return maxLengths;
        }

        /// <summary>
        ///    Pretty-prints, in tabular form, a single page of database results.
        /// </summary>
        private static bool DisplayPage(List<ReaderMetadata> columns, List<Dictionary<string, string>> page, bool promptForContinuation)
        {
            var columnPadding = ComputePrintingWidth(columns, page);
            for (var i = 0; i < columns.Count; i++)
            {
                Console.Write(columns[i].Column.PadRight(columnPadding[i]));
                Console.Write(" ");
            }
            Console.WriteLine();

            for (var i = 0; i < columns.Count; i++)
            {
                Console.Write(columns[i].DataType.PadRight(columnPadding[i]));
                Console.Write(" ");
            }
            Console.WriteLine();

            for (var i = 0; i < columns.Count; i++)
            {
                Console.Write(new String('=', columnPadding[i]));
                Console.Write(" ");
            }
            Console.WriteLine();

            foreach (var row in page)
            {
                for (var i = 0; i < columns.Count; i++)
                {
                    var column = columns[i].Column;
                    var entry = row[column] ?? "<null>";
                    Console.Write(entry.PadRight(columnPadding[i]));
                    Console.Write(" ");
                }
                Console.WriteLine();
            }

            if (!promptForContinuation)
            {
                return true;
            }

            Console.Write("Press Enter to continue, or q to quit: ");
            var input = Console.ReadKey();
            Console.WriteLine();
            return input.KeyChar != 'q';
        }

        /// <summary>
        ///   Converts a table metadata listing into a printable page.
        /// </summary>
        private static Tuple<List<ReaderMetadata>, List<Dictionary<string, string>>> TableListingToPage(List<TableMetadata> tables)
        {
            var metadata = new List<ReaderMetadata> {
               new ReaderMetadata("Catalog", "System.String"),
               new ReaderMetadata("Schema", "System.String"),
               new ReaderMetadata("Table", "System.String")
            };

            var rows = new List<Dictionary<string, string>>();
            foreach (var table in tables)
            {
                var row = new Dictionary<string, string>();
                row["Catalog"] = table.Catalog;
                row["Schema"] = table.Schema;
                row["Table"] = table.Table;
                rows.Add(row);
            }

            return Tuple.Create(metadata, rows);
        }

        /// <summary>
        ///   Converts a column metadata listing into a printable page.
        /// </summary>
        private static Tuple<List<ReaderMetadata>, List<Dictionary<string, string>>> ColumnListingToPage(List<ColumnMetadata> columns)
        {

           var metadata = new List<ReaderMetadata> {
              new ReaderMetadata("Catalog", "System.String"),
              new ReaderMetadata("Schema", "System.String"),
              new ReaderMetadata("Table", "System.String"),
              new ReaderMetadata("Column", "System.String"),
              new ReaderMetadata("DataType", "System.String")
           };

            var rows = new List<Dictionary<string, string>>();
            foreach (var column in columns)
            {
                var row = new Dictionary<string, string>();
                row["Catalog"] = column.Catalog;
                row["Schema"] = column.Schema;
                row["Table"] = column.Table;
                row["Column"] = column.Column;
                row["DataType"] = column.DataType;
                rows.Add(row);
            }

            return Tuple.Create(metadata, rows);
        }

        /// <summary>
        ///   Reads a single SQL query, or returns null if a parsing error was
        ///   encountered.
        /// </summary>
        private static string ReadQuery()
        {
            var lexer = new SqlLexer();
            var continuation = false;
            var buffer = new StringBuilder();

            do
            {
                if (continuation)
                {
                    Console.Write(">>>> ");
                }
                else
                {
                    Console.Write("sql> ");
                    continuation = true;
                }

                var line = Console.ReadLine() + "\n";
                buffer.Append(line);
                lexer.Feed(line);
            } while (lexer.State != LexerState.COMPLETE &&
                     lexer.State != LexerState.ERROR);

            if (lexer.State == LexerState.COMPLETE)
            {
                return buffer.ToString();
            }
            else
            {
                return null;
            }
        }

        struct RunConfig
        {
            public string Url;
            public bool VerifyCertificate;
            public bool Paginate;
        }

        private static void PrintUsageAndDie()
        {
            Console.Error.WriteLine("CLI.exe -u <server-url> [-s] [-p]");
            Environment.Exit(1);
        }

        private static RunConfig ParseArguments(string[] Args)
        {
            var config = new RunConfig();
            config.VerifyCertificate = true;
            config.Paginate = true;

            try
            {
                for (var i = 0; i < Args.Length; i++)
                {
                    switch (Args[i])
                    {
                        case "-u":
                            if (config.Url != null) PrintUsageAndDie();
                            config.Url = Args[i + 1];
                            i++;
                            break;

                        case "-s":
                            config.VerifyCertificate = false;
                            break;

                        case "-p":
                            config.Paginate = false;
                            break;
                    }
                }
            }
            catch (IndexOutOfRangeException)
            {
                PrintUsageAndDie();
            }

            if (config.Url == null)
            {
                PrintUsageAndDie();
            }

            return config;
        }

        public static void Main(string[] Args)
        {
            var config = ParseArguments(Args);

            RpcWrapper rpc = new RpcWrapper(new Uri(config.Url));
            if (!config.VerifyCertificate)
            {
               ServicePointManager.ServerCertificateValidationCallback +=
                  (sender, cert, chain, errors) => true;
            }

            var done = false;
            while (!done)
            {
                var sql = ReadQuery()?.Trim();
                if (sql == null)
                {
                    Console.Error.WriteLine("Could not parse SQL");
                    continue;
                }

                try
                {
                    switch (sql)
                    {
                        case "quit;":
                            rpc.Quit();
                            done = true;
                            break;

                        case "tables;":
                            var tables = rpc.RetrieveTables();
                            var tablePageInfo = TableListingToPage(tables);
                            DisplayPage(tablePageInfo.Item1, tablePageInfo.Item2, false);
                            break;

                        case "views;":
                            var views = rpc.RetrieveViews();
                            var viewPageInfo = TableListingToPage(views);
                            DisplayPage(viewPageInfo.Item1, viewPageInfo.Item2, false);
                            break;

                        case "columns;":
                            Console.Write("Enter the table name (catalog and schema are optional): ");
                            Console.Out.Flush();

                            var dottedName = Console.ReadLine();
                            var dottedParts = SqlLexer.ParseDottedName(dottedName);

                            string catalog = "", schema = "", table = null;

                            switch (dottedParts.Count)
                            {
                                case 1:
                                    table = dottedParts[0];
                                    break;
                                case 2:
                                    schema = dottedParts[0];
                                    table = dottedParts[1];
                                    break;
                                case 3:
                                    catalog = dottedParts[0];
                                    schema = dottedParts[1];
                                    table = dottedParts[2];
                                    break;
                            }

                            if (table == null)
                            {
                                Console.WriteLine("Invalid table name provided");
                            }
                            else
                            {
                                var cols = rpc.RetrieveColumns(catalog, schema, table);
                                var colsPageInfo = ColumnListingToPage(cols);
                                DisplayPage(colsPageInfo.Item1, colsPageInfo.Item2, false);
                            }
                            break;

                        default:
                            try
                            {
                                rpc.ExecuteSql(sql);

                                var resultColumns = rpc.RetrieveQueryColumns();
                                if (resultColumns.Count == 0)
                                {
                                    Console.WriteLine("Records affected: {0}", rpc.RetrieveResultCount());
                                }
                                else if (config.Paginate)
                                {
                                    var page = new List<Dictionary<string, string>>();
                                    var pagePrinted = false;
                                    do
                                    {
                                        page = rpc.RetrievePage();

                                        var canDisplay = page.Count > 0 || !pagePrinted;
                                        if (canDisplay && !DisplayPage(resultColumns, page, true))
                                        {
                                            break;
                                        }

                                        pagePrinted = true;
                                    } while (page.Count != 0);
                                }
                                else
                                {
                                    var results = new List<Dictionary<string, string>>();
                                    var page = new List<Dictionary<string, string>>();
                                    do
                                    {
                                        page = rpc.RetrievePage();
                                        results.AddRange(page);
                                    } while (page.Count != 0);

                                    DisplayPage(resultColumns, results, false);
                                }
                            }
                            finally
                            {
                                try
                                {
                                    rpc.FinishQuery();
                                }
                                catch (RpcException)
                                {
                                }
                            }
                            break;
                    }
                }
                catch (RpcException error)
                {
                    Console.Error.WriteLine("Received error from server: {0}", error);
                }
            }
        }
    }
}
