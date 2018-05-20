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
            Console.Error.WriteLine("adonotebook.exe (-p <port>) (-f <provider> <connection-string> | -r <dll> <class> <connection-string>)");
            Environment.Exit(1);
        }

        private static Tuple<int, ADOQueryExecutor> ParseArguments(string[] Args)
        {
            ADOQueryExecutor executor = null;
            int port = -1;

            try
            {
                for (var i = 0; i < Args.Length; i++)
                {
                    switch (Args[i])
                    {
                        case "-p":
                            if (port != -1) PrintUsageAndDie();
                            try
                            {
                                port = int.Parse(Args[i + 1]);
                                if (port < 1 || port > 65536) PrintUsageAndDie();
                            }
                            catch (FormatException)
                            {
                                PrintUsageAndDie();
                            }

                            i++;
                            break;

                        case "-f":
                            if (executor != null) PrintUsageAndDie();
                            executor = new ADOProviderFactoryExecutor(Args[i + 1], Args[i + 2]);
                            i += 2;
                            break;

                        case "-r":
                            if (executor != null) PrintUsageAndDie();
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

            if (port == -1)
            {
                port = 1995;
            }

            return Tuple.Create(port, executor);
        }

        public static void Main(string[] Args)
        {
            var runConfiguration = ParseArguments(Args);
            if (runConfiguration == null || runConfiguration.Item2 == null)
            {
                PrintUsageAndDie();
            }

            var server = new JsonRpcServer("http://localhost:" + runConfiguration.Item1 + "/");
            server.Run(runConfiguration.Item2);
        }
    }
}
