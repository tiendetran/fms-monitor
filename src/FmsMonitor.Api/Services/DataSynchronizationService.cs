using FmsMonitor.Api.Models;
using FmsMonitor.Api.Repositories;
using OllamaSharp;

namespace FmsMonitor.Api.Services;

/// <summary>
/// Service đồng bộ dữ liệu từ SQL Server sang PostgreSQL với pgvector
/// </summary>
public interface IDataSynchronizationService
{
    Task SynchronizeRecentDataAsync(int minutes = 60);
    Task SynchronizeByMachineCodeAsync(string machineCode, DateTime fromDate, DateTime toDate);
}

public class DataSynchronizationService : IDataSynchronizationService
{
    private readonly IMachineAmperageRepository _sqlRepository;
    private readonly IPostgresVectorRepository _pgRepository;
    private readonly IOllamaApiClient _ollamaClient;
    private readonly ILogger<DataSynchronizationService> _logger;

    public DataSynchronizationService(
        IMachineAmperageRepository sqlRepository,
        IPostgresVectorRepository pgRepository,
        IOllamaApiClient ollamaClient,
        ILogger<DataSynchronizationService> logger)
    {
        _sqlRepository = sqlRepository;
        _pgRepository = pgRepository;
        _ollamaClient = ollamaClient;
        _logger = logger;
    }

    public async Task SynchronizeRecentDataAsync(int minutes = 60)
    {
        try
        {
            _logger.LogInformation("Bắt đầu đồng bộ dữ liệu {Minutes} phút gần nhất", minutes);

            var recentData = await _sqlRepository.GetAllRecentAsync(minutes);
            var dataList = recentData.ToList();

            _logger.LogInformation("Tìm thấy {Count} bản ghi cần đồng bộ", dataList.Count);

            foreach (var data in dataList)
            {
                try
                {
                    // Tạo embedding từ dữ liệu
                    var embedding = await GenerateEmbeddingAsync(data);

                    // Lưu vào PostgreSQL với embedding
                    await _pgRepository.InsertAmperageWithEmbeddingAsync(data, embedding);

                    _logger.LogDebug("Đã đồng bộ dữ liệu máy {MachineCode} tại {RecordedAt}",
                        data.MachineCode, data.RecordedAt);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi đồng bộ dữ liệu máy {MachineCode}", data.MachineCode);
                }
            }

            _logger.LogInformation("Hoàn thành đồng bộ {Count} bản ghi", dataList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong quá trình đồng bộ dữ liệu");
            throw;
        }
    }

    public async Task SynchronizeByMachineCodeAsync(string machineCode, DateTime fromDate, DateTime toDate)
    {
        try
        {
            _logger.LogInformation("Đồng bộ dữ liệu máy {MachineCode} từ {FromDate} đến {ToDate}",
                machineCode, fromDate, toDate);

            var data = await _sqlRepository.GetByMachineCodeAsync(machineCode, fromDate, toDate);
            var dataList = data.ToList();

            foreach (var item in dataList)
            {
                try
                {
                    var embedding = await GenerateEmbeddingAsync(item);
                    await _pgRepository.InsertAmperageWithEmbeddingAsync(item, embedding);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi đồng bộ bản ghi {Id}", item.Id);
                }
            }

            _logger.LogInformation("Đã đồng bộ {Count} bản ghi cho máy {MachineCode}", dataList.Count, machineCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi đồng bộ dữ liệu máy {MachineCode}", machineCode);
            throw;
        }
    }

    private async Task<float[]> GenerateEmbeddingAsync(MachineAmperage data)
    {
        // Tạo text để embedding
        var text = $"Machine: {data.MachineCode}, Value: {data.AmperageValue}A, Time: {data.RecordedAt:yyyy-MM-dd HH:mm:ss}, Status: {data.Status}";

        try
        {
            // Sử dụng Ollama để tạo embedding
            var embeddingResponse = await _ollamaClient.GetEmbeddings(
                new EmbedRequest
                {
                    Model = "nomic-embed-text",
                    Input = text
                });

            return embeddingResponse.Embeddings.FirstOrDefault()?.ToArray() ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi tạo embedding cho text: {Text}", text);
            // Trả về vector rỗng hoặc vector mặc định
            return new float[768]; // nomic-embed-text có 768 dimensions
        }
    }
}
