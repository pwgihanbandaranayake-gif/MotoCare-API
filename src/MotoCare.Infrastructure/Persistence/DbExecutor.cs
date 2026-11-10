using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MotoCare.Application.Abstractions;

namespace MotoCare.Infrastructure.Persistence;

/// <summary>
/// Thin Dapper wrapper. Every database call in the project goes through one of these methods
/// and runs a stored procedure (never inline SQL).
/// </summary>
public sealed class DbExecutor(IConfiguration config)
{
    private readonly string _connectionString = config.GetConnectionString("MotoCareDb")
        ?? throw new InvalidOperationException("Connection string 'MotoCareDb' is missing in appsettings.json.");

    private SqlConnection Open() => new(_connectionString);

    /// <summary>All rows of the first result set.</summary>
    public async Task<List<RowData>> ListAsync(string proc, object? param = null)
    {
        await using var cn = Open();
        IEnumerable<object> rows = await cn.QueryAsync(proc, param, commandType: CommandType.StoredProcedure);
        return rows.Select(ToRow).ToList();
    }

    /// <summary>First row of the first result set (or null).</summary>
    public async Task<RowData?> SingleAsync(string proc, object? param = null)
    {
        await using var cn = Open();
        object? row = await cn.QueryFirstOrDefaultAsync(proc, param, commandType: CommandType.StoredProcedure);
        return row is null ? null : ToRow(row);
    }

    /// <summary>
    /// Reads several result sets into a named bag.
    /// A name ending with '?' is read as a single row, e.g. MultiAsync(proc, p, "invoice?", "items").
    /// </summary>
    public async Task<ResultBag> MultiAsync(string proc, object? param, params string[] sets)
    {
        await using var cn = Open();
        using var grid = await cn.QueryMultipleAsync(proc, param, commandType: CommandType.StoredProcedure);

        var bag = new ResultBag();
        foreach (var name in sets)
        {
            IEnumerable<object> raw = await grid.ReadAsync();
            var rows = raw.Select(ToRow).ToList();
            if (name.EndsWith('?')) bag[name.TrimEnd('?')] = rows.FirstOrDefault();
            else bag[name] = rows;
        }
        return bag;
    }

    /// <summary>Runs a save procedure that ends with "SELECT @Id AS Id" and returns the id.</summary>
    public async Task<int> SaveAsync(string proc, object? param)
    {
        await using var cn = Open();
        return await cn.ExecuteScalarAsync<int>(proc, param, commandType: CommandType.StoredProcedure);
    }

    /// <summary>First column of the first row.</summary>
    public async Task<T?> ScalarAsync<T>(string proc, object? param)
    {
        await using var cn = Open();
        return await cn.ExecuteScalarAsync<T>(proc, param, commandType: CommandType.StoredProcedure);
    }

    /// <summary>Runs a procedure that returns nothing.</summary>
    public async Task ExecAsync(string proc, object? param)
    {
        await using var cn = Open();
        await cn.ExecuteAsync(proc, param, commandType: CommandType.StoredProcedure);
    }

    /// <summary>First row mapped to a typed class.</summary>
    public async Task<T?> FirstAsync<T>(string proc, object? param)
    {
        await using var cn = Open();
        return await cn.QueryFirstOrDefaultAsync<T>(proc, param, commandType: CommandType.StoredProcedure);
    }

    /// <summary>Dapper row -> dictionary (DBNull becomes null).</summary>
    private static RowData ToRow(object r)
    {
        var row = new RowData();
        foreach (var kv in (IDictionary<string, object>)r)
            row[kv.Key] = kv.Value is DBNull ? null : kv.Value;
        return row;
    }
}
