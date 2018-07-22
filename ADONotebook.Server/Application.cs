using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;

namespace ADONotebook
{
    public class Application
    {
        struct RunConfig
        {
            public int Port;
            public ADOQueryExecutor Executor;
            public bool Ssl;
        }

        private static void PrintUsageAndDie()
        {
            Console.Error.WriteLine("Server.exe (-p <port>) (-f <provider> | -r <dll> <class>) [-s] (-P <property> <value>)*");
            Environment.Exit(1);
        }

        private static RunConfig ParseArguments(string[] Args)
        {
            var config = new RunConfig();
            config.Port = -1;
            config.Ssl = false;
            var properties = new Dictionary<string, string>();

            try
            {
                for (var i = 0; i < Args.Length; i++)
                {
                    switch (Args[i])
                    {
                        case "-p":
                            if (config.Port != -1) PrintUsageAndDie();
                            try
                            {
                                config.Port = int.Parse(Args[i + 1]);
                                if (config.Port < 1 || config.Port > 65536) PrintUsageAndDie();
                            }
                            catch (FormatException)
                            {
                                PrintUsageAndDie();
                            }

                            i++;
                            break;

                        case "-s":
                            config.Ssl = true;
                            break;

                        case "-f":
                            if (config.Executor != null) PrintUsageAndDie();
                            config.Executor = new ADOProviderFactoryExecutor(Args[i + 1], properties);
                            i++;
                            break;

                        case "-r":
                            if (config.Executor != null) PrintUsageAndDie();
                            config.Executor = new ADOReflectionExecutor(Args[i + 1], Args[i + 2], properties);
                            i += 2;
                            break;

                        case "-P":
                            properties[Args[i + 1]] = Args[i + 2];
                            i += 2;
                            break;
                    }
                }
            }
            catch (IndexOutOfRangeException)
            {
                PrintUsageAndDie();
            }

            if (config.Port == -1)
            {
                config.Port = 1995;
            }

            if (config.Executor == null)
            {
                PrintUsageAndDie();
            }

            return config;
        }

        public static void Main(string[] Args)
        {
            var runConfiguration = ParseArguments(Args);
            var url = "http";
            if (runConfiguration.Ssl)
            {
                url += "s";
            }
            url += "://localhost:" + runConfiguration.Port + "/";

            var server = new JsonRpcServer(url);
            server.Run(runConfiguration.Executor);
        }
    }
}
