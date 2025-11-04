# FMS Monitor - Hướng dẫn Triển khai

## Mục lục
- [Triển khai với Docker Compose](#triển-khai-với-docker-compose)
- [Triển khai thủ công](#triển-khai-thủ-công)
- [Cấu hình Production](#cấu-hình-production)
- [Monitoring và Logs](#monitoring-và-logs)
- [Backup và Recovery](#backup-và-recovery)

## Triển khai với Docker Compose

### Bước 1: Chuẩn bị môi trường

```bash
# Clone repository
git clone <repository-url>
cd fms-monitor

# Tạo file .env từ template
cp .env.example .env

# Chỉnh sửa .env với thông tin thực tế
nano .env
```

### Bước 2: Khởi động services

```bash
# Build và start tất cả services
docker-compose up -d

# Kiểm tra trạng thái
docker-compose ps

# Xem logs
docker-compose logs -f fms-api
```

### Bước 3: Setup Ollama models

```bash
# Pull LLM model (cần nhiều thời gian và dung lượng)
docker exec -it fms-ollama ollama pull gpt-oss:120b-cloud

# Pull embedding model
docker exec -it fms-ollama ollama pull nomic-embed-text

# Kiểm tra models đã cài
docker exec -it fms-ollama ollama list
```

### Bước 4: Initialize databases

```bash
# SQL Server - schema đã tự động chạy qua volume mount
# Kiểm tra
docker exec -it fms-sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P FmsMonitor123! \
  -Q "SELECT name FROM sys.databases WHERE name = 'FmsMonitor'"

# PostgreSQL - schema đã tự động chạy
# Kiểm tra
docker exec -it fms-postgres psql -U postgres -d fms_monitor_vector \
  -c "SELECT tablename FROM pg_tables WHERE schemaname = 'public';"
```

### Bước 5: Kiểm tra API

```bash
# Health check
curl http://localhost:5001/health

# Swagger UI
open http://localhost:5001/swagger
```

### Bước 6: Thêm dữ liệu ban đầu

Sử dụng Swagger UI hoặc curl để:
1. Tạo monitoring configurations
2. Thêm email recipients
3. Thêm test data

## Triển khai thủ công

### Requirements

- Ubuntu 22.04 LTS (recommended)
- .NET 9.0 Runtime
- SQL Server 2022
- PostgreSQL 15 + pgvector
- Ollama
- Nginx (reverse proxy)

### Bước 1: Install .NET 9.0

```bash
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 9.0

# Add to PATH
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc
```

### Bước 2: Install và setup databases

#### SQL Server

```bash
# Import Microsoft GPG key
wget -qO- https://packages.microsoft.com/keys/microsoft.asc | sudo apt-key add -

# Add repository
sudo add-apt-repository "$(wget -qO- https://packages.microsoft.com/config/ubuntu/22.04/mssql-server-2022.list)"

# Install
sudo apt-get update
sudo apt-get install -y mssql-server

# Setup
sudo /opt/mssql/bin/mssql-conf setup

# Run schema
sqlcmd -S localhost -U sa -P YourPassword -i sql/sqlserver-schema.sql
```

#### PostgreSQL với pgvector

```bash
# Install PostgreSQL
sudo apt install postgresql-15 postgresql-contrib-15

# Install pgvector
sudo apt install postgresql-15-pgvector

# Create database
sudo -u postgres psql -c "CREATE DATABASE fms_monitor_vector;"

# Run schema
sudo -u postgres psql -d fms_monitor_vector -f sql/postgresql-schema.sql
```

### Bước 3: Install Ollama

```bash
curl https://ollama.ai/install.sh | sh

# Start service
sudo systemctl start ollama
sudo systemctl enable ollama

# Pull models
ollama pull gpt-oss:120b-cloud
ollama pull nomic-embed-text
```

### Bước 4: Deploy application

```bash
# Build application
cd src/FmsMonitor.Api
dotnet publish -c Release -o /opt/fms-monitor

# Copy configuration
cp appsettings.json /opt/fms-monitor/
nano /opt/fms-monitor/appsettings.json  # Edit with production values

# Create systemd service
sudo nano /etc/systemd/system/fms-monitor.service
```

Service file content:

```ini
[Unit]
Description=FMS Monitor API
After=network.target mssql-server.service postgresql.service

[Service]
Type=notify
WorkingDirectory=/opt/fms-monitor
ExecStart=/usr/bin/dotnet /opt/fms-monitor/FmsMonitor.Api.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

```bash
# Enable and start
sudo systemctl daemon-reload
sudo systemctl enable fms-monitor
sudo systemctl start fms-monitor

# Check status
sudo systemctl status fms-monitor
```

### Bước 5: Setup Nginx reverse proxy

```bash
sudo apt install nginx

sudo nano /etc/nginx/sites-available/fms-monitor
```

Nginx config:

```nginx
server {
    listen 80;
    server_name fmsmonitor.yourdomain.com;

    location / {
        proxy_pass http://localhost:5001;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

```bash
# Enable site
sudo ln -s /etc/nginx/sites-available/fms-monitor /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx
```

### Bước 6: Setup SSL với Let's Encrypt

```bash
sudo apt install certbot python3-certbot-nginx

sudo certbot --nginx -d fmsmonitor.yourdomain.com

# Auto-renewal test
sudo certbot renew --dry-run
```

## Cấu hình Production

### Security Best Practices

1. **Database Passwords**: Sử dụng strong passwords
2. **Connection Strings**: Store trong environment variables hoặc Azure Key Vault
3. **HTTPS Only**: Bật HTTPS và HSTS
4. **CORS**: Giới hạn origins cụ thể
5. **Rate Limiting**: Implement để chống DoS

### Performance Tuning

#### SQL Server

```sql
-- Enable Query Store
ALTER DATABASE FmsMonitor SET QUERY_STORE = ON;

-- Update statistics
EXEC sp_updatestats;

-- Rebuild indexes
ALTER INDEX ALL ON MachineAmperage REBUILD;
```

#### PostgreSQL

```sql
-- Vacuum và analyze
VACUUM ANALYZE;

-- Reindex
REINDEX DATABASE fms_monitor_vector;

-- Update vector index
REINDEX INDEX idx_machine_amperage_vector_embedding;
```

### Monitoring Configuration

```json
{
  "Monitoring": {
    "IntervalSeconds": 30,  // Giảm xuống cho production
    "EnableBackgroundService": true
  },
  "PostgreSQL": {
    "EnableSync": true,
    "SyncIntervalMinutes": 30  // Sync thường xuyên hơn
  }
}
```

## Monitoring và Logs

### Xem logs

#### Docker

```bash
# API logs
docker-compose logs -f fms-api

# SQL Server logs
docker-compose logs -f sqlserver

# Tất cả logs
docker-compose logs -f
```

#### Systemd

```bash
# Realtime logs
sudo journalctl -u fms-monitor -f

# Recent logs
sudo journalctl -u fms-monitor -n 100

# Logs by date
sudo journalctl -u fms-monitor --since "2024-11-04"
```

### Application logs

Logs được lưu tại `/app/logs` (Docker) hoặc `/opt/fms-monitor/logs` (manual):

```bash
# Latest log file
tail -f logs/fms-monitor-*.txt

# Search for errors
grep "Error" logs/fms-monitor-*.txt
```

### Health Monitoring

#### Setup monitoring script

```bash
#!/bin/bash
# /usr/local/bin/check-fms-health.sh

HEALTH_URL="http://localhost:5001/health"
ALERT_EMAIL="admin@company.com"

response=$(curl -s -o /dev/null -w "%{http_code}" $HEALTH_URL)

if [ $response -ne 200 ]; then
    echo "FMS Monitor health check failed! Status: $response" | \
    mail -s "FMS Monitor Alert" $ALERT_EMAIL

    # Restart service
    sudo systemctl restart fms-monitor
fi
```

#### Setup cron job

```bash
# Run every 5 minutes
*/5 * * * * /usr/local/bin/check-fms-health.sh
```

## Backup và Recovery

### Database Backup

#### SQL Server

```bash
#!/bin/bash
# /usr/local/bin/backup-sqlserver.sh

BACKUP_DIR="/backup/sqlserver"
DATE=$(date +%Y%m%d_%H%M%S)

docker exec fms-sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P FmsMonitor123! \
  -Q "BACKUP DATABASE FmsMonitor TO DISK = '/var/opt/mssql/backup/FmsMonitor_$DATE.bak' WITH INIT"

# Copy backup out of container
docker cp fms-sqlserver:/var/opt/mssql/backup/FmsMonitor_$DATE.bak $BACKUP_DIR/

# Keep only last 7 days
find $BACKUP_DIR -name "*.bak" -mtime +7 -delete
```

#### PostgreSQL

```bash
#!/bin/bash
# /usr/local/bin/backup-postgres.sh

BACKUP_DIR="/backup/postgres"
DATE=$(date +%Y%m%d_%H%M%S)

docker exec fms-postgres pg_dump -U postgres fms_monitor_vector | \
  gzip > $BACKUP_DIR/fms_monitor_vector_$DATE.sql.gz

# Keep only last 7 days
find $BACKUP_DIR -name "*.sql.gz" -mtime +7 -delete
```

#### Setup automated backups

```bash
# Daily backup at 2 AM
0 2 * * * /usr/local/bin/backup-sqlserver.sh
0 2 * * * /usr/local/bin/backup-postgres.sh
```

### Recovery

#### SQL Server

```bash
# Stop application
docker-compose stop fms-api

# Restore database
docker exec fms-sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P FmsMonitor123! \
  -Q "RESTORE DATABASE FmsMonitor FROM DISK = '/var/opt/mssql/backup/FmsMonitor_YYYYMMDD.bak' WITH REPLACE"

# Start application
docker-compose start fms-api
```

#### PostgreSQL

```bash
# Stop application
docker-compose stop fms-api

# Restore database
gunzip -c /backup/postgres/fms_monitor_vector_YYYYMMDD.sql.gz | \
  docker exec -i fms-postgres psql -U postgres fms_monitor_vector

# Start application
docker-compose start fms-api
```

## Scaling Considerations

### Horizontal Scaling

Để scale horizontally:

1. Deploy multiple API instances
2. Setup load balancer (Nginx/HAProxy)
3. Share database connections
4. Implement distributed caching (Redis)

### Database Optimization

1. **Partitioning**: Partition MachineAmperage table by date
2. **Read Replicas**: Setup read replicas cho reporting
3. **Caching**: Implement caching layer
4. **Archive**: Archive old data > 6 months

## Troubleshooting

### API không start

```bash
# Check logs
docker-compose logs fms-api
sudo journalctl -u fms-monitor -n 50

# Common issues:
# - Database connection failed
# - Ollama not accessible
# - Port already in use
```

### High CPU usage

```bash
# Check processes
docker stats

# Check Ollama usage (AI models consume lots of resources)
docker stats fms-ollama

# Solution: Limit resources in docker-compose.yml
```

### Database connection timeout

```bash
# Increase timeout in appsettings.json
"ConnectionStrings": {
  "SqlServer": "...;Connection Timeout=60;"
}

# Check database status
docker-compose exec sqlserver systemctl status mssql-server
```

---

Để được hỗ trợ thêm, vui lòng tạo issue tại repository hoặc liên hệ support team.
