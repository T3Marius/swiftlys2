using Dapper;
using System.Data.SQLite;
using SwiftlyS2.Core.Natives;
using System.Data;
using System.Collections.Concurrent;
using MySqlConnector;
using Npgsql;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Core.Services;
using SwiftlyS2.Shared.Database;

namespace SwiftlyS2.Core.Database;

internal class DatabaseService : IDatabaseService
{

  private ILogger<DatabaseService> _Logger { get; init; }
  private CoreContext _Context { get; init; }

  private ConcurrentDictionary<string, Func<IDbConnection>> _connectionStrings = new ConcurrentDictionary<string, Func<IDbConnection>>();

  public DatabaseService( ILogger<DatabaseService> logger, CoreContext context )
  {
    _Logger = logger;
    _Context = context;
  }

  public string GetConnectionString( string connectionName )
  {
    if (NativeDatabase.ConnectionExists(connectionName))
    {
      return NativeDatabase.GetCredentials(connectionName);
    }
    return NativeDatabase.GetDefaultConnectionCredentials();
  }

  private Func<IDbConnection> ResolveConnectionString(string connectionString)
  {
    if (_connectionStrings.TryGetValue(connectionString, out var cached))
      return cached;

    try
    {
      var factory = CreateConnectionFactory(connectionString);
      _connectionStrings.TryAdd(connectionString, factory);
      return factory;
    }
    catch (Exception e)
    {
      if (!GlobalExceptionHandler.Handle(e)) throw;
      _Logger.LogError(e, "Failed to parse connection string. Expected format: protocol://user:password@host:port/database");
      throw;
    }
  }

  private static Func<IDbConnection> CreateConnectionFactory(string connectionString)
  {
    // Format: protocol://user:password@host:port/database (password may contain special chars like @)
    if (connectionString.StartsWith("sqlite://"))
      return () => new SQLiteConnection($"Data Source={connectionString[9..]}");

    var protoEnd = connectionString.IndexOf("://");
    var lastAt = connectionString.LastIndexOf('@');
    var slash = connectionString.IndexOf('/', lastAt);

    if (protoEnd < 0 || lastAt < protoEnd || slash < 0)
      throw new FormatException("Expected format: protocol://user:password@host:port/database");

    var protocol = connectionString[..protoEnd];
    var credentials = connectionString[(protoEnd + 3)..lastAt];
    var userEnd = credentials.IndexOf(':');

    if (userEnd < 0)
      throw new FormatException("Expected format: protocol://user:password@host:port/database");

    var (defaultPort, factory) = protocol switch
    {
      "mysql" => ("3306", (Func<string, IDbConnection>)(cs => new MySqlConnection(cs))),
      "postgresql" => ("5432", (Func<string, IDbConnection>)(cs => new NpgsqlConnection(cs))),
      _ => throw new NotSupportedException($"Unsupported protocol: {protocol}")
    };

    var hostPort = connectionString[(lastAt + 1)..slash];
    var portColon = hostPort.LastIndexOf(':');
    var host = portColon > 0 ? hostPort[..portColon] : hostPort;
    var port = portColon > 0 ? hostPort[(portColon + 1)..] : defaultPort;

    var connStr = $"Server={host};"
               + $"Port={port};"
               + $"Database={connectionString[(slash + 1)..]};"
               + $"User ID={credentials[..userEnd]};"
               + $"Password={credentials[(userEnd + 1)..]}";

    return () => factory(connStr);
  }

  public IDbConnection GetConnection( string connectionName )
  {
    var connectionString = GetConnectionString(connectionName);
    return ResolveConnectionString(connectionString)();
  }
}