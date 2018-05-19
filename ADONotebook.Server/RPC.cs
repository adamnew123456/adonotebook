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
    public class JsonRpcServer
    {
        private string Endpoint;

        public JsonRpcServer(string endpoint)
        {
            Endpoint = endpoint;
        }

        private void SetOutputContent(HttpListenerResponse response, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);

            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }

        /// <summary>
        ///   Runs the RPC server, until it receives a call of the "quit" method.
        /// </summary>
        public void Run(ADOQueryExecutor executor)
        {
            var proxy = new JsonRpcProxy(executor);


            Config.SetPostProcessHandler((JsonRequest request, JsonResponse response, object context) => {
                    if (response.Error != null)
                    {
                        var innerException = (response.Error.data as Exception);
                        var errorData = new Dictionary<string, string>();
                        errorData["stacktrace"] = innerException.StackTrace;
                        response.Error = new JsonRpcException(-32603, innerException.Message, errorData);
                    }

                    return null;
                });

            var listener = new HttpListener();
            listener.Prefixes.Add(Endpoint);

            Console.WriteLine("Awaiting requests on {0}", Endpoint);
            listener.Start();

            try
            {
                executor.Open();

                while (!proxy.Finished)
                {
                    var context = listener.GetContext();
                    context.Response.ContentType = "application/json";

                    if (context.Request.HttpMethod != "POST")
                    {
                        context.Response.StatusCode = 405;
                        context.Response.StatusDescription = "Illegal Method";

                        var errorResponse = "{\"jsonrpc\": \"2.0\", \"error\": {\"code\": -32000, \"message\": \"Only the POST method is allowed\"}, \"id\": null}";
                        SetOutputContent(context.Response, errorResponse);
                        continue;
                    }

                    if (context.Request.Url.PathAndQuery != "/")
                    {
                        context.Response.StatusCode = 404;
                        context.Response.StatusDescription = "Not Found";

                        var errorResponse = "{\"jsonrpc\": \"2.0\", \"error\": {\"code\": -32001, \"message\": \"Only the path / is allowed\"}, \"id\": null}";
                        SetOutputContent(context.Response, errorResponse);
                        continue;
                    }

                    if (context.Request.ContentType != "application/json")
                    {
                        var errorResponse = "{\"jsonrpc\": \"2.0\", \"error\": {\"code\": -32700, \"message\": \"Parse error\"}, \"id\": null}";
                        SetOutputContent(context.Response, errorResponse);
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
                                Console.WriteLine(result.Result);
                                SetOutputContent(context.Response, result.Result);
                            })
                        .Wait();
                }
            }
            catch (Exception)
            {
                listener.Stop();
                executor.Close();
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
        public bool Finished { get; private set; }

        private ADOQueryExecutor Executor;
        private ADORequestPaginator Paginator;

        public JsonRpcProxy(ADOQueryExecutor executor) : base()
        {
            Finished = false;
            Executor = executor;
        }

        [JsonRpcMethod]
        private List<TableMetadata> tables()
        {
            var results = Executor.Tables();
            return results;
        }

        [JsonRpcMethod]
        private List<TableMetadata> views()
        {
            var results = Executor.Views();
            return results;
        }

        [JsonRpcMethod]
        private List<ColumnMetadata> columns()
        {
            var results = Executor.Columns();
            return results;
        }

        [JsonRpcMethod]
        private bool execute(string sql)
        {
            if (Paginator != null)
            {
                throw new InvalidOperationException("Please finish your existing query before running another one");
            }

            Paginator = Executor.Execute(sql);
            return true;
        }

        private void CheckPaginator()
        {
            if (Paginator == null)
            {
                throw new InvalidOperationException("Cannot call this function without a current query");
            }
        }

        [JsonRpcMethod]
        private ReaderMetadata metadata()
        {
            CheckPaginator();
            var metadata = new ReaderMetadata();

            foreach (var column in Paginator.Columns)
            {
                metadata.ColumnNames.Add(column.ColumnName);
                metadata.ColumnTypes.Add(column.DataType.ToString());
            }

            return metadata;
        }

        [JsonRpcMethod]
        private int count()
        {
            CheckPaginator();
            if (Paginator.ResultCount == -1)
            {
                throw new InvalidOperationException("Cannot get result count from query that returns data");
            }

            return Paginator.ResultCount;
        }

        [JsonRpcMethod]
        private List<Dictionary<string, string>> page()
        {
            CheckPaginator();
            var outputRows = new List<Dictionary<string, string>>();
            var page = Paginator.NextPage();

            foreach (DataRow row in page.Rows)
            {
                var outputRow = new Dictionary<string, string>();

                foreach (var column in Paginator.Columns)
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

            return outputRows;
        }

        [JsonRpcMethod]
        private bool finish()
        {
            CheckPaginator();
            Paginator.Close();
            Paginator = null;
            return true;
        }

        [JsonRpcMethod]
        private bool quit()
        {
            if (Paginator != null)
            {
                throw new InvalidOperationException("Cannot quit before finishing current query");
            }

            Finished = true;
            return true;
        }
    }
}
