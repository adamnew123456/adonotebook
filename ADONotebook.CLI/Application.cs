using System;
using System.Collections.Generic;
using System.Text;

namespace ADONotebook
{
    public class Application
    {
        private static int[] ComputePrintingWidth(ReaderMetadata columns, List<Dictionary<string, string>> page)
        {
            var maxLengths = new int[columns.ColumnNames.Count];
            for (var i = 0; i < columns.ColumnNames.Count; i++)
            {
                maxLengths[i] = Math.Max(maxLengths[i], columns.ColumnNames[i].Length);
            }

            for (var i = 0; i < columns.ColumnTypes.Count; i++)
            {
                maxLengths[i] = Math.Max(maxLengths[i], columns.ColumnTypes[i].Length);
            }

            foreach (var row in page)
            {
                for (var i = 0; i < columns.ColumnNames.Count; i++)
                {
                    var entry = row[columns.ColumnNames[i]] ?? "<null>";
                    maxLengths[i] = Math.Max(maxLengths[i], entry.Length);
                }
            }

            return maxLengths;
        }

        private static bool DisplayPage(ReaderMetadata columns, List<Dictionary<string, string>> page, bool promptForContinuation)
        {
            var columnPadding = ComputePrintingWidth(columns, page);
            for (var i = 0; i < columns.ColumnNames.Count; i++)
            {
                Console.Write(columns.ColumnNames[i].PadRight(columnPadding[i]));
                Console.Write(" ");
            }
            Console.WriteLine();

            for (var i = 0; i < columns.ColumnTypes.Count; i++)
            {
                Console.Write(columns.ColumnTypes[i].PadRight(columnPadding[i]));
                Console.Write(" ");
            }
            Console.WriteLine();

            for (var i = 0; i < columns.ColumnTypes.Count; i++)
            {
                Console.Write(new String('=', columnPadding[i]));
                Console.Write(" ");
            }
            Console.WriteLine();

            foreach (var row in page)
            {
                for (var i = 0; i < columns.ColumnNames.Count; i++)
                {
                    var column = columns.ColumnNames[i];
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

        private static Tuple<ReaderMetadata, List<Dictionary<string, string>>> TableListingToPage(List<TableMetadata> tables)
        {
            var metadata = new ReaderMetadata
            {
                ColumnNames = new List<string> { "Catalog", "Table" },
                ColumnTypes = new List<string> { "System.String", "System.String" }
            };

            var rows = new List<Dictionary<string, string>>();
            foreach (var table in tables)
            {
                var row = new Dictionary<string, string>();
                row["Catalog"] = table.Catalog;
                row["Table"] = table.Table;
                rows.Add(row);
            }

            return Tuple.Create(metadata, rows);
        }

        private static Tuple<ReaderMetadata, List<Dictionary<string, string>>> ColumnListingToPage(List<ColumnMetadata> columns)
        {
            var metadata = new ReaderMetadata
            {
                ColumnNames = new List<string> { "Catalog", "Table", "Column", "DataType" },
                ColumnTypes = new List<string> { "System.String", "System.String", "System.String", "System.String" }
            };

            var rows = new List<Dictionary<string, string>>();
            foreach (var column in columns)
            {
                var row = new Dictionary<string, string>();
                row["Catalog"] = column.Catalog;
                row["Table"] = column.Table;
                row["Column"] = column.Column;
                row["DataType"] = column.DataType;
                rows.Add(row);
            }

            return Tuple.Create(metadata, rows);
        }

        public static void Main(string[] Args)
        {
            RpcWrapper rpc = null;
            try
            {
                rpc = new RpcWrapper(new Uri(Args[0]));
            }
            catch (IndexOutOfRangeException)
            {
                Console.Error.WriteLine("adonotebook.exe <server-url>");
                Environment.Exit(1);
            }

            var done = false;
            while (!done)
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

                if (lexer.State == LexerState.ERROR)
                {
                    Console.Error.WriteLine("Could not parse SQL");
                    continue;
                }

                var sql = buffer.ToString().Trim();
                switch (sql)
                {
                    case "quit;":
                        try
                        {
                            rpc.Quit();
                            done = true;
                        }
                        catch (RpcException error)
                        {
                            Console.Error.WriteLine("Received error from server: {0}", error);
                        }
                        break;

                    case "tables;":
                        try
                        {
                            var tables = rpc.RetrieveTables();
                            var tablePageInfo = TableListingToPage(tables);
                            DisplayPage(tablePageInfo.Item1, tablePageInfo.Item2, false);
                        }
                        catch (RpcException error)
                        {
                            Console.Error.WriteLine("Received error from server: {0}", error);
                        }
                        break;

                    case "views;":
                        try
                        {
                            var views = rpc.RetrieveViews();
                            var viewPageInfo = TableListingToPage(views);
                            DisplayPage(viewPageInfo.Item1, viewPageInfo.Item2, false);
                        }
                        catch (RpcException error)
                        {
                            Console.Error.WriteLine("Received error from server: {0}", error);
                        }
                        break;

                    case "columns;":
                        try
                        {
                            var cols = rpc.RetrieveColumns();
                            var colsPageInfo = ColumnListingToPage(cols);
                            DisplayPage(colsPageInfo.Item1, colsPageInfo.Item2, false);
                        }
                        catch (RpcException error)
                        {
                            Console.Error.WriteLine("Received error from server: {0}", error);
                        }
                        break;

                    default:
                        try
                        {
                            rpc.ExecuteSql(sql);

                            var resultColumns = rpc.RetrieveQueryColumns();
                            if (resultColumns.ColumnNames.Count == 0)
                            {
                                Console.WriteLine("Records affected: {0}", rpc.RetrieveResultCount());
                            }
                            else
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

                        }
                        catch (RpcException error)
                        {
                            Console.Error.WriteLine("Received error from server: {0}", error);
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
        }
    }
}
