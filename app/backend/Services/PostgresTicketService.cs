using NpgsqlTypes;

namespace MinimalApi.Services;

public class PostgresTicketService
{
    private readonly string _connectionString;

    public PostgresTicketService(IConfiguration config)
    {
        _connectionString = config["AZURE_POSTGRESQL_CONNECTION_STRING"] ?? throw new ArgumentNullException("AZURE_POSTGRESQL_CONNECTION_STRING");
    }

    public async Task<List<Dictionary<string, object>>> QueryTicketAsync(string sqlQuery, List<Dictionary<string, object>>? parameters)
    {
        var results = new List<Dictionary<string, object>>();

        // Initiate connection to PostgreSQL Server
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Prepare SQL query command
        await using (var cmd = new NpgsqlCommand(sqlQuery, connection))
        {
            // Add parameters to sql query
            if (parameters != null)
            {
                foreach (var paramDict in parameters)
                {
                    foreach (var kv in paramDict)
                    {
                        if (kv.Key == "type")
                        {
                            continue; // no need to process type parameters
                        }

                        var paramName = kv.Key;
                        var paramValue = kv.Value;
                        var type = paramDict.ContainsKey("type") ? paramDict["type"]?.ToString() : null;
                        var dbType = DefineDBType(type);

                        // Convert parameter value to final value
                        var finalValue = ConvertParamValue(paramValue, dbType);

                        // Add Parameters
                        cmd.Parameters.AddWithValue(paramName, dbType, finalValue ?? DBNull.Value);
                    }
                }
            }

            // Process query execution
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);
                }
                results.Add(row);
            }
        }

        return results;
    }

    // Helper method to define NpqsqlDbType based on type parameter
    private static NpgsqlDbType DefineDBType(string? type)
    {
        if (type == null)
        {
            return NpgsqlDbType.Unknown;
        }

        return type.ToUpperInvariant() switch
        {
            "VARCHAR" => NpgsqlDbType.Varchar,
            "TEXT" => NpgsqlDbType.Text,
            "INTEGER" => NpgsqlDbType.Integer,
            "FLOAT" => NpgsqlDbType.Double,
            "TIMESTAMP" => NpgsqlDbType.Timestamp,
            _ => NpgsqlDbType.Unknown
        };
    }

    // Helper method to convert parameter value from JsonElement into its respective data type
    private static object? ConvertParamValue(object? value, NpgsqlDbType dbType)
    {
        if (value == null)
        {
            return DBNull.Value;
        }

        // Convert JsonElement
        if (value is JsonElement je)
        {
            return dbType switch
            {
                NpgsqlDbType.Varchar or NpgsqlDbType.Text => je.ValueKind == JsonValueKind.String ? je.GetString() : null,
                NpgsqlDbType.Integer => je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var intValue) ? intValue : null,
                NpgsqlDbType.Double => je.ValueKind == JsonValueKind.Number && je.TryGetDouble(out var doubleValue) ? doubleValue : null,
                NpgsqlDbType.Timestamp => je.ValueKind == JsonValueKind.String && je.TryGetDateTime(out var dateTime) ? dateTime : null,
                _ => je.ToString()
            };
        }

        return value; // return as-is
    }
}