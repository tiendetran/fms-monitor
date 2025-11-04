using Dapper;
using FmsMonitor.Api.Models;
using Npgsql;

namespace FmsMonitor.Api.Repositories;

/// <summary>
/// Repository cho PostgreSQL với pgvector để lưu trữ và tìm kiếm dữ liệu với embeddings
/// </summary>
public interface IPostgresVectorRepository
{
    Task<int> InsertAmperageWithEmbeddingAsync(MachineAmperage amperage, float[] embedding);
    Task<IEnumerable<MachineAmperage>> SearchSimilarPatternsAsync(float[] queryEmbedding, int limit = 10);
    Task<bool> TestConnectionAsync();
}

public class PostgresVectorRepository : IPostgresVectorRepository
{
    private readonly string _connectionString;

    public PostgresVectorRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<int> InsertAmperageWithEmbeddingAsync(MachineAmperage amperage, float[] embedding)
    {
        const string sql = @"
            INSERT INTO machine_amperage_vector
            (machine_code, machine_name, amperage_value, recorded_at, status, notes, embedding)
            VALUES
            (@MachineCode, @MachineName, @AmperageValue, @RecordedAt, @Status, @Notes, @Embedding)
            RETURNING id";

        using var connection = new NpgsqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            amperage.MachineCode,
            amperage.MachineName,
            amperage.AmperageValue,
            amperage.RecordedAt,
            amperage.Status,
            amperage.Notes,
            Embedding = embedding
        });
    }

    public async Task<IEnumerable<MachineAmperage>> SearchSimilarPatternsAsync(float[] queryEmbedding, int limit = 10)
    {
        const string sql = @"
            SELECT id as Id, machine_code as MachineCode, machine_name as MachineName,
                   amperage_value as AmperageValue, recorded_at as RecordedAt,
                   status as Status, notes as Notes,
                   embedding <-> @QueryEmbedding as distance
            FROM machine_amperage_vector
            ORDER BY embedding <-> @QueryEmbedding
            LIMIT @Limit";

        using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QueryAsync<MachineAmperage>(sql, new
        {
            QueryEmbedding = queryEmbedding,
            Limit = limit
        });
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
