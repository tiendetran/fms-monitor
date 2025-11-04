-- FMS Monitor - PostgreSQL với pgvector Database Schema

-- Tạo database
-- CREATE DATABASE fms_monitor_vector;

-- Kết nối đến database
-- \c fms_monitor_vector;

-- Cài đặt pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

-- Bảng machine_amperage_vector: Lưu dữ liệu Ampe với embedding vector
DROP TABLE IF EXISTS machine_amperage_vector;

CREATE TABLE machine_amperage_vector (
    id SERIAL PRIMARY KEY,
    machine_code VARCHAR(50) NOT NULL,
    machine_name VARCHAR(200) NOT NULL,
    amperage_value DECIMAL(10, 2) NOT NULL,
    recorded_at TIMESTAMP NOT NULL,
    status VARCHAR(50),
    notes TEXT,
    embedding vector(768), -- nomic-embed-text sử dụng 768 dimensions
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tạo index cho search hiệu quả
CREATE INDEX idx_machine_amperage_vector_machine_code ON machine_amperage_vector(machine_code);
CREATE INDEX idx_machine_amperage_vector_recorded_at ON machine_amperage_vector(recorded_at DESC);
CREATE INDEX idx_machine_amperage_vector_embedding ON machine_amperage_vector USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);

-- Bảng anomaly_patterns: Lưu các pattern bất thường đã học
DROP TABLE IF EXISTS anomaly_patterns;

CREATE TABLE anomaly_patterns (
    id SERIAL PRIMARY KEY,
    machine_code VARCHAR(50) NOT NULL,
    pattern_name VARCHAR(200) NOT NULL,
    pattern_description TEXT,
    pattern_data JSONB NOT NULL, -- Lưu dữ liệu pattern dạng JSON
    embedding vector(768),
    severity_level INT NOT NULL, -- 0=Info, 1=Warning, 2=Critical, 3=Emergency
    occurrence_count INT DEFAULT 1,
    first_detected TIMESTAMP NOT NULL,
    last_detected TIMESTAMP NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_anomaly_patterns_machine_code ON anomaly_patterns(machine_code);
CREATE INDEX idx_anomaly_patterns_embedding ON anomaly_patterns USING ivfflat (embedding vector_cosine_ops) WITH (lists = 50);
CREATE INDEX idx_anomaly_patterns_is_active ON anomaly_patterns(is_active);

-- Bảng ai_analysis_history: Lưu lịch sử phân tích AI
DROP TABLE IF EXISTS ai_analysis_history;

CREATE TABLE ai_analysis_history (
    id SERIAL PRIMARY KEY,
    machine_code VARCHAR(50) NOT NULL,
    analysis_text TEXT NOT NULL,
    suggested_alert_level INT NOT NULL,
    recommendations JSONB,
    predicted_issue TEXT,
    context_data JSONB, -- Lưu context data đã sử dụng
    embedding vector(768),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_ai_analysis_history_machine_code ON ai_analysis_history(machine_code);
CREATE INDEX idx_ai_analysis_history_created_at ON ai_analysis_history(created_at DESC);
CREATE INDEX idx_ai_analysis_history_embedding ON ai_analysis_history USING ivfflat (embedding vector_cosine_ops) WITH (lists = 50);

-- Function: Tìm kiếm similar patterns
CREATE OR REPLACE FUNCTION find_similar_patterns(
    query_embedding vector(768),
    similarity_threshold FLOAT DEFAULT 0.8,
    max_results INT DEFAULT 10
)
RETURNS TABLE (
    machine_code VARCHAR,
    pattern_name VARCHAR,
    similarity FLOAT,
    severity_level INT
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        ap.machine_code,
        ap.pattern_name,
        1 - (ap.embedding <-> query_embedding) AS similarity,
        ap.severity_level
    FROM anomaly_patterns ap
    WHERE ap.is_active = TRUE
      AND (1 - (ap.embedding <-> query_embedding)) >= similarity_threshold
    ORDER BY ap.embedding <-> query_embedding
    LIMIT max_results;
END;
$$ LANGUAGE plpgsql;

-- Function: Lấy historical analysis tương tự
CREATE OR REPLACE FUNCTION get_similar_analysis(
    query_embedding vector(768),
    target_machine_code VARCHAR DEFAULT NULL,
    max_results INT DEFAULT 5
)
RETURNS TABLE (
    id INT,
    machine_code VARCHAR,
    analysis_text TEXT,
    recommendations JSONB,
    similarity FLOAT
) AS $$
BEGIN
    IF target_machine_code IS NULL THEN
        RETURN QUERY
        SELECT
            ah.id,
            ah.machine_code,
            ah.analysis_text,
            ah.recommendations,
            1 - (ah.embedding <-> query_embedding) AS similarity
        FROM ai_analysis_history ah
        ORDER BY ah.embedding <-> query_embedding
        LIMIT max_results;
    ELSE
        RETURN QUERY
        SELECT
            ah.id,
            ah.machine_code,
            ah.analysis_text,
            ah.recommendations,
            1 - (ah.embedding <-> query_embedding) AS similarity
        FROM ai_analysis_history ah
        WHERE ah.machine_code = target_machine_code
        ORDER BY ah.embedding <-> query_embedding
        LIMIT max_results;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- View: Thống kê patterns theo máy
CREATE OR REPLACE VIEW v_pattern_statistics AS
SELECT
    machine_code,
    COUNT(*) as total_patterns,
    COUNT(CASE WHEN is_active = TRUE THEN 1 END) as active_patterns,
    AVG(severity_level) as avg_severity,
    MAX(last_detected) as last_anomaly_detected
FROM anomaly_patterns
GROUP BY machine_code;

-- Thêm comments
COMMENT ON TABLE machine_amperage_vector IS 'Bảng lưu trữ dữ liệu Ampe với vector embeddings để tìm kiếm tương tự';
COMMENT ON TABLE anomaly_patterns IS 'Bảng lưu các pattern bất thường đã học được từ AI';
COMMENT ON TABLE ai_analysis_history IS 'Lịch sử phân tích của AI Agent';
COMMENT ON FUNCTION find_similar_patterns IS 'Tìm các pattern bất thường tương tự dựa trên embedding';
COMMENT ON FUNCTION get_similar_analysis IS 'Lấy các phân tích AI tương tự từ lịch sử';

-- Grant permissions (điều chỉnh theo user của bạn)
-- GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO fms_user;
-- GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO fms_user;
-- GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO fms_user;

SELECT 'PostgreSQL schema with pgvector setup completed!' as message;
