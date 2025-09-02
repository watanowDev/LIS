using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace WATA.LIS.DB
{
    public sealed class MigrationService
    {
        private readonly string _cs;
        public MigrationService(string connectionString)
        {
            _cs = connectionString;
        }

        public async Task EnsureSchemaAsync(CancellationToken ct = default)
        {
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);

            await using (var cmd = new NpgsqlCommand("SET lock_timeout = '5s'; SET statement_timeout = '30s';", conn))
                await cmd.ExecuteNonQueryAsync(ct);

            var sql = @"
CREATE TABLE IF NOT EXISTS schema_migrations (
  id serial PRIMARY KEY,
  version text NOT NULL,
  applied_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS app_session (
  session_id text PRIMARY KEY,
  started_at timestamptz NOT NULL,
  ended_at timestamptz,
  app_version text,
  machine_name text
);

CREATE INDEX IF NOT EXISTS idx_app_session_started_at ON app_session(started_at DESC);
";

            await using var tx = await conn.BeginTransactionAsync(ct);
            await using (var cmd = new NpgsqlCommand(sql, conn, tx))
                await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
        }
    }
}
