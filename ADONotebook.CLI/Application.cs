using System;
using System.Collections.Generic;

namespace ADONotebook
{
    public class Application
    {
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

            Console.WriteLine("+++ Tables +++");
            foreach (var table in rpc.RetrieveTables())
            {
                Console.WriteLine("`{0}`.`{1}`", table.Catalog, table.Table);
            }

            Console.WriteLine("+++ Views +++");
            foreach (var view in rpc.RetrieveViews())
            {
                Console.WriteLine("`{0}`.`{1}`", view.Catalog, view.Table);
            }

            Console.WriteLine("+++ Columns +++");
            foreach (var column in rpc.RetrieveColumns())
            {
                Console.WriteLine("`{0}`.`{1}`.`{2}`", column.Catalog, column.Table, column.Column);
            }

            Console.WriteLine("+++ Executing Query: select * from Product +++");
            rpc.ExecuteSql("select * from Product");

            Console.WriteLine("+++ Retrieving Query Metdata +++");
            var metadata = rpc.RetrieveQueryColumns();
            for (var i = 0; i < metadata.ColumnNames.Count; i++)
            {
                Console.WriteLine("`{0}` :: {1}", metadata.ColumnNames[i], metadata.ColumnTypes[i]);
            }

            Console.WriteLine("+++ Paging Query +++");
            var page = new List<Dictionary<string, string>>();
            do
            {
                Console.WriteLine("### Page ###");
                page = rpc.RetrievePage();

                foreach (var row in page)
                {
                    foreach (var column in row.Keys)
                    {
                        Console.WriteLine("{0}: {1}", column, row[column]);
                    }
                    Console.WriteLine();
                }
            } while (page.Count > 0);

            Console.WriteLine("+++ Finishing Query +++");
            rpc.FinishQuery();

            Console.WriteLine("+++ Terminating Server +++");
            rpc.Quit();
        }
    }
}
