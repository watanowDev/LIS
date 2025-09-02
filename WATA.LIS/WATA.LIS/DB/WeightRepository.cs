using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace WATA.LIS.DB
{
    public sealed class WeightRepository
    {
        private readonly string _cs;
        public WeightRepository(string connectionString) => _cs = connectionString;

        public async Task EnsureTableAsync(CancellationToken ct = default)
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS weight_reading (
  time timestamptz NOT NULL,
  session_id text NOT NULL,
  gross_weight integer,
  right_weight integer,
  left_weight integer,
  right_battery integer,
  left_battery integer,
  right_is_charging boolean,
  left_is_charging boolean,
  right_online boolean,
  left_online boolean,
  gross_net boolean,
  over_load boolean,
  out_of_tolerance boolean,
  PRIMARY KEY (time, session_id)
);
CREATE INDEX IF NOT EXISTS idx_weight_time ON weight_reading(time DESC);";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task InsertAsync(
            string sessionId,
            int grossWeight,
            int rightWeight,
            int leftWeight,
            int rightBattery,
            int leftBattery,
            bool rightIsCharging,
            bool leftIsCharging,
            bool rightOnline,
            bool leftOnline,
            bool grossNet,
            bool overLoad,
            bool outOfTolerance,
            CancellationToken ct = default)
        {
            const string sql = @"INSERT INTO weight_reading(
  time, session_id, gross_weight, right_weight, left_weight, right_battery, left_battery,
  right_is_charging, left_is_charging, right_online, left_online, gross_net, over_load, out_of_tolerance)
VALUES (now(), $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13);";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(sessionId);
            cmd.Parameters.AddWithValue(grossWeight);
            cmd.Parameters.AddWithValue(rightWeight);
            cmd.Parameters.AddWithValue(leftWeight);
            cmd.Parameters.AddWithValue(rightBattery);
            cmd.Parameters.AddWithValue(leftBattery);
            cmd.Parameters.AddWithValue(rightIsCharging);
            cmd.Parameters.AddWithValue(leftIsCharging);
            cmd.Parameters.AddWithValue(rightOnline);
            cmd.Parameters.AddWithValue(leftOnline);
            cmd.Parameters.AddWithValue(grossNet);
            cmd.Parameters.AddWithValue(overLoad);
            cmd.Parameters.AddWithValue(outOfTolerance);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
