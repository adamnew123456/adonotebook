# What is this?

- **ADONotebook.Server** An ADO.NET web service that speaks a specific 
  JSON-RPC protocol over HTTP. Clients can connect to it and execute
  queries against whatever provider the service is configured to use.

- **ADONotebook.CLI** A command-line client that speaks that JSON-RPC 
  protocol.

The main reason for this split (compared to most query tools, which run the
driver in-process) is to allow for alternative backends, like the JDBC backend
provided by the companion project
[JDBCNotebook](https://github.com/adamnew123456/jdbcnotebook). The CLI can
communicate with anything that speaks this protocol, so even more backends
(such as for ODBC) could be created and used with this client (or other
clients).

Ultimately, my goal is to get it working with a UI that handles multiple
simultaneous connections - something akin to DBeaver, but whose backend
is agnostic to the database API. ODBC support is also planned.

# Building

1. Create a packages directory at the top-level.
2. Restore the packages from Nuget into that directory: `nuget restore packages.config -PackagesDirectory packages`
3. Compile via `msbuild` or `xbuild`.

# Running

1. Navigate to `ADONotebook.Server\bin\Debug`.
2. Depending upon your platform, run the command 
   `Server.exe -f <Provider> -P <Property> <Value> -P <Property> <Value>...`. 
   The provider option will need to be an entry in your machine.config, something like 
   `System.Data.SqlClient` or `Mono.Data.Sqlite` (you can also use a provider 
   from a DLL directly, but this involves knowing the provider factory class name).
3. This will start up the ADO.NET provider server, and you should see a message like **Awaiting connections on http://localhost:1995/**.
4. Navigate to `ADONotebook.CLI\bin\Debug` and run `CLI.exe -u http://localhost:1995/`.

From the command line, you can use these commands:

- `tables;` Prints a table listing
- `views;` Prints a view listing
- `columns;` Prints a column listing
- `quit;` Terminates the client and the server

In addition, you can also enter a SQL query terminated by a semicolon. A typical
session might look something like this (minus the actual results, and any
pagination that might need to happen):

```
sql> tables;
...
sql> views;
...
sql> select * from Customer
>>>> where FirstName = 'Frank'
>>>> order by SpendingAuthority;
...
sql> quit;
```

# Server Options 

- `-p port` Sets the port that the server listens on (default: 1995)
- `-f provider` Uses the given name to look up a provider within `DbProviderFactories`
- `-r dll class` Loads the assembly file and uses the class name, which should extend `DbProviderFactory`
- `-s` Enables SSL, using the certificate configured via `httpcfg`
- `-P property value` Sets connection properties on the provider's connection string builder. This can be repeated for any number of properties.

# Client Options 

- `-u server-url` Sets the server to connect to
- `-p` Disables pagination
- `-s` Disables SSL certificate verification

# Protocol
## JSON-RPC and HTTP

The methods and parameters sit on top of
[JSON-RPC](http://www.jsonrpc.org/specification), which is itself transported
over HTTP. The basic rules for the HTTP side are:

- A server *must* accept `application/json` as the input Content-Type. It
  *should* fail any request that doesn't have that Content-Type.
- A server *must* use either `application/json` or `application/json-rpc` as
  its output Content-Type.
- A server *must* accept `POST` requests on the path `/`. It *should* fail any
  request that doesn't have that method or path.
  
Any error on the HTTP side *should* not return anything in its response body.

On the JSON-RPC side, the only requirement is that the error response have this
structure. The stacktrace can be set to an empty string if it would be too
difficult/sensitive to return, but it's encouraged for debugging purposes.

```json
{
    "code": "<an integer, see the JSON-RPC spec>",
    "message": "<a string, containing a description of the error>",
    "data": {
        "stacktrace": "<a string, containing the call stack of where the process failed>"
    }
}
```

Any errors on the JSON-RPC side should still produce a status code of 200.

## Structures

There are a few common structures which are returned by methods that are useful
to see laid out. An implementation that uses a JSON library like Jackson or
JSON.NET that can map language objects into JSON would be advised to define
these as their own classes:

```java
class ReaderMetadata {
    String column;
    String datatype;
}

class TableMetadata {
    String catalog;
    String schema;
    String table;
}

class ColumnMetadata {
    String catalog;
    String schema;
    String table;
    String column;
    String datatype;
}
```

Note that the JSON representation of `List` is an array, and the JSON
representation of `Map` is as an object. These classes should also be rendered
as objects, with their field names being the object attributes and their field
values being the values of those attributes. For example, this is a
list of ReaderMetadata structures rendered as JSON:

```json
[
    {
        "column": "Metric",
        "datatype": "varchar"
    },
    {
        "column": "YTD",
        "datatype": "int"
    }
]
```

## Methods

Some of the methods listed will have the annotation *Active* or *NoActive*.
T>his means that the method either requires a currently active query in order to
run, or that the method must not have an active query in order to run.

    List<TableMetadata> tables()
    List<TableMetadata> views()
    
These functions return all tables and views currently visible within the 
database.

    List<ColumnMetadata> columns(String catalog, String schema, String table)
    
    
This function returns columns which are part of one or more tables. A
single table can be specified by providing non-blank catalog, schema
and table names, while leaving any of them blank will return columns
from any tables that match the patterns. For example:

- `columns("", "", "")` will return columns from every table
- `columns("prod", "product", "")` will return every column from every table in the `prod.product` schema
- `columns("", "", "employee")` will return every column from every table named *employee*

<!-- -->

    @NoActive boolean execute(String sql) -> @Active
    
This executes the given query, and saves the result set so that it may
be inspected using the `@Active` functions, and returns `true`.

This should fail if the RPC is currently in `@Active` mode.

    @Active List<ReaderMetadata> metadata()
    
This returns the columns available on the current result set. Note
that this may be empty if the result set does not have any results
(e.g. you previously executed an INSERT/UPDATE/DELETE) - in that
case, use the `count()` function.
    
This should fail if the RPC is not currently in `@Active` mode.

    @Active int count()

Gets the affected record count for the query. This should be safe to call if
the result set was generated from a SELECT query (the API should not return
a fault), but the result of that call is not defined. 

This should fail if the RPC is not currently in `@Active` mode.

    @Active List<Map<String, String>> page(int max)
    
This returns up to `max` rows of data from the current result set, but may
return fewer if there are fewer than `max` rows available.

If the result set is exhausted and there are no more rows left, this
should return an empty list. Otherwise, this should always return at
least one row (or fail), even if the caller would have to wait for it.

This should fail if the RPC is not currently in `@Active` mode, or if the 
argument is not positive.

    @Active boolean finish() -> @NoActive
   
Closes the current query, taking the server into `@NoActive` mode.

This should fail if the RPC is not currently in `@Active` mode.

    @NoActive void quit()
    
Terminates the server and stops accepting new requests.
