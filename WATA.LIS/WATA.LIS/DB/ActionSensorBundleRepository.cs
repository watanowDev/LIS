#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace WATA.LIS.DB
{
    public sealed class ActionSensorBundleRepository
    {
        private readonly string _cs;
        public ActionSensorBundleRepository(string connectionString) => _cs = connectionString;

        public async Task EnsureTableAsync(CancellationToken ct = default)
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS action_sensor_bundle (
  time timestamptz NOT NULL,
  session_id text NOT NULL,
  gross_weight integer,
  right_weight integer,
  left_weight integer,
  distance_mm integer,
  rfid_count integer,
  rfid_avg_rssi integer,
  PRIMARY KEY (time, session_id)
);
CREATE INDEX IF NOT EXISTS idx_action_sensor_bundle_time ON action_sensor_bundle(time DESC);";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task InsertAsync(
            string sessionId,
            int? gross,
            int? right,
            int? left,
            int? distanceMm,
            int? rfidCount,
            int? rfidAvgRssi,
            CancellationToken ct = default)
        {
            const string sql = @"INSERT INTO action_sensor_bundle (
  time, session_id, gross_weight, right_weight, left_weight, distance_mm, rfid_count, rfid_avg_rssi)
VALUES (now(), $1, $2, $3, $4, $5, $6, $7);";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(sessionId);
            cmd.Parameters.AddWithValue((object?)gross ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)right ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)left ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)distanceMm ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)rfidCount ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue((object?)rfidAvgRssi ?? System.DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
