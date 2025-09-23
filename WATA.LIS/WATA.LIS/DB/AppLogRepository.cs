#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;

namespace WATA.LIS.DB
{
    public sealed class AppLogRepository
    {
        private readonly string _cs;
        private const string Schema = "lis_core";
        public AppLogRepository(string connectionString) => _cs = connectionString;

        public async Task EnsureTableAsync(CancellationToken ct = default)
        {
            string sql = $@"
CREATE SCHEMA IF NOT EXISTS ""{Schema}"";
CREATE TABLE IF NOT EXISTS ""{Schema}"".app_logs (
  log_id        BIGSERIAL PRIMARY KEY,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
  session_id    text NULL,
  category      VARCHAR(32)  NOT NULL,
  level         VARCHAR(16)  NULL,
  message       TEXT         NOT NULL,
  source        VARCHAR(128) NULL,
  line_number   integer      NULL,
  thread_id     integer      NULL,
  machine_name  VARCHAR(128) NULL,
  vehicle_id    VARCHAR(64)  NULL,
  work_location_id VARCHAR(64) NULL,
  project_id    VARCHAR(64)  NULL,
  mapping_id    VARCHAR(64)  NULL,
  map_id        VARCHAR(64)  NULL,
  correlation_id VARCHAR(64) NULL,
  tags          TEXT[]       NULL,
  context       JSONB        NULL
);
CREATE INDEX IF NOT EXISTS idx_app_logs_created_at ON ""{Schema}"".app_logs (created_at DESC);
CREATE INDEX IF NOT EXISTS idx_app_logs_category_created_at ON ""{Schema}"".app_logs (category, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_app_logs_session_created_at ON ""{Schema}"".app_logs (session_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_app_logs_context_gin ON ""{Schema}"".app_logs USING GIN (context);
";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task InsertAsync(
            System.DateTimeOffset createdAt,
            string? sessionId,
            string category,
            string message,
            string? source,
            int? lineNumber,
            int? threadId,
            string? machineName,
            string? vehicleId,
            string? workLocationId,
            string? projectId,
            string? mappingId,
            string? mapId,
            string? level = null,
            string? correlationId = null,
            string? contextJson = null,
            string[]? tags = null,
            CancellationToken ct = default)
        {
            string sql = $@"INSERT INTO ""{Schema}"".app_logs (
  created_at, session_id, category, level, message, source, line_number, thread_id, machine_name,
  vehicle_id, work_location_id, project_id, mapping_id, map_id, correlation_id, tags, context)
VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17::jsonb);";

            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(createdAt);
            cmd.Parameters.AddWithValue((object?)sessionId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue(category);
            cmd.Parameters.AddWithValue((object?)level ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue(message);
            cmd.Parameters.AddWithValue((object?)source ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)lineNumber ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)threadId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)machineName ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)vehicleId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)workLocationId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)projectId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)mappingId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)mapId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)correlationId ?? System.DBNull.Value);

            var pTags = new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text };
            pTags.Value = (object?)tags ?? System.DBNull.Value;
            cmd.Parameters.Add(pTags);

            var pContext = new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb };
            pContext.Value = (object?)contextJson ?? System.DBNull.Value;
            cmd.Parameters.Add(pContext);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        public sealed class AppLogRow
        {
            public System.DateTimeOffset CreatedAt { get; set; }
            public string? Category { get; set; }
            public string? Message { get; set; }
            public string? ContextJson { get; set; }
        }

        public async Task<AppLogRow?> GetLastByCategoryAsync(string category, CancellationToken ct = default)
        {
            string sql = $@"SELECT created_at, category, message, context
FROM ""{Schema}"".app_logs
WHERE category = $1
ORDER BY created_at DESC
LIMIT 1;";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(category);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                return new AppLogRow
                {
                    CreatedAt = r.GetFieldValue<System.DateTimeOffset>(0),
                    Category = r.IsDBNull(1) ? null : r.GetString(1),
                    Message  = r.IsDBNull(2) ? null : r.GetString(2),
                    ContextJson = r.IsDBNull(3) ? null : r.GetString(3),
                };
            }
            return null;
        }
    }
}