# FMS Monitor - AI Agent Documentation

## Tổng quan

AI Agent trong FMS Monitor sử dụng Microsoft Semantic Kernel kết hợp với Ollama để phân tích dữ liệu giám sát và đưa ra cảnh báo thông minh.

## Kiến trúc AI Agent

```
┌─────────────────────────────────────┐
│   Anomaly Detection Agent           │
│                                      │
│  ┌───────────────────────────────┐  │
│  │  Microsoft Semantic Kernel    │  │
│  │  - Chat Completion            │  │
│  │  - Context Management         │  │
│  │  - Prompt Engineering         │  │
│  └───────────┬───────────────────┘  │
│              │                       │
│              ▼                       │
│  ┌───────────────────────────────┐  │
│  │  Ollama Integration           │  │
│  │  - LLM: gpt-oss:120b-cloud    │  │
│  │  - Embedding: nomic-embed-text│  │
│  └───────────┬───────────────────┘  │
│              │                       │
│              ▼                       │
│  ┌───────────────────────────────┐  │
│  │  Analysis Pipeline            │  │
│  │  1. Collect context           │  │
│  │  2. Build prompt              │  │
│  │  3. Query LLM                 │  │
│  │  4. Parse response            │  │
│  │  5. Generate recommendations  │  │
│  └───────────────────────────────┘  │
└─────────────────────────────────────┘
```

## Ollama Models

### LLM: gpt-oss:120b-cloud

**Mô tả**: Large Language Model với 120 billion parameters, được tối ưu cho cloud deployment.

**Sử dụng**:
- Phân tích ngữ cảnh
- Dự đoán sự cố
- Đưa ra khuyến nghị

**Yêu cầu phần cứng**:
- RAM: 64GB minimum (128GB recommended)
- GPU: NVIDIA with 24GB+ VRAM (A100/H100 recommended)
- Storage: 100GB+

**Installation**:
```bash
ollama pull gpt-oss:120b-cloud
```

### Embedding: nomic-embed-text

**Mô tả**: Text embedding model với 768 dimensions, tối ưu cho similarity search.

**Sử dụng**:
- Tạo vector embeddings cho dữ liệu
- Tìm kiếm pattern tương tự
- Clustering anomalies

**Yêu cầu phần cứng**:
- RAM: 2GB
- GPU: Optional (faster with GPU)
- Storage: 1GB

**Installation**:
```bash
ollama pull nomic-embed-text
```

## Agent Workflow

### 1. Trigger Analysis

Agent được kích hoạt khi:
- Giá trị Ampe vượt ngưỡng MIN/MAX
- Số lần dao động vượt giới hạn cho phép
- Manual trigger từ API

### 2. Context Collection

Agent thu thập:
```csharp
AgentContext {
    MachineCode
    MachineName
    RecentData (30 minutes)
    RecentAlerts (24 hours)
    HistoricalData
}
```

### 3. Prompt Building

Agent xây dựng prompt với:
- Machine information
- Configuration thresholds
- Recent amperage data
- Fluctuation analysis
- Historical alerts

**Example Prompt**:
```
# Thông tin máy
- Mã máy: MC01
- Tên máy: Máy CNC 01

# Cấu hình giám sát
- Ngưỡng MIN: 100A
- Ngưỡng MAX: 130A
- Dao động bình thường: 115A - 125A
- Cửa sổ giám sát: 5 phút
- Số dao động cho phép: 1

# Phân tích dao động hiện tại
- Thời gian: 2024-11-04 10:25 - 10:30
- Số dao động phát hiện: 3
- Bất thường: CÓ
- Lý do: Phát hiện 3 dao động vượt ngưỡng trong 5 phút

# Dữ liệu gần đây
- 10:30:00: 135A
- 10:29:00: 132A
- 10:28:00: 128A
- 10:27:00: 118A
- 10:26:00: 120A

# Yêu cầu
Dựa trên thông tin trên, hãy:
1. Phân tích tình trạng hiện tại của máy
2. Đánh giá mức độ nghiêm trọng
3. Dự đoán vấn đề có thể xảy ra
4. Đưa ra khuyến nghị cụ thể
```

### 4. LLM Query

Agent gọi Ollama API:

```csharp
var response = await chatService.GetChatMessageContentAsync(chatHistory);
```

### 5. Response Parsing

Agent parse response theo format:

```json
{
  "isAbnormal": true,
  "analysis": "Máy CNC 01 đang có dấu hiệu quá tải...",
  "suggestedAlertLevel": 2,
  "recommendations": [
    "Kiểm tra và thay dao cắt",
    "Giảm tốc độ cắt xuống 80%",
    "Kiểm tra độ cứng vật liệu"
  ],
  "predictedIssue": "Mòn dao cắt"
}
```

### 6. Alert Generation

Tạo Alert object:
```csharp
Alert {
    Level = aiAnalysis.SuggestedAlertLevel
    Type = AlertType.AbnormalFluctuation
    Message = fluctuationAnalysis.Reason
    AiAnalysis = aiAnalysis.Analysis
}
```

## Vector Embeddings & Similarity Search

### Creating Embeddings

```csharp
var text = $"Machine: {machineCode}, Value: {amperageValue}A, Time: {recordedAt}";
var embedding = await ollamaClient.GetEmbeddings(new EmbedRequest {
    Model = "nomic-embed-text",
    Input = text
});
```

### Storing in PostgreSQL

```sql
INSERT INTO machine_amperage_vector
(machine_code, amperage_value, recorded_at, embedding)
VALUES ('MC01', 135, '2024-11-04 10:30:00', '[0.123, 0.456, ...]'::vector(768));
```

### Similarity Search

```sql
SELECT * FROM machine_amperage_vector
ORDER BY embedding <-> '[query_embedding]'::vector(768)
LIMIT 10;
```

### Pattern Learning

Agent tự động học patterns:

```csharp
// Khi phát hiện anomaly mới
var pattern = new AnomalyPattern {
    MachineCode = machineCode,
    PatternName = "High Fluctuation Pattern #1",
    PatternData = JsonSerializer.Serialize(data),
    Embedding = await GenerateEmbedding(data),
    SeverityLevel = AlertLevel.Warning
};

await SavePatternAsync(pattern);
```

### Finding Similar Patterns

```sql
SELECT * FROM find_similar_patterns(
    query_embedding := '[embedding]'::vector(768),
    similarity_threshold := 0.8,
    max_results := 10
);
```

## Alert Level Determination

Agent xác định alert level dựa trên:

### Fluctuation Ratio

```csharp
var fluctuationRatio = (double)actualFluctuations / allowedFluctuations;

if (fluctuationRatio <= 1.0)
    alertLevel = AlertLevel.Info;
else if (fluctuationRatio <= 2.0)
    alertLevel = AlertLevel.Warning;
else if (fluctuationRatio <= 3.0)
    alertLevel = AlertLevel.Critical;
else
    alertLevel = AlertLevel.Emergency;
```

### Historical Context

- Nếu đã có cảnh báo tương tự trong 24h: Tăng level
- Nếu tần suất cảnh báo cao: Tăng level
- Nếu có trend tăng dần: Tăng level

### Severity Mapping

| Level | Value | Description | Email Recipients |
|-------|-------|-------------|------------------|
| Info | 0 | Thông tin | - |
| Warning | 1 | Cảnh báo | Nhân viên trực tiếp |
| Critical | 2 | Nghiêm trọng | Nhân viên + Trưởng ca |
| Emergency | 3 | Khẩn cấp | Tất cả |

## Customizing AI Agent

### System Prompt

Chỉnh sửa trong `AnomalyDetectionAgent.cs`:

```csharp
private string GetSystemPrompt()
{
    return @"Bạn là một chuyên gia AI trong lĩnh vực...

    Khi phân tích, hãy xem xét:
    1. Xu hướng thay đổi của dòng điện
    2. Tần suất dao động
    3. ...

    [Customize your instructions here]
    ";
}
```

### Response Format

Định nghĩa format JSON mong muốn:

```csharp
// Thêm fields mới vào AgentAnalysisResponse
public class AgentAnalysisResponse
{
    // Existing fields...

    // New custom fields
    public double ConfidenceScore { get; set; }
    public string RootCause { get; set; }
    public List<string> RelatedPatterns { get; set; }
}
```

### Custom Analysis Logic

Override phương thức phân tích:

```csharp
public class CustomAnomalyDetectionAgent : AnomalyDetectionAgent
{
    public override async Task<AgentAnalysisResponse> AnalyzeAsync(
        AgentAnalysisRequest request,
        AgentContext context)
    {
        // Add custom pre-processing
        var preprocessedData = PreProcessData(request.RecentData);

        // Call base implementation
        var baseResponse = await base.AnalyzeAsync(request, context);

        // Add custom post-processing
        var enhancedResponse = EnhanceResponse(baseResponse);

        return enhancedResponse;
    }
}
```

## Performance Optimization

### Caching Embeddings

```csharp
private readonly MemoryCache _embeddingCache = new();

private async Task<float[]> GetEmbeddingWithCache(string text)
{
    if (_embeddingCache.TryGetValue(text, out float[] cached))
        return cached;

    var embedding = await GenerateEmbedding(text);
    _embeddingCache.Set(text, embedding, TimeSpan.FromHours(1));
    return embedding;
}
```

### Batch Processing

```csharp
// Process multiple machines in parallel
var tasks = machines.Select(m => AnalyzeMachineAsync(m));
await Task.WhenAll(tasks);
```

### Request Throttling

```csharp
private readonly SemaphoreSlim _throttle = new(5); // Max 5 concurrent

public async Task<AgentAnalysisResponse> AnalyzeAsync(...)
{
    await _throttle.WaitAsync();
    try
    {
        return await PerformAnalysisAsync(...);
    }
    finally
    {
        _throttle.Release();
    }
}
```

## Monitoring AI Agent

### Metrics to Track

1. **Response Time**: Thời gian phân tích
2. **Accuracy**: Độ chính xác dự đoán
3. **False Positives**: Cảnh báo sai
4. **False Negatives**: Miss cảnh báo
5. **LLM Token Usage**: Chi phí API

### Logging

```csharp
_logger.LogInformation("AI Agent analyzing {MachineCode}, Context: {ContextSize} items",
    machineCode, context.RecentData.Count);

_logger.LogInformation("AI Analysis completed in {Duration}ms, Result: {IsAbnormal}",
    duration, result.IsAbnormal);
```

### Error Handling

```csharp
try
{
    var analysis = await _anomalyAgent.AnalyzeAsync(request, context);
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "Ollama API unavailable");
    // Fallback to rule-based detection
    analysis = RuleBasedAnalysis(request);
}
catch (Exception ex)
{
    _logger.LogError(ex, "AI Agent error");
    // Return safe default
    analysis = GetDefaultAnalysis();
}
```

## Testing AI Agent

### Unit Tests

```csharp
[Fact]
public async Task AnalyzeAsync_ShouldDetectAbnormal_WhenHighFluctuation()
{
    // Arrange
    var agent = new AnomalyDetectionAgent(config, logger);
    var request = new AgentAnalysisRequest {
        FluctuationAnalysis = new FluctuationAnalysis {
            FluctuationCount = 5,
            IsAbnormal = true
        }
    };

    // Act
    var result = await agent.AnalyzeAsync(request, context);

    // Assert
    Assert.True(result.IsAbnormal);
    Assert.True(result.SuggestedAlertLevel >= AlertLevel.Warning);
}
```

### Integration Tests

```csharp
[Fact]
public async Task FullPipeline_ShouldSendAlert_WhenAnomalyDetected()
{
    // Arrange: Setup test machine with anomalous data
    await InsertTestDataAsync();

    // Act: Trigger monitoring
    await _monitoringService.MonitorAllMachinesAsync();

    // Assert: Verify alert created and email sent
    var alerts = await _alertRepository.GetUnresolvedAsync();
    Assert.NotEmpty(alerts);

    var emailSent = await VerifyEmailSentAsync();
    Assert.True(emailSent);
}
```

## Best Practices

1. **Prompt Engineering**: Dành thời gian tối ưu prompts
2. **Context Window**: Không gửi quá nhiều data (limit 30 mins)
3. **Caching**: Cache embeddings và responses khi có thể
4. **Fallback**: Luôn có rule-based backup khi AI fail
5. **Monitoring**: Track performance và accuracy
6. **Feedback Loop**: Thu thập feedback để cải thiện
7. **Version Control**: Version prompts như code
8. **A/B Testing**: Test different prompts/models

## Troubleshooting

### Agent không trả về kết quả

```bash
# Check Ollama service
curl http://localhost:11434/api/tags

# Check models
ollama list

# Test model
ollama run gpt-oss:120b-cloud "Hello"
```

### Response parsing failed

- Kiểm tra format JSON trong response
- Thêm fallback parsing logic
- Log raw response để debug

### Slow response time

- Giảm context size
- Sử dụng smaller model cho testing
- Enable GPU acceleration
- Implement caching

### High false positive rate

- Tune thresholds trong MonitoringConfiguration
- Adjust system prompt
- Thêm more context (historical data)
- Implement feedback mechanism

---

Để biết thêm chi tiết, tham khảo:
- [Microsoft Semantic Kernel Docs](https://learn.microsoft.com/en-us/semantic-kernel/)
- [Ollama Documentation](https://ollama.ai/docs)
- [pgvector Guide](https://github.com/pgvector/pgvector)
