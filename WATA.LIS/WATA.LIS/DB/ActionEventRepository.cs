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

-- 날짜/월 시작일: 생성 컬럼(12+)
ALTER TABLE action_event
  ADD COLUMN IF NOT EXISTS date_day date GENERATED ALWAYS AS (time::date) STORED;

ALTER TABLE action_event
  ADD COLUMN IF NOT EXISTS month_start date GENERATED ALWAYS AS ((date_trunc('month', time))::date) STORED;

-- 조회 패턴에 맞는 인덱스
CREATE INDEX IF NOT EXISTS idx_action_event_date_day ON action_event(date_day);
CREATE INDEX IF NOT EXISTS idx_action_event_month_start ON action_event(month_start);

-- 대용량 대비 BRIN(시간축 순차적 삽입에 유리, btree와 병행 가능)
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_indexes 
    WHERE schemaname = current_schema() 
      AND indexname = 'idx_action_event_time_brin'
  ) THEN
    EXECUTE 'CREATE INDEX idx_action_event_time_brin ON action_event USING BRIN (time)';
  END IF;
END$$;";
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
