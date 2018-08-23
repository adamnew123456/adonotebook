using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Net;
using System.Text;

using AustinHarris.JsonRpc;
using Newtonsoft.Json;

namespace ADONotebook
{
    /// <summary>
    ///   Routes requests from an HTTP server to the JSON-RPC processor.
    /// </summary>
    public class JsonRpcServer
    {
        private string Endpoint;

        public JsonRpcServer(string endpoint)
        {
            Endpoint = endpoint;
        }

        /// <summary>
        ///   A helper for writing a response string.
        /// </summary>
        private void SetOutputContent(HttpListenerResponse response, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);

            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }

        /// <summary>
        ///   Add headers to allow the RPC to be used by JS.
        /// </summary>
        private void AddCORSHeaders(HttpListenerContext context)
        {
            context.Response.AddHeader("Access-Control-Allow-Origin", "*");
            context.Response.AddHeader("Access-Control-Allow-Methods", "POST, OPTIONS");
            context.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
            context.Response.AddHeader("Access-Control-Max-Age", "86400");
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
                    AddCORSHeaders(context);

                    if (context.Request.HttpMethod == "OPTIONS")
                    {
                        context.Response.StatusCode = 200;
                        context.Response.StatusDescription = "OK";
                        context.Response.ContentLength64 = 0;
                        context.Response.OutputStream.Close();
                        continue;
                    }

                    if (context.Request.HttpMethod != "POST")
                    {
                        context.Response.StatusCode = 405;
                        context.Response.StatusDescription = "Illegal Method";
                        context.Response.OutputStream.Close();
                        continue;
                    }

                    if (context.Request.Url.PathAndQuery != "/")
                    {
                        context.Response.StatusCode = 404;
                        context.Response.StatusDescription = "Not Found";
                        context.Response.OutputStream.Close();
                        continue;
                    }

                    if (context.Request.ContentType != "application/json")
                    {
                       context.Response.StatusCode = 400;
                       context.Response.StatusDescription = "Illegal Content Type";
                       context.Response.OutputStream.Close();
                       continue;
                    }

                    context.Response.ContentType = "application/json";
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

    /// <summary>
    ///   Information about the columns in a result set.
    /// </summary>
    class ReaderMetadata
    {
        [JsonProperty("column")]
        public string Column;

        [JsonProperty("datatype")]
        public string DataType;

        public ReaderMetadata(string column, string datatype)
        {
            Column = column;
            DataType = datatype;
        }
    }

    /// <summary>
    ///   The actual implementation of the RPC methods.
    /// </summary>
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
        private List<ColumnMetadata> columns(string catalog, string schema, string table)
        {
            if (catalog == "") catalog = null;
            if (schema == "") schema = null;
            if (table == "") table = null;
            var results = Executor.Columns(catalog, schema, table);
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
        private List<ReaderMetadata> metadata()
        {
            CheckPaginator();
            var metadata = new List<ReaderMetadata>();

            foreach (var column in Paginator.Columns)
            {
                metadata.Add(new ReaderMetadata(column.ColumnName, column.DataType.ToString()));
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
        private List<Dictionary<string, string>> page(int size)
        {
            CheckPaginator();
            if (size <= 0) {
                throw new InvalidOperationException("Page size must be a positive integer");
            }

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
