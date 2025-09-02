using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace WATA.LIS.DB
{
    public sealed class SystemStatusRepository
    {
        private readonly string _cs;
        public SystemStatusRepository(string connectionString) => _cs = connectionString;

        public async Task EnsureTableAsync(CancellationToken ct = default)
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS system_status_tick (
  time timestamptz NOT NULL,
  session_id text NOT NULL,
  backend_ok boolean,
  network_ok boolean,
  set_all_ready boolean,
  error_code text,
  message text,
  PRIMARY KEY (time, session_id)
);
CREATE INDEX IF NOT EXISTS idx_system_status_time ON system_status_tick(time DESC);";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task InsertAsync(bool backendOk, bool networkOk, bool setAllReady, string errorCode, string message, string sessionId, CancellationToken ct = default)
        {
            const string sql = @"INSERT INTO system_status_tick(time, session_id, backend_ok, network_ok, set_all_ready, error_code, message)
VALUES (now(), $1, $2, $3, $4, $5, $6);";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(sessionId);
            cmd.Parameters.AddWithValue(backendOk);
            cmd.Parameters.AddWithValue(networkOk);
            cmd.Parameters.AddWithValue(setAllReady);
            cmd.Parameters.AddWithValue((object?)errorCode ?? (object)System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)message ?? (object)System.DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
