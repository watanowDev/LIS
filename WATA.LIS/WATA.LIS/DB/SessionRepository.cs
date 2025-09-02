using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace WATA.LIS.DB
{
    public sealed class SessionRepository
    {
        private readonly string _cs;
        public SessionRepository(string connectionString)
        {
            _cs = connectionString;
        }

        public async Task UpsertStartAsync(System.DateTimeOffset startedAt, string sessionId, string appVersion, string machineName, CancellationToken ct = default)
        {
            const string sql = @"
INSERT INTO app_session(session_id, started_at, app_version, machine_name)
VALUES ($1, $2, $3, $4)
ON CONFLICT (session_id) DO UPDATE SET app_version = EXCLUDED.app_version, machine_name = EXCLUDED.machine_name;";

            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(sessionId);
            cmd.Parameters.AddWithValue(startedAt);
            cmd.Parameters.AddWithValue(appVersion ?? string.Empty);
            cmd.Parameters.AddWithValue(machineName ?? string.Empty);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task MarkEndAsync(System.DateTimeOffset endedAt, string sessionId, CancellationToken ct = default)
        {
            const string sql = "UPDATE app_session SET ended_at = $1 WHERE session_id = $2";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(endedAt);
            cmd.Parameters.AddWithValue(sessionId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
