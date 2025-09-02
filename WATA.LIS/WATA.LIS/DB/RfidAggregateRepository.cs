using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace WATA.LIS.DB
{
    public sealed class RfidAggregateRepository
    {
        private readonly string _cs;
        public RfidAggregateRepository(string connectionString) => _cs = connectionString;

        public async Task EnsureTableAsync(CancellationToken ct = default)
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS rfid_read_agg (
  time timestamptz NOT NULL,
  session_id text NOT NULL,
  source text,          -- e.g., Keonn2ch/Keonn4ch
  antenna integer,      -- port/channel if available
  header integer,       -- mux/header if available
  rssi integer,
  read_count integer,
  epc text,
  PRIMARY KEY (time, session_id, epc)
);
CREATE INDEX IF NOT EXISTS idx_rfid_time ON rfid_read_agg(time DESC);
CREATE INDEX IF NOT EXISTS idx_rfid_epc ON rfid_read_agg(epc);";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task Insert2chAsync(
            string sessionId,
            string epc,
            int rssi,
            int readCount,
            CancellationToken ct = default)
        {
            const string sql = @"INSERT INTO rfid_read_agg(
  time, session_id, source, antenna, header, rssi, read_count, epc)
VALUES (now(), $1, 'Keonn2ch', NULL, NULL, $2, $3, $4);";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(sessionId);
            cmd.Parameters.AddWithValue(rssi);
            cmd.Parameters.AddWithValue(readCount);
            cmd.Parameters.AddWithValue(epc);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task Insert4chAsync(
            string sessionId,
            string epc,
            int port,
            int mux1,
            int mux2,
            int rssi,
            int readCount,
            CancellationToken ct = default)
        {
            const string sql = @"INSERT INTO rfid_read_agg(
  time, session_id, source, antenna, header, rssi, read_count, epc)
VALUES (now(), $1, 'Keonn4ch', $2, $3, $4, $5, $6);";
            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(sessionId);
            cmd.Parameters.AddWithValue(port);
            // header: mux1*10 + mux2 (simple packing)
            cmd.Parameters.AddWithValue(mux1 * 10 + mux2);
            cmd.Parameters.AddWithValue(rssi);
            cmd.Parameters.AddWithValue(readCount);
            cmd.Parameters.AddWithValue(epc);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
