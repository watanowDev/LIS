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

        public async Task UpsertStartAsync(string sessionId, string appVersion, string machineName, CancellationToken ct = default)
        {
            const string sql = @"
INSERT INTO app_session(session_id, started_at, app_version, machine_name)
VALUES ($1, now(), $2, $3)
ON CONFLICT (session_id) DO UPDATE SET app_version = EXCLUDED.app_version, machine_name = EXCLUDED.machine_name;";

            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(sessionId);
            cmd.Parameters.AddWithValue(appVersion ?? string.Empty);
            cmd.Parameters.AddWithValue(machineName ?? string.Empty);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task MarkEndAsync(string sessionId, CancellationToken ct = default)
        {
            const string sql = "UPDATE app_session SET ended_at = now() WHERE session_id = $1";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(sessionId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
