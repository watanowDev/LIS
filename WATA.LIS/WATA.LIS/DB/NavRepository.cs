using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace WATA.LIS.DB
{
    public sealed class NavRepository
    {
        private readonly string _cs;
        public NavRepository(string connectionString) => _cs = connectionString;

        public async Task EnsureTableAsync(CancellationToken ct = default)
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS nav_reading (
  time timestamptz NOT NULL,
  session_id text NOT NULL,
  navi_x bigint,
  navi_y bigint,
  navi_t bigint,
  zone_id text,
  zone_name text,
  project_id text,
  mapping_id text,
  map_id text,
  result text,
  vehicle_id text,
  PRIMARY KEY (time, session_id)
);
CREATE INDEX IF NOT EXISTS idx_nav_time ON nav_reading(time DESC);";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task InsertAsync(
            System.DateTimeOffset time,
            string sessionId,
            long x,
            long y,
            long t,
            string zoneId,
            string zoneName,
            string projectId,
            string mappingId,
            string mapId,
            string result,
            string vehicleId,
            CancellationToken ct = default)
        {
                        const string sql = @"INSERT INTO nav_reading(
    time, session_id, navi_x, navi_y, navi_t, zone_id, zone_name, project_id, mapping_id, map_id, result, vehicle_id)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12);";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue(time);
                        cmd.Parameters.AddWithValue(sessionId);
                        cmd.Parameters.AddWithValue(x);
                        cmd.Parameters.AddWithValue(y);
                        cmd.Parameters.AddWithValue(t);
                        cmd.Parameters.AddWithValue(zoneId ?? (object)System.DBNull.Value);
                        cmd.Parameters.AddWithValue(zoneName ?? (object)System.DBNull.Value);
                        cmd.Parameters.AddWithValue(projectId ?? (object)System.DBNull.Value);
                        cmd.Parameters.AddWithValue(mappingId ?? (object)System.DBNull.Value);
                        cmd.Parameters.AddWithValue(mapId ?? (object)System.DBNull.Value);
                        cmd.Parameters.AddWithValue(result ?? (object)System.DBNull.Value);
                        cmd.Parameters.AddWithValue(vehicleId ?? (object)System.DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
