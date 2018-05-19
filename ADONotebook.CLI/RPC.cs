using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ADONotebook
{
    public class ReaderMetadata
    {
        public List<string> ColumnNames;
        public List<string> ColumnTypes;
    }

    public class TableMetadata
    {
        public string Catalog;
        public string Table;
    }

    public class ColumnMetadata
    {
        public string Catalog;
        public string Table;
        public string Column;
        public string DataType;
    }

    public class RpcException : Exception
    {
        public RpcException(int code, string message) : base(String.Format("[{0}] {1}", code, message))
        {
        }
    }

    public class RpcWrapper
    {
        private Uri Endpoint;

        public RpcWrapper(Uri endpoint)
        {
            Endpoint = endpoint;
        }

        /// <summary>
        ///   Executes a remote call, returning the "result" member if the
        ///   call was successful, or throwing an RpcException otherwise.
        /// </summary>
        public JToken RemoteCall(string method, params object[] args)
        {
            var request = new JObject();
            request["id"] = 1;
            request["jsonrpc"] = "2.0";
            request["method"] = method;

            var jsonArgs = new JArray();
            foreach (var arg in args)
            {
                jsonArgs.Add(arg);
            }

            request["params"] = jsonArgs;

            var client = new WebClient();
            client.Headers.Remove("Content-Type");
            client.Headers.Add("Content-Type", "application/json");

            var responseRaw = client.UploadString(Endpoint, "POST", request.ToString());
            var response = JObject.Parse(responseRaw);
            if (response.ContainsKey("error"))
            {
                throw new RpcException(response["error"]["code"].ToObject<int>(),
                                       response["error"]["data"]["Message"].ToObject<string>());
            }

            return response["result"];
        }

        /// <summary>
        ///   Retrieves the connection's tables from the server.
        /// </summary>
        public List<TableMetadata> RetrieveTables()
        {
            return RemoteCall("tables")
                .ToObject<List<TableMetadata>>();
        }

        /// <summary>
        ///   Retrieves the connection's views from the server.
        /// </summary>
        public List<TableMetadata> RetrieveViews()
        {
            return RemoteCall("views")
                .ToObject<List<TableMetadata>>();
        }

        /// <summary>
        ///   Retrieves the connection's columns from the server.
        /// </summary>
        public List<ColumnMetadata> RetrieveColumns()
        {
            return RemoteCall("columns")
                .ToObject<List<ColumnMetadata>>();
        }

        /// <summary>
        ///   Requests the server to execute the given SQL.
        /// </summary>
        public void ExecuteSql(string sql)
        {
            RemoteCall("execute", sql);
        }

        /// <summary>
        ///   Requests the server to provide the column listing of the current query.
        /// </summary>
        public ReaderMetadata RetrieveQueryColumns()
        {
            return RemoteCall("metadata")
                .ToObject<ReaderMetadata>();
        }

        /// <summary>
        ///   Requests the server to provide a page of results.
        /// </summary>
        public List<Dictionary<string, string>> RetrievePage()
        {
            return RemoteCall("page")
                .ToObject<List<Dictionary<string, string>>>();
        }

        /// <summary>
        ///   Requests the server to provide the result count of the current query.
        /// </summary>
        public int RetrieveResultCount()
        {
            return RemoteCall("count").ToObject<int>();
        }

        /// <summary>
        ///   Tells the server to close the current query.
        /// </summary>
        public void FinishQuery()
        {
            RemoteCall("finish");
        }

        /// <summary>
        ///   Terminates the server.
        /// </summary>
        public void Quit()
        {
            RemoteCall("quit");
        }
    }
}
