using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace WATA.LIS.DB
{
    public sealed class DistanceRepository
    {
        private readonly string _cs;
        public DistanceRepository(string connectionString) => _cs = connectionString;

        public async Task EnsureTableAsync(CancellationToken ct = default)
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS distance_reading (
  time timestamptz NOT NULL,
  session_id text NOT NULL,
  distance_mm integer,
  connected boolean,
  PRIMARY KEY (time, session_id)
);
CREATE INDEX IF NOT EXISTS idx_distance_time ON distance_reading(time DESC);";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }

                public async Task InsertAsync(
                        System.DateTimeOffset time,
                        string sessionId,
                        int distanceMm,
                        bool connected,
                        CancellationToken ct = default)
        {
                        const string sql = @"INSERT INTO distance_reading(
    time, session_id, distance_mm, connected)
VALUES ($1, $2, $3, $4);";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue(time);
                        cmd.Parameters.AddWithValue(sessionId);
                        cmd.Parameters.AddWithValue(distanceMm);
                        cmd.Parameters.AddWithValue(connected);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
