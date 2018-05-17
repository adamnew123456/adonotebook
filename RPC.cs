using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Net;
using System.Text;

using AustinHarris.JsonRpc;

namespace ADONotebook
{
    public class JsonRpcInput : QueryInput
    {
        public QueryExecutor Executor { get; set; }
        private JsonRpcProxy Proxy;

        public JsonRpcInput()
        {
            Proxy = new JsonRpcProxy();
        }

        public void Run()
        {
            Proxy.Executor = Executor;

            var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:1995/");

            Console.WriteLine("Awaiting requests on http://localhost:1995/");
            listener.Start();

            try
            {
                Executor.Open();

                while (!Proxy.Finished)
                {
                    var context = listener.GetContext();
                    context.Response.ContentType = "application/json";

                    if (context.Request.ContentType != "application/json")
                    {
                        Console.WriteLine("Got non-JSON request");

                        var errorResponse = "{\"jsonrpc\": \"2.0\", \"error\": {\"code\": -32700, \"message\": \"Parse error\"}, \"id\": null}";
                        var errorResponseBytes = Encoding.UTF8.GetBytes(errorResponse);

                        context.Response.ContentLength64 = errorResponseBytes.Length;
                        context.Response.OutputStream.Write(errorResponseBytes, 0, errorResponseBytes.Length);
                        context.Response.Close();
                        continue;
                    }

                    var inputBuffer = new byte[context.Request.ContentLength64];
                    var offset = 0L;

                    while (offset != context.Request.ContentLength64)
                    {
                        offset += context.Request.InputStream.Read(inputBuffer, (int)offset, (int)(context.Request.ContentLength64 - offset));
                    }

                    var input = Encoding.UTF8.GetString(inputBuffer);
                    Console.WriteLine("Processing request: ");
                    Console.WriteLine(input);

                    JsonRpcProcessor
                        .Process(input)
                        .ContinueWith(result => {
                                Console.WriteLine("Delivering result:");
                                result.Wait();
                                Console.WriteLine(result.Result);

                                var responseBytes = Encoding.UTF8.GetBytes(result.Result);
                                context.Response.ContentLength64 = responseBytes.Length;
                                context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                                context.Response.OutputStream.Close();
                            })
                        .Wait();
                }
            }
            catch (Exception)
            {
                listener.Stop();
                Executor.Close();
                throw;
            }
        }
    }

    class ReaderMetadata
    {
        public List<string> ColumnNames;
        public List<string> ColumnTypes;

        public ReaderMetadata()
        {
            ColumnNames = new List<string>();
            ColumnTypes = new List<string>();
        }
    }

    class JsonRpcProxy : JsonRpcService
    {
        public QueryExecutor Executor;
        private JsonRpcOutput Output;

        public bool Finished { get; private set; }

        public JsonRpcProxy() : base()
        {
            Finished = false;
        }

        [JsonRpcMethod]
        private bool execute(string sql)
        {
            Output = new JsonRpcOutput();
            Executor.Output = Output;
            Executor.ProcessQuery(sql);
            return true;
        }

        private void CheckOutput()
        {
            if (Output == null)
            {
                throw new InvalidOperationException("Cannot call this function without current query");
            }

            if (Output.ErrorMessage != null)
            {
                throw new InvalidOperationException("The provider threw an exception: " + Output.ErrorMessage);
            }
        }

        [JsonRpcMethod]
        private ReaderMetadata metadata()
        {
            CheckOutput();

            var metadata = new ReaderMetadata();


            if (Output.Columns == null)
            {
                return metadata;
            }

            foreach (var column in Output.Columns)
            {
                metadata.ColumnNames.Add(column.ColumnName);
                metadata.ColumnTypes.Add(column.DataType.ToString());
            }

            return metadata;
        }

        [JsonRpcMethod]
        private int count()
        {
            CheckOutput();

            if (Output.ResultCount == -1)
            {
                throw new InvalidOperationException("Cannot get result count from query that returns data");
            }

            return Output.ResultCount;
        }

        [JsonRpcMethod]
        private List<Dictionary<string, string>> page()
        {
            CheckOutput();

            var outputRows = new List<Dictionary<string, string>>();
            if (Output.Pages.Count == 0)
            {
                return outputRows;
            }

            foreach (DataRow row in Output.Pages[0].Rows)
            {
                var outputRow = new Dictionary<string, string>();

                foreach (var column in Output.Columns)
                {
                    var data = row[column.ColumnName];
                    if (data == null)
                    {
                        outputRow[column.ColumnName] = null;
                    }
                    else
                    {
                        outputRow[column.ColumnName] = data.ToString();
                    }
                }

                outputRows.Add(outputRow);
            }

            Output.Pages.RemoveAt(0);
            return outputRows;
        }

        [JsonRpcMethod]
        private bool finish()
        {
            Executor.Output = null;
            Output = null;
            return true;
        }

        [JsonRpcMethod]
        private bool quit()
        {
            Finished = true;
            return true;
        }
    }

    public class JsonRpcOutput : QueryOutput
    {
        public DataColumn[] Columns { get; private set; }
        public List<DataTable> Pages { get; private set; }
        public int ResultCount { get; private set; }
        public string ErrorMessage { get; private set; }

        public JsonRpcOutput()
        {
            ResultCount = -1;
            Pages = new List<DataTable>();
        }

        public void DisplayError(string error)
        {
            ErrorMessage = error;
        }

        public void DisplayResultCount(int count)
        {
            ResultCount = count;
        }

        public void DisplayColumns(DataColumn[] columns)
        {
            Columns = columns;
        }

        public bool DisplayPage(DataTable table)
        {
            Pages.Add(table.Copy());
            return true;
        }

        public void DisplayLastPage(DataTable table)
        {
            Pages.Add(table.Copy());
        }
    }
}
