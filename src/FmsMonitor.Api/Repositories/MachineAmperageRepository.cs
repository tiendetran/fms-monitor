using Dapper;
using FmsMonitor.Api.Models;
using Microsoft.Data.SqlClient;

namespace FmsMonitor.Api.Repositories;

public class MachineAmperageRepository : IMachineAmperageRepository
{
    private readonly string _connectionString;

    public MachineAmperageRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SqlServer")
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<IEnumerable<MachineAmperage>> GetByMachineCodeAsync(string machineCode, DateTime fromDate, DateTime toDate)
    {
        const string sql = @"
            SELECT Id, MachineCode, MachineName, AmperageValue, RecordedAt, Status, Notes
            FROM MachineAmperage
            WHERE MachineCode = @MachineCode
                AND RecordedAt >= @FromDate
                AND RecordedAt <= @ToDate
            ORDER BY RecordedAt DESC";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<MachineAmperage>(sql, new { MachineCode = machineCode, FromDate = fromDate, ToDate = toDate });
    }

    public async Task<IEnumerable<MachineAmperage>> GetRecentDataAsync(string machineCode, int minutes)
    {
        var fromDate = DateTime.UtcNow.AddMinutes(-minutes);
        const string sql = @"
            SELECT Id, MachineCode, MachineName, AmperageValue, RecordedAt, Status, Notes
            FROM MachineAmperage
            WHERE MachineCode = @MachineCode AND RecordedAt >= @FromDate
            ORDER BY RecordedAt DESC";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<MachineAmperage>(sql, new { MachineCode = machineCode, FromDate = fromDate });
    }

    public async Task<MachineAmperage?> GetLatestAsync(string machineCode)
    {
        const string sql = @"
            SELECT TOP 1 Id, MachineCode, MachineName, AmperageValue, RecordedAt, Status, Notes
            FROM MachineAmperage
            WHERE MachineCode = @MachineCode
            ORDER BY RecordedAt DESC";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<MachineAmperage>(sql, new { MachineCode = machineCode });
    }

    public async Task<int> InsertAsync(MachineAmperage amperage)
    {
        const string sql = @"
            INSERT INTO MachineAmperage (MachineCode, MachineName, AmperageValue, RecordedAt, Status, Notes)
            VALUES (@MachineCode, @MachineName, @AmperageValue, @RecordedAt, @Status, @Notes);
            SELECT CAST(SCOPE_IDENTITY() as int)";

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(sql, amperage);
    }

    public async Task<IEnumerable<MachineAmperage>> GetAllRecentAsync(int minutes)
    {
        var fromDate = DateTime.UtcNow.AddMinutes(-minutes);
        const string sql = @"
            SELECT Id, MachineCode, MachineName, AmperageValue, RecordedAt, Status, Notes
            FROM MachineAmperage
            WHERE RecordedAt >= @FromDate
            ORDER BY MachineCode, RecordedAt DESC";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<MachineAmperage>(sql, new { FromDate = fromDate });
    }
}

public class MonitoringConfigurationRepository : IMonitoringConfigurationRepository
{
    private readonly string _connectionString;

    public MonitoringConfigurationRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SqlServer")
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<IEnumerable<MonitoringConfiguration>> GetAllActiveAsync()
    {
        const string sql = @"
            SELECT Id, MachineCode, MachineName, MinAmperage, MaxAmperage,
                   FluctuationMinAmperage, FluctuationMaxAmperage, MonitoringWindowMinutes,
                   AllowedFluctuationCount, DataFetchIntervalSeconds, IsActive, CreatedAt, UpdatedAt
            FROM MonitoringConfiguration
            WHERE IsActive = 1
            ORDER BY MachineCode";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<MonitoringConfiguration>(sql);
    }

    public async Task<MonitoringConfiguration?> GetByMachineCodeAsync(string machineCode)
    {
        const string sql = @"
            SELECT Id, MachineCode, MachineName, MinAmperage, MaxAmperage,
                   FluctuationMinAmperage, FluctuationMaxAmperage, MonitoringWindowMinutes,
                   AllowedFluctuationCount, DataFetchIntervalSeconds, IsActive, CreatedAt, UpdatedAt
            FROM MonitoringConfiguration
            WHERE MachineCode = @MachineCode";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<MonitoringConfiguration>(sql, new { MachineCode = machineCode });
    }

    public async Task<int> InsertAsync(MonitoringConfiguration config)
    {
        const string sql = @"
            INSERT INTO MonitoringConfiguration
            (MachineCode, MachineName, MinAmperage, MaxAmperage, FluctuationMinAmperage,
             FluctuationMaxAmperage, MonitoringWindowMinutes, AllowedFluctuationCount,
             DataFetchIntervalSeconds, IsActive, CreatedAt)
            VALUES
            (@MachineCode, @MachineName, @MinAmperage, @MaxAmperage, @FluctuationMinAmperage,
             @FluctuationMaxAmperage, @MonitoringWindowMinutes, @AllowedFluctuationCount,
             @DataFetchIntervalSeconds, @IsActive, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() as int)";

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(sql, config);
    }

    public async Task<int> UpdateAsync(MonitoringConfiguration config)
    {
        const string sql = @"
            UPDATE MonitoringConfiguration
            SET MachineName = @MachineName,
                MinAmperage = @MinAmperage,
                MaxAmperage = @MaxAmperage,
                FluctuationMinAmperage = @FluctuationMinAmperage,
                FluctuationMaxAmperage = @FluctuationMaxAmperage,
                MonitoringWindowMinutes = @MonitoringWindowMinutes,
                AllowedFluctuationCount = @AllowedFluctuationCount,
                DataFetchIntervalSeconds = @DataFetchIntervalSeconds,
                IsActive = @IsActive,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, config);
    }

    public async Task<int> DeleteAsync(int id)
    {
        const string sql = "DELETE FROM MonitoringConfiguration WHERE Id = @Id";
        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { Id = id });
    }
}

public class AlertRepository : IAlertRepository
{
    private readonly string _connectionString;

    public AlertRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SqlServer")
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<int> InsertAsync(Alert alert)
    {
        const string sql = @"
            INSERT INTO Alert
            (MachineCode, MachineName, Level, Type, CurrentValue, Message, AiAnalysis,
             DetectedAt, IsNotified, NotifiedAt, IsResolved, ResolvedAt, ResolvedBy)
            VALUES
            (@MachineCode, @MachineName, @Level, @Type, @CurrentValue, @Message, @AiAnalysis,
             @DetectedAt, @IsNotified, @NotifiedAt, @IsResolved, @ResolvedAt, @ResolvedBy);
            SELECT CAST(SCOPE_IDENTITY() as int)";

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(sql, alert);
    }

    public async Task<int> UpdateAsync(Alert alert)
    {
        const string sql = @"
            UPDATE Alert
            SET IsNotified = @IsNotified,
                NotifiedAt = @NotifiedAt,
                IsResolved = @IsResolved,
                ResolvedAt = @ResolvedAt,
                ResolvedBy = @ResolvedBy
            WHERE Id = @Id";

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, alert);
    }

    public async Task<IEnumerable<Alert>> GetRecentByMachineCodeAsync(string machineCode, int hours)
    {
        var fromDate = DateTime.UtcNow.AddHours(-hours);
        const string sql = @"
            SELECT Id, MachineCode, MachineName, Level, Type, CurrentValue, Message, AiAnalysis,
                   DetectedAt, IsNotified, NotifiedAt, IsResolved, ResolvedAt, ResolvedBy
            FROM Alert
            WHERE MachineCode = @MachineCode AND DetectedAt >= @FromDate
            ORDER BY DetectedAt DESC";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<Alert>(sql, new { MachineCode = machineCode, FromDate = fromDate });
    }

    public async Task<IEnumerable<Alert>> GetUnresolvedAsync()
    {
        const string sql = @"
            SELECT Id, MachineCode, MachineName, Level, Type, CurrentValue, Message, AiAnalysis,
                   DetectedAt, IsNotified, NotifiedAt, IsResolved, ResolvedAt, ResolvedBy
            FROM Alert
            WHERE IsResolved = 0
            ORDER BY Level DESC, DetectedAt DESC";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<Alert>(sql);
    }

    public async Task<Alert?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT Id, MachineCode, MachineName, Level, Type, CurrentValue, Message, AiAnalysis,
                   DetectedAt, IsNotified, NotifiedAt, IsResolved, ResolvedAt, ResolvedBy
            FROM Alert
            WHERE Id = @Id";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<Alert>(sql, new { Id = id });
    }
}

public class EmailRecipientRepository : IEmailRecipientRepository
{
    private readonly string _connectionString;

    public EmailRecipientRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SqlServer")
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<IEnumerable<EmailRecipient>> GetAllActiveAsync()
    {
        const string sql = @"
            SELECT Id, Name, Email, Role, MinAlertLevel, IsActive, CreatedAt
            FROM EmailRecipient
            WHERE IsActive = 1
            ORDER BY MinAlertLevel, Name";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<EmailRecipient>(sql);
    }

    public async Task<IEnumerable<EmailRecipient>> GetByAlertLevelAsync(AlertLevel minLevel)
    {
        const string sql = @"
            SELECT Id, Name, Email, Role, MinAlertLevel, IsActive, CreatedAt
            FROM EmailRecipient
            WHERE IsActive = 1 AND MinAlertLevel <= @MinLevel
            ORDER BY MinAlertLevel, Name";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<EmailRecipient>(sql, new { MinLevel = (int)minLevel });
    }

    public async Task<int> InsertAsync(EmailRecipient recipient)
    {
        const string sql = @"
            INSERT INTO EmailRecipient (Name, Email, Role, MinAlertLevel, IsActive, CreatedAt)
            VALUES (@Name, @Email, @Role, @MinAlertLevel, @IsActive, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() as int)";

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(sql, recipient);
    }

    public async Task<int> UpdateAsync(EmailRecipient recipient)
    {
        const string sql = @"
            UPDATE EmailRecipient
            SET Name = @Name,
                Email = @Email,
                Role = @Role,
                MinAlertLevel = @MinAlertLevel,
                IsActive = @IsActive
            WHERE Id = @Id";

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, recipient);
    }
}
