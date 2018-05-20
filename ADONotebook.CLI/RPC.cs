using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ADONotebook
{
    public class ReaderMetadata
    {
        [JsonProperty("columnnames")]
        public List<string> ColumnNames;

        [JsonProperty("columntypes")]
        public List<string> ColumnTypes;
    }

    public class TableMetadata
    {
        [JsonProperty("catalog")]
        public string Catalog;

        [JsonProperty("table")]
        public string Table;
    }

    public class ColumnMetadata
    {
        [JsonProperty("catalog")]
        public string Catalog;

        [JsonProperty("table")]
        public string Table;

        [JsonProperty("column")]
        public string Column;

        [JsonProperty("datatype")]
        public string DataType;
    }

    public class RpcException : Exception
    {
        public RpcException(string message, string stacktrace) : base(String.Format("{0}\n{1}", message, stacktrace))
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

            var requestBytes = Encoding.UTF8.GetBytes(request.ToString());

            /*
             * I'm not sure if it's a property of Mono's WebClient or the .NET
             * WebClient in general, but any issue retrieving the response that
             * causes a WebException will close the response before we can
             * examine it. That's a problem because the real error data occurs
             * inside of the response body, as JSON-RPC errors.
             *
             * To combat that, we have to construct the request manually so that
             * we control the lifetime of all of the streams involved and can ignore
             * any WebExceptions.
             */
            var httpRequest = HttpWebRequest.Create(Endpoint) as HttpWebRequest;
            httpRequest.ContentType = "application/json";
            httpRequest.Accept = "application/json; application/json-rpc";
            httpRequest.ContentLength = requestBytes.Length;
            httpRequest.Method = "POST";

            var requestStream = httpRequest.GetRequestStream();
            requestStream.Write(requestBytes, 0, requestBytes.Length);
            requestStream.Close();

            HttpWebResponse httpResponse;
            try
            {
                httpResponse = httpRequest.GetResponse() as HttpWebResponse;
            }
            catch (WebException error)
            {
                httpResponse = error.Response as HttpWebResponse;
            }

            if (httpResponse.ContentType != "application/json" &&
                httpResponse.ContentType != "application/json-rpc")
            {
                httpResponse.Close();
                throw new RpcException("Response received from server was not JSON", "");
            }

            var responseReader = new StreamReader(httpResponse.GetResponseStream(), Encoding.UTF8);
            var responseRaw = responseReader.ReadToEnd();

            responseReader.Close();
            httpResponse.Close();

            var response = JObject.Parse(responseRaw);
            if (response.ContainsKey("error"))
            {
                throw new RpcException(response["error"]["message"].ToObject<string>(),
                                       response["error"]["data"]["stacktrace"].ToObject<string>());
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
