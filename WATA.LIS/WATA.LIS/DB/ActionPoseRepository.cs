#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace WATA.LIS.DB
{
    public sealed class ActionPoseRepository
    {
        private readonly string _cs;
        public ActionPoseRepository(string connectionString) => _cs = connectionString;

        public async Task EnsureTableAsync(CancellationToken ct = default)
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS action_pose (
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
  vehicle_id text,
  action text,
  PRIMARY KEY (time, session_id)
);
CREATE INDEX IF NOT EXISTS idx_action_pose_time ON action_pose(time DESC);";
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
            string vehicleId,
            string action,
            CancellationToken ct = default)
        {
                        const string sql = @"INSERT INTO action_pose (
    time, session_id, navi_x, navi_y, navi_t, zone_id, zone_name, project_id, mapping_id, map_id, vehicle_id, action)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)
ON CONFLICT (time, session_id) DO NOTHING;";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue(time);
                        cmd.Parameters.AddWithValue(sessionId);
                        cmd.Parameters.AddWithValue(x);
                        cmd.Parameters.AddWithValue(y);
                        cmd.Parameters.AddWithValue(t);
                        cmd.Parameters.AddWithValue((object?)zoneId ?? System.DBNull.Value);
                        cmd.Parameters.AddWithValue((object?)zoneName ?? System.DBNull.Value);
                        cmd.Parameters.AddWithValue((object?)projectId ?? System.DBNull.Value);
                        cmd.Parameters.AddWithValue((object?)mappingId ?? System.DBNull.Value);
                        cmd.Parameters.AddWithValue((object?)mapId ?? System.DBNull.Value);
                        cmd.Parameters.AddWithValue((object?)vehicleId ?? System.DBNull.Value);
                        cmd.Parameters.AddWithValue((object?)action ?? System.DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
