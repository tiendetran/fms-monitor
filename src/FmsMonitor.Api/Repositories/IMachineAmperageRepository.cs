using FmsMonitor.Api.Models;

namespace FmsMonitor.Api.Repositories;

public interface IMachineAmperageRepository
{
    Task<IEnumerable<MachineAmperage>> GetByMachineCodeAsync(string machineCode, DateTime fromDate, DateTime toDate);
    Task<IEnumerable<MachineAmperage>> GetRecentDataAsync(string machineCode, int minutes);
    Task<MachineAmperage?> GetLatestAsync(string machineCode);
    Task<int> InsertAsync(MachineAmperage amperage);
    Task<IEnumerable<MachineAmperage>> GetAllRecentAsync(int minutes);
}

public interface IMonitoringConfigurationRepository
{
    Task<IEnumerable<MonitoringConfiguration>> GetAllActiveAsync();
    Task<MonitoringConfiguration?> GetByMachineCodeAsync(string machineCode);
    Task<int> InsertAsync(MonitoringConfiguration config);
    Task<int> UpdateAsync(MonitoringConfiguration config);
    Task<int> DeleteAsync(int id);
}

public interface IAlertRepository
{
    Task<int> InsertAsync(Alert alert);
    Task<int> UpdateAsync(Alert alert);
    Task<IEnumerable<Alert>> GetRecentByMachineCodeAsync(string machineCode, int hours);
    Task<IEnumerable<Alert>> GetUnresolvedAsync();
    Task<Alert?> GetByIdAsync(int id);
}

public interface IEmailRecipientRepository
{
    Task<IEnumerable<EmailRecipient>> GetAllActiveAsync();
    Task<IEnumerable<EmailRecipient>> GetByAlertLevelAsync(AlertLevel minLevel);
    Task<int> InsertAsync(EmailRecipient recipient);
    Task<int> UpdateAsync(EmailRecipient recipient);
}
