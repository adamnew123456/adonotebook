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
            Console.Error.WriteLine("adonotebook.exe (-c|-s) (-f <provider> <connection-string> | -r <dll> <class> <connection-string>)");
            Environment.Exit(1);
        }

        private struct RunConfiguration
        {
            public QueryInput Input;
            public QueryExecutor Executor;
            public QueryOutput Output;
        }

        private static RunConfiguration? ParseArguments(string[] Args)
        {
            var config = new RunConfiguration();

            try
            {
                for (var i = 0; i < Args.Length; i++)
                {
                    switch (Args[i])
                    {
                        case "-s":
                            if (config.Input != null)
                            {
                                PrintUsageAndDie();
                            }

                            config.Input = new JsonRpcInput();
                            break;

                        case "-c":
                            if (config.Input != null)
                            {
                                PrintUsageAndDie();
                            }

                            config.Input = new ConsoleInput();
                            config.Output = new ConsoleOutput();
                            break;

                        case "-f":
                            if (config.Executor != null)
                            {
                                PrintUsageAndDie();
                            }

                            config.Executor = new ADOProviderFactoryExecutor(Args[i + 1], Args[i + 2]);
                            i += 2;
                            break;

                        case "-r":
                            if (config.Executor != null)
                            {
                                PrintUsageAndDie();
                            }

                            config.Executor = new ADOReflectionExecutor(Args[i + 1], Args[i + 2], Args[i + 3]);
                            i += 3;
                            break;
                    }
                }
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }

            if (config.Input == null || config.Executor == null)
            {
                return null;
            }

            if (config.Output != null)
            {
                config.Executor.Output = config.Output;
            }

            config.Input.Executor = config.Executor;
            return config;
        }

        public static void Main(string[] Args)
        {
            var config = ParseArguments(Args);
            if (config == null) PrintUsageAndDie();

            config.Value.Input.Run();
        }
    }
}
