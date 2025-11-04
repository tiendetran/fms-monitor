# FMS Monitor - Hệ thống Giám sát Vận hành Máy trong Sản xuất

![.NET](https://img.shields.io/badge/.NET-9.0-blue)
![License](https://img.shields.io/badge/license-MIT-green)

Hệ thống giám sát thông minh tích hợp AI Agent để phát hiện bất thường và cảnh báo sớm các vấn đề trong vận hành máy móc sản xuất.

## 📋 Mục lục

- [Tính năng](#-tính-năng)
- [Công nghệ](#-công-nghệ)
- [Kiến trúc](#-kiến-trúc)
- [Cài đặt](#-cài-đặt)
- [Cấu hình](#-cấu-hình)
- [Sử dụng](#-sử-dụng)
- [API Endpoints](#-api-endpoints)
- [Triển khai](#-triển-khai)

## 🚀 Tính năng

### 1. Giám sát Real-time
- ✅ Thu thập dữ liệu Ampe từ máy móc theo thời gian thiết lập
- ✅ Kiểm tra ngưỡng MIN, MAX tự động
- ✅ Phát hiện dao động bất thường
- ✅ Background service chạy liên tục

### 2. AI Agent Phân tích Thông minh
- 🤖 Sử dụng Microsoft Semantic Kernel + Ollama
- 🧠 Phân tích ngữ cảnh và dự đoán sự cố
- 📊 Đưa ra khuyến nghị cụ thể
- 🎯 Học từ lịch sử cảnh báo

### 3. Đồng bộ dữ liệu
- 🔄 Đồng bộ từ SQL Server sang PostgreSQL với pgvector
- 🔍 Tìm kiếm pattern tương tự bằng vector embeddings
- 💾 Lưu trữ lịch sử phân tích AI

### 4. Cảnh báo thông minh
- 📧 Gửi email tự động theo cấp độ cảnh báo
- 👥 Phân quyền người nhận theo vai trò
- 🎨 Email HTML đẹp mắt với chi tiết phân tích
- 📈 Dashboard cảnh báo

## 🛠 Công nghệ

### Backend
- **Framework**: ASP.NET Core 9.0 Web API
- **ORM**: Dapper (high-performance)
- **Database**:
  - SQL Server (primary storage)
  - PostgreSQL + pgvector (vector search)

### AI/ML
- **LLM**: Ollama với model `gpt-oss:120b-cloud`
- **Embeddings**: `nomic-embed-text` (768 dimensions)
- **Agent Framework**: Microsoft Semantic Kernel
- **Client**: OllamaSharp

### Logging & Monitoring
- **Logging**: Serilog
- **Documentation**: Swagger/OpenAPI

### Email
- **Library**: MailKit

## 🏗 Kiến trúc

```
┌─────────────────┐
│   Máy móc CNC   │
│   Máy tiện, ...  │
└────────┬────────┘
         │ Dữ liệu Ampe
         ▼
┌─────────────────────────────────────┐
│     SQL Server (Primary DB)          │
│  - MachineAmperage                   │
│  - MonitoringConfiguration           │
│  - Alert                             │
│  - EmailRecipient                    │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│   FMS Monitor API (.NET 9)          │
│                                      │
│  ┌──────────────────────────────┐  │
│  │  Monitoring Service           │  │
│  │  - Check thresholds           │  │
│  │  - Analyze fluctuations       │  │
│  └──────────┬───────────────────┘  │
│             │                       │
│             ▼                       │
│  ┌──────────────────────────────┐  │
│  │  AI Agent (Semantic Kernel)   │  │
│  │  - Ollama Integration         │  │
│  │  - Context Analysis           │  │
│  │  - Pattern Recognition        │  │
│  └──────────┬───────────────────┘  │
│             │                       │
│             ▼                       │
│  ┌──────────────────────────────┐  │
│  │  Email Notification Service   │  │
│  │  - Alert by level             │  │
│  │  - HTML templates             │  │
│  └──────────────────────────────┘  │
│                                      │
│  ┌──────────────────────────────┐  │
│  │  Data Sync Service            │  │
│  │  - SQL → PostgreSQL           │  │
│  │  - Generate embeddings        │  │
│  └──────────┬───────────────────┘  │
└─────────────┼──────────────────────┘
              │
              ▼
┌─────────────────────────────────────┐
│  PostgreSQL + pgvector               │
│  - Vector embeddings                 │
│  - Similarity search                 │
│  - Pattern learning                  │
└─────────────────────────────────────┘
```

## 📦 Cài đặt

### Yêu cầu hệ thống

- .NET 9.0 SDK
- SQL Server 2019+
- PostgreSQL 14+ với pgvector extension
- Ollama với models:
  - `gpt-oss:120b-cloud`
  - `nomic-embed-text`

### Bước 1: Clone repository

```bash
git clone <repository-url>
cd fms-monitor
```

### Bước 2: Cài đặt Ollama và models

```bash
# Cài đặt Ollama (https://ollama.ai)
curl https://ollama.ai/install.sh | sh

# Pull models
ollama pull gpt-oss:120b-cloud
ollama pull nomic-embed-text

# Kiểm tra Ollama đang chạy
curl http://localhost:11434/api/tags
```

### Bước 3: Setup databases

#### SQL Server

```bash
# Chạy script tạo schema
sqlcmd -S localhost -U sa -P YourPassword123 -i sql/sqlserver-schema.sql
```

#### PostgreSQL với pgvector

```bash
# Cài đặt pgvector
# Ubuntu/Debian
sudo apt install postgresql-15-pgvector

# hoặc build from source
git clone https://github.com/pgvector/pgvector.git
cd pgvector
make
sudo make install

# Tạo database và chạy schema
psql -U postgres -c "CREATE DATABASE fms_monitor_vector;"
psql -U postgres -d fms_monitor_vector -f sql/postgresql-schema.sql
```

### Bước 4: Cấu hình application

Chỉnh sửa `src/FmsMonitor.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=YOUR_SERVER;Database=FmsMonitor;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True",
    "PostgreSQL": "Host=YOUR_HOST;Port=5432;Database=fms_monitor_vector;Username=YOUR_USER;Password=YOUR_PASSWORD"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "FromEmail": "your-email@gmail.com",
    "Username": "your-email@gmail.com",
    "Password": "your-app-password"
  }
}
```

### Bước 5: Build và chạy

```bash
cd src/FmsMonitor.Api
dotnet restore
dotnet build
dotnet run
```

API sẽ chạy tại: `https://localhost:5001` (hoặc port được cấu hình)

Swagger UI: `https://localhost:5001/swagger`

## ⚙️ Cấu hình

### Monitoring Configuration

File: `appsettings.json`

```json
{
  "Monitoring": {
    "IntervalSeconds": 60,              // Kiểm tra mỗi 60 giây
    "EnableBackgroundService": true     // Bật/tắt background service
  }
}
```

### PostgreSQL Sync

```json
{
  "PostgreSQL": {
    "EnableSync": true,                 // Bật/tắt đồng bộ
    "SyncIntervalMinutes": 60           // Đồng bộ mỗi 60 phút
  }
}
```

### Email Configuration

Để sử dụng Gmail SMTP, bạn cần:
1. Bật 2-Factor Authentication
2. Tạo App Password tại: https://myaccount.google.com/apppasswords
3. Sử dụng App Password trong config

## 📘 Sử dụng

### 1. Tạo cấu hình giám sát cho máy

```bash
curl -X POST https://localhost:5001/api/monitoring/configurations \
  -H "Content-Type: application/json" \
  -d '{
    "machineCode": "MC01",
    "machineName": "Máy CNC 01",
    "minAmperage": 100,
    "maxAmperage": 130,
    "fluctuationMinAmperage": 115,
    "fluctuationMaxAmperage": 125,
    "monitoringWindowMinutes": 5,
    "allowedFluctuationCount": 1,
    "dataFetchIntervalSeconds": 60,
    "isActive": true
  }'
```

### 2. Thêm người nhận email

```bash
curl -X POST https://localhost:5001/api/emailrecipients \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Trần Văn A",
    "email": "tranvana@gmail.com",
    "role": "Nhân viên trực tiếp",
    "minAlertLevel": 1,
    "isActive": true
  }'
```

### 3. Thêm dữ liệu Ampe (cho testing)

```bash
curl -X POST https://localhost:5001/api/amperage \
  -H "Content-Type: application/json" \
  -d '{
    "machineCode": "MC01",
    "machineName": "Máy CNC 01",
    "amperageValue": 135
  }'
```

### 4. Kích hoạt giám sát thủ công

```bash
curl -X POST https://localhost:5001/api/monitoring/run
```

## 🔌 API Endpoints

### Monitoring

- `GET /api/monitoring/configurations` - Lấy tất cả cấu hình
- `GET /api/monitoring/configurations/{machineCode}` - Lấy cấu hình theo máy
- `POST /api/monitoring/configurations` - Tạo cấu hình mới
- `PUT /api/monitoring/configurations/{id}` - Cập nhật cấu hình
- `DELETE /api/monitoring/configurations/{id}` - Xóa cấu hình
- `GET /api/monitoring/analyze/{machineCode}` - Phân tích dao động
- `POST /api/monitoring/run` - Chạy giám sát thủ công

### Alerts

- `GET /api/alerts/unresolved` - Lấy cảnh báo chưa giải quyết
- `GET /api/alerts/machine/{machineCode}` - Lấy cảnh báo theo máy
- `GET /api/alerts/{id}` - Chi tiết cảnh báo
- `PUT /api/alerts/{id}/resolve` - Đánh dấu đã giải quyết

### Amperage

- `GET /api/amperage/machine/{machineCode}` - Lấy dữ liệu theo thời gian
- `GET /api/amperage/machine/{machineCode}/recent` - Dữ liệu gần đây
- `GET /api/amperage/machine/{machineCode}/latest` - Dữ liệu mới nhất
- `POST /api/amperage` - Thêm dữ liệu mới

### Email Recipients

- `GET /api/emailrecipients` - Danh sách người nhận
- `POST /api/emailrecipients` - Thêm người nhận
- `PUT /api/emailrecipients/{id}` - Cập nhật người nhận

### Health Check

- `GET /health` - Kiểm tra trạng thái API

## 🚢 Triển khai

### Docker (Recommended)

Tạo `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/FmsMonitor.Api/FmsMonitor.Api.csproj", "src/FmsMonitor.Api/"]
RUN dotnet restore "src/FmsMonitor.Api/FmsMonitor.Api.csproj"
COPY . .
WORKDIR "/src/src/FmsMonitor.Api"
RUN dotnet build "FmsMonitor.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FmsMonitor.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FmsMonitor.Api.dll"]
```

Build và chạy:

```bash
docker build -t fms-monitor .
docker run -d -p 5001:80 --name fms-monitor fms-monitor
```

### Windows Service

```bash
dotnet publish -c Release -r win-x64 --self-contained
sc create FmsMonitor binPath="C:\path\to\FmsMonitor.Api.exe"
sc start FmsMonitor
```

### Linux Systemd

Tạo file `/etc/systemd/system/fms-monitor.service`:

```ini
[Unit]
Description=FMS Monitor API
After=network.target

[Service]
Type=notify
WorkingDirectory=/opt/fms-monitor
ExecStart=/usr/bin/dotnet /opt/fms-monitor/FmsMonitor.Api.dll
Restart=always
RestartSec=10
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

Kích hoạt:

```bash
sudo systemctl enable fms-monitor
sudo systemctl start fms-monitor
```

## 📊 Ví dụ Cảnh báo

### Email cảnh báo mẫu

```
[CẢNH BÁO] FMS Monitor - Máy CNC 01 (MC01)

Máy: Máy CNC 01 (MC01)
Loại cảnh báo: Dao động bất thường
Giá trị hiện tại: 135A
Thời gian phát hiện: 2024-11-04 10:30:00

Thông báo:
Phát hiện 3 dao động vượt ngưỡng trong 5 phút (cho phép tối đa 1 dao động).
Khoảng dao động bình thường: 115A - 125A

🤖 Phân tích AI:
Máy CNC 01 đang có dấu hiệu quá tải. Dòng điện dao động mạnh và liên tục vượt
ngưỡng cho phép. Khả năng cao do:
- Mòn dao cắt cần thay thế
- Vật liệu gia công quá cứng
- Tốc độ cắt không phù hợp

📋 Khuyến nghị xử lý:
- Kiểm tra và thay dao cắt ngay lập tức
- Giảm tốc độ cắt xuống 80%
- Kiểm tra độ cứng vật liệu gia công
- Ghi nhận và theo dõi thêm
```

## 🔧 Troubleshooting

### Không kết nối được Ollama

```bash
# Kiểm tra Ollama service
systemctl status ollama

# Restart Ollama
systemctl restart ollama

# Test connection
curl http://localhost:11434/api/tags
```

### Database connection failed

```bash
# Test SQL Server
sqlcmd -S localhost -U sa -P password -Q "SELECT @@VERSION"

# Test PostgreSQL
psql -U postgres -c "SELECT version();"
```

### Email không gửi được

- Kiểm tra SMTP credentials
- Đảm bảo đã bật App Password cho Gmail
- Kiểm tra firewall port 587

## 📄 License

MIT License - xem file [LICENSE](LICENSE)

## 👥 Đóng góp

Mọi đóng góp đều được chào đón! Vui lòng tạo Pull Request hoặc Issue.

## 📞 Liên hệ

- Email: support@fmsmonitor.com
- Website: https://fmsmonitor.com

---

**Made with ❤️ for Manufacturing Industry**
