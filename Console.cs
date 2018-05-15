using System;
using System.Data;
using System.Data.Common;
using System.Text;

namespace ADONotebook
{
    public static class ConsoleUtils
    {
        /// <summary>
        ///   Prints a string to stdout, and reads a line of text
        ///   (newline included) from the user.
        /// </summary>
        public static string Prompt(string prompt)
        {
            Console.Write(prompt);
            return Console.ReadLine() + "\n";
        }

        /// <summary>
        ///   Renders a data table to stdout, including column headings and
        ///   types, which are right-justified column-by-column.
        /// </summary>
        public static void RenderDataTable(DataTable table)
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
    }

    public class ConsoleInput : QueryInput
    {
        public QueryExecutor Executor {get; set;}

        public ConsoleInput(QueryExecutor exec)
        {
            Executor = exec;
        }

        /// <summary>
        ///   Reads a complete SQL query from the console. If a syntax error
        ///   is found, this returns null, otherwise the query is returned.
        /// </summary>
        private string ReadQuery()
        {
            var buffer = new StringBuilder();
            var lexer = new SqlLexer();

            var line = ConsoleUtils.Prompt("sql> ");
            while (true)
            {
                buffer.Append(line);
                lexer.Feed(line);
                if (lexer.State == LexerState.COMPLETE)
                {
                    return buffer.ToString();
                }
                else if (lexer.State == LexerState.ERROR)
                {
                    return null;
                }

                line = ConsoleUtils.Prompt(">>> ");
            }
        }

        /// <summary>
        ///   Passes queries from the console to the executor, until a quit
        ///   command is read.
        /// </summary>
        public void Run()
        {
            using (Executor)
            {
                Executor.Open();
                while (true)
                {
                    string query = ReadQuery();
                    if (query == null)
                    {
                        Console.WriteLine("Could not parse SQL");
                        continue;
                    }

                    query = query.Trim();
                    if (query == "quit;")
                    {
                        break;
                    }

                    Executor.ProcessQuery(query);
                }
            }
        }
    }

    public class ConsoleOutput : QueryOutput
    {
        /// <summary>
        ///   Displays an error message on stdout.
        /// </summary>
        public void DisplayError(string message)
        {
            Console.Error.WriteLine("Error from provider: {0}", message);
        }

        /// <summary>
        ///   Displays the affected record count on stdout.
        /// </summary>
        public void DisplayResultCount(int results)
        {
            Console.WriteLine("{0} records affected", results);
        }

        /// <summary>
        ///   Displays a page of results, and prompts the user to continue
        ///   displaying.
        /// </summary>
        public bool DisplayPage(DataTable table)
        {
            ConsoleUtils.RenderDataTable(table);
            return ConsoleUtils.Prompt("Enter to continue, or q to quit").Trim() != "q";
        }

        /// <summary>
        ///   Displays the last page of results, without prompting the user.
        /// </summary>
        public void DisplayLastPage(DataTable table)
        {
            ConsoleUtils.RenderDataTable(table);
        }
    }
}
