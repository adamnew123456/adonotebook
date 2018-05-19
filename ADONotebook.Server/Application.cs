using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;

namespace ADONotebook
{
    public class Application
    {
        private static void PrintUsageAndDie()
        {
            Console.Error.WriteLine("adonotebook.exe (-f <provider> <connection-string> | -r <dll> <class> <connection-string>)");
            Environment.Exit(1);
        }

        private static ADOQueryExecutor ParseArguments(string[] Args)
        {
            ADOQueryExecutor executor = null;

            try
            {
                for (var i = 0; i < Args.Length; i++)
                {
                    switch (Args[i])
                    {
                        case "-f":
                            if (executor != null)
                            {
                                PrintUsageAndDie();
                            }

                            executor = new ADOProviderFactoryExecutor(Args[i + 1], Args[i + 2]);
                            i += 2;
                            break;

                        case "-r":
                            if (executor != null)
                            {
                                PrintUsageAndDie();
                            }

                            executor = new ADOReflectionExecutor(Args[i + 1], Args[i + 2], Args[i + 3]);
                            i += 3;
                            break;
                    }
                }
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }

            return executor;
        }

        public static void Main(string[] Args)
        {
            var executor = ParseArguments(Args);
            if (executor == null) PrintUsageAndDie();

            var server = new JsonRpcServer("http://localhost:1995/");
            server.Run(executor);
        }
    }
}
