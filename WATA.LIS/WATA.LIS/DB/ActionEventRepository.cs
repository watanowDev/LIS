#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace WATA.LIS.DB
{
    public sealed class ActionEventRepository
    {
        private readonly string _cs;
        public ActionEventRepository(string connectionString) => _cs = connectionString;

        public async Task EnsureTableAsync(CancellationToken ct = default)
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS action_event (
  time timestamptz NOT NULL,
  session_id text NOT NULL,
  action text,
  work_location_id text,
  load_id text,
  epc text,
  cepc text,
  request_url text,
  request_body jsonb,
  source text,
  PRIMARY KEY (time, session_id)
);

-- 기본 시간 인덱스(정렬/범위)
CREATE INDEX IF NOT EXISTS idx_action_event_time ON action_event(time DESC);

-- 표현식 인덱스(일/월 집계 최적화) - 테이블 스키마 변경 없이 동작
CREATE INDEX IF NOT EXISTS idx_action_event_day_expr ON action_event ((time::date));
CREATE INDEX IF NOT EXISTS idx_action_event_month_expr ON action_event (((date_trunc('month', time))::date));

-- 대용량 대비 BRIN(시간축 순차적 삽입에 유리)
CREATE INDEX IF NOT EXISTS idx_action_event_time_brin ON action_event USING BRIN (time);
";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task InsertAsync(
            System.DateTimeOffset time,
            string sessionId,
            string action,
            string workLocationId,
            string loadId,
            string epc,
            string cepc,
            string requestUrl,
            string requestBodyJson,
            string source = "backend",
            CancellationToken ct = default)
        {
            const string sql = @"INSERT INTO action_event (
  time, session_id, action, work_location_id, load_id, epc, cepc, request_url, request_body, source)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9::jsonb, $10)
ON CONFLICT (time, session_id) DO NOTHING;";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(time);
            cmd.Parameters.AddWithValue(sessionId);
            cmd.Parameters.AddWithValue((object?)action ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)workLocationId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)loadId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)epc ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)cepc ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)requestUrl ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)requestBodyJson ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)source ?? System.DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
