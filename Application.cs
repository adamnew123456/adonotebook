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
            Console.Error.WriteLine("adonotebook.exe (-f <provider> <connection-string> | -c <dll> <class> <connection-string>)");
            Environment.Exit(1);
        }

        public static void Main(string[] Args)
        {
            var output = new ConsoleOutput();

            if (Args.Length == 0) PrintUsageAndDie();

            QueryExecutor executor = null;
            if (Args[0] == "-f")
            {
                if (Args.Length != 3) PrintUsageAndDie();
                executor = new ADOProviderFactoryExecutor(Args[1], Args[2], output);
            }
            else if (Args[0] == "-c")
            {
                if (Args.Length != 4) PrintUsageAndDie();
                executor = new ADOReflectionExecutor(Args[1], Args[2], Args[3], output);
            }
            else PrintUsageAndDie();

            var input = new ConsoleInput(executor);

            input.Run();
        }
    }
}
