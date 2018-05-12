using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;

namespace ADONotebook
{
    public class Application
    {
        private readonly string Provider;
        private readonly string ConnectionString;

        public Application(string provider, string connectionString)
        {
            Provider = provider;
            ConnectionString = connectionString;
        }

        public DbConnection OpenConnection()
        {
            var factory = DbProviderFactories.GetFactory(Provider);
            var connection = factory.CreateConnection();
            connection.ConnectionString = ConnectionString;
            connection.Open();
            return connection;
        }

        private string PromptConsole(string prompt)
        {
            Console.Write(prompt);
            return Console.ReadLine();
        }

        private string ReadQuery()
        {
            var lexer = new SqlLexer();
            var builder = new StringBuilder();

            var inContinuation = false;
            while (true)
            {
                var prompt = inContinuation ? ">>>  " : "sql> ";
                inContinuation = true;

                var line = PromptConsole(prompt).Trim() + "\n";
                builder.Append(line);
                lexer.Feed(line);

                if (lexer.State == LexerState.COMPLETE)
                {
                    return builder.ToString();
                }
                else if (lexer.State == LexerState.ERROR)
                {
                    Console.WriteLine("[ERROR] Could not parse SQL statement");
                    return null;
                }
            }
        }

        private void RenderDataTable(DataTable table)
        {
            var maxColumnLength = new int[table.Columns.Count];
            for (var i = 0; i < table.Columns.Count; i++)
            {
                maxColumnLength[i] = Math.Max(table.Columns[i].ColumnName.Length, table.Columns[i].DataType.ToString().Length + 2);
            }

            foreach (DataRow row in table.Rows)
            {
                for (var i = 0; i < table.Columns.Count; i++)
                {
                    maxColumnLength[i] = Math.Max(maxColumnLength[i], row[i].ToString().Length + 2);
                }
            }

            for (var i = 0; i < table.Columns.Count; i++)
            {
                Console.Write(table.Columns[i].ColumnName.PadRight(maxColumnLength[i]));
                Console.Write(" ");
            }
            Console.WriteLine();

            for (var i = 0; i < table.Columns.Count; i++)
            {
                Console.Write("[{0}]", table.Columns[i].DataType.ToString().PadRight(maxColumnLength[i] - 2));
                Console.Write(" ");
            }
            Console.WriteLine();

            for (var i = 0; i < table.Columns.Count; i++)
            {
                Console.Write(new string('=', maxColumnLength[i]));
                Console.Write(" ");
            }
            Console.WriteLine();

            foreach (DataRow row in table.Rows)
            {
                for (var i = 0; i < table.Columns.Count; i++)
                {
                    Console.Write(row[i].ToString().PadRight(maxColumnLength[i]));
                    Console.Write(" ");
                }
                Console.WriteLine();
            }
        }

        public void Run()
        {
            using (var connection = OpenConnection())
            {
                while (true)
                {
                    var query = ReadQuery().Trim();
                    if (query == ".quit;")
                    {
                        break;
                    }

                    try
                    {
                        var command = connection.CreateCommand();
                        command.CommandText = query;
                        command.CommandType = CommandType.Text;

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.FieldCount == 0)
                            {
                                Console.WriteLine("{0} rows affected", reader.RecordsAffected);
                            }
                            else
                            {
                                var table = new DataTable();

                                for (var i = 0; i < reader.FieldCount; i++)
                                {
                                    var tableColumn = new DataColumn(reader.GetName(i), reader.GetFieldType(i));
                                    table.Columns.Add(tableColumn);
                                }

                                while (reader.Read())
                                {
                                    var tableRow = table.NewRow();
                                    for (var i = 0; i < reader.FieldCount; i++)
                                    {
                                        tableRow[i] = reader[i];
                                    }

                                    table.Rows.Add(tableRow);
                                    if (table.Rows.Count == 100)
                                    {
                                        RenderDataTable(table);
                                        table.Clear();

                                        var doContinue = PromptConsole("Press Enter to continue, or q to quit").Trim();
                                        if (doContinue == "q")
                                        {
                                            break;
                                        }
                                    }
                                }

                                if (table.Rows.Count > 0)
                                {
                                    RenderDataTable(table);
                                }
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine(err.ToString());
                        Console.WriteLine(err.StackTrace);
                    }
                }
            }
        }

        public static void Main(string[] Args)
        {
            if (Args.Length != 2)
            {
                Console.Error.WriteLine("adonotebook.exe <provider> <connection-string>");
                Environment.Exit(1);
            }

            var app = new Application(Args[0], Args[1]);
            app.Run();
        }
    }
}
