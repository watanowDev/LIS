#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace WATA.LIS.DB
{
    /// <summary>
    /// DB ��ġ INSERT ���� (5�ʸ��� �Ǵ� 100�� ���̸� �ڵ� �÷���)
    /// CPU/I/O ���ҽ� ����ȭ�� ���� ť ��� ��ġ ó��
    /// </summary>
    public sealed class BatchInsertService : IDisposable
    {
        private readonly string _connectionString;
        private readonly ConcurrentQueue<PendingInsert> _queue = new();
    private readonly Timer _flushTimer;
  private readonly SemaphoreSlim _flushLock = new(1, 1);
        private bool _disposed;

        // ���� ������ �Ӱ谪
        private const int MaxBatchSize = 100;        // 100�� ���̸� ��� ó��
        private const int FlushIntervalMs = 5000;    // 5�ʸ��� �ڵ� �÷���

        public BatchInsertService(string connectionString)
   {
   _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            
         // 5�ʸ��� �ڵ� �÷��� Ÿ�̸� ����
     _flushTimer = new Timer(async _ => await FlushBatchAsync(), null, FlushIntervalMs, FlushIntervalMs);
    }

        #region Enqueue Methods

      /// <summary>
        /// weight_reading ���̺� INSERT ��û ť�� �߰�
  /// </summary>
        public void EnqueueWeightReading(
            DateTimeOffset time, string sessionId,
            int grossWeight, int rightWeight, int leftWeight,
          int rightBattery, int leftBattery,
   bool rightIsCharging, bool leftIsCharging,
  bool rightOnline, bool leftOnline,
            bool grossNet, bool overLoad, bool outOfTolerance)
  {
        if (_disposed) return;

     _queue.Enqueue(new PendingInsert
            {
              Type = InsertType.WeightReading,
 Time = time,
SessionId = sessionId,
    GrossWeight = grossWeight,
       RightWeight = rightWeight,
     LeftWeight = leftWeight,
         RightBattery = rightBattery,
                LeftBattery = leftBattery,
       RightIsCharging = rightIsCharging,
                LeftIsCharging = leftIsCharging,
          RightOnline = rightOnline,
             LeftOnline = leftOnline,
        GrossNet = grossNet,
          OverLoad = overLoad,
       OutOfTolerance = outOfTolerance
            });

    TryFlushIfFull();
    }

/// <summary>
        /// distance_reading ���̺� INSERT ��û ť�� �߰�
        /// </summary>
        public void EnqueueDistanceReading(
            DateTimeOffset time, string sessionId,
        int distanceMm, bool connected)
        {
         if (_disposed) return;

 _queue.Enqueue(new PendingInsert
       {
      Type = InsertType.DistanceReading,
       Time = time,
            SessionId = sessionId,
        DistanceMm = distanceMm,
  DistanceConnected = connected
});

            TryFlushIfFull();
        }

        /// <summary>
        /// nav_reading ���̺� INSERT ��û ť�� �߰�
        /// </summary>
        public void EnqueueNavReading(
          DateTimeOffset time, string sessionId,
     long x, long y, long t,
       string? zoneId, string? zoneName,
 string? projectId, string? mappingId, string? mapId,
            string? result, string? vehicleId)
        {
    if (_disposed) return;

         _queue.Enqueue(new PendingInsert
 {
    Type = InsertType.NavReading,
       Time = time,
        SessionId = sessionId,
                NavX = x,
         NavY = y,
                NavT = t,
    ZoneId = zoneId,
        ZoneName = zoneName,
      ProjectId = projectId,
     MappingId = mappingId,
            MapId = mapId,
    Result = result,
     VehicleId = vehicleId
    });

     TryFlushIfFull();
        }

        /// <summary>
        /// rfid_read_agg ���̺� INSERT ��û ť�� �߰� (2ch)
        /// </summary>
 public void EnqueueRfid2chReading(
            DateTimeOffset time, string sessionId,
       string epc, int rssi, int readCount)
        {
          if (_disposed) return;

            _queue.Enqueue(new PendingInsert
 {
           Type = InsertType.Rfid2ch,
             Time = time,
      SessionId = sessionId,
      Epc = epc,
 Rssi = rssi,
    ReadCount = readCount
         });

            TryFlushIfFull();
        }

        #endregion

        /// <summary>
/// 100�� ���̸� ��� �񵿱� �÷��� Ʈ����
        /// </summary>
        private void TryFlushIfFull()
   {
            if (_queue.Count >= MaxBatchSize)
       {
          _ = Task.Run(FlushBatchAsync);
 }
        }

    /// <summary>
     /// ť�� ���� INSERT �۾��� ��ġ�� ó��
        /// </summary>
        private async Task FlushBatchAsync()
     {
            if (_disposed || _queue.IsEmpty) return;

            // �ߺ� ���� ����
     if (!await _flushLock.WaitAsync(0))
                return;

   try
            {
                var batch = new List<PendingInsert>();
          
    // �ִ� 1000�������� �� ���� ó��
           while (_queue.TryDequeue(out var item) && batch.Count < 1000)
    {
      batch.Add(item);
          }

    if (batch.Count == 0) return;

                // ? ���� Ʈ��������� ��ġ INSERT
                await using var conn = new NpgsqlConnection(_connectionString);
  await conn.OpenAsync();
 await using var transaction = await conn.BeginTransactionAsync();

     try
 {
        foreach (var item in batch)
             {
    switch (item.Type)
   {
             case InsertType.WeightReading:
   await InsertWeightReadingAsync(conn, item);
   break;
   case InsertType.DistanceReading:
       await InsertDistanceReadingAsync(conn, item);
            break;
      case InsertType.NavReading:
    await InsertNavReadingAsync(conn, item);
       break;
        case InsertType.Rfid2ch:
      await InsertRfid2chAsync(conn, item);
     break;
   }
  }

         await transaction.CommitAsync();
            
          // �α� ���ø� (10�� �̻� ��ġ�� �α�)
       if (batch.Count >= 10)
    {
            WATA.LIS.Core.Common.Tools.Log(
          $"[DB Batch] Flushed {batch.Count} records to DB", 
    WATA.LIS.Core.Common.Tools.ELogType.SystemLog);
         }
     }
     catch (Exception ex)
         {
       await transaction.RollbackAsync();
       WATA.LIS.Core.Common.Tools.Log(
               $"[DB Batch] Rollback error: {ex.Message}", 
         WATA.LIS.Core.Common.Tools.ELogType.SystemLog);
   
         // ������ �׸� ��ť�� (���û��� - �ʿ�� �ּ� ����)
         // foreach (var item in batch) _queue.Enqueue(item);
 }
    }
finally
            {
     _flushLock.Release();
            }
        }

    #region Individual Insert Methods

   private static async Task InsertWeightReadingAsync(NpgsqlConnection conn, PendingInsert item)
        {
      const string sql = @"
INSERT INTO weight_reading(
    time, session_id, gross_weight, right_weight, left_weight, right_battery, left_battery,
    right_is_charging, left_is_charging, right_online, left_online, gross_net, over_load, out_of_tolerance)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)
ON CONFLICT (time, session_id) DO NOTHING;@";

            await using var cmd = new NpgsqlCommand(sql, conn);
         cmd.Parameters.AddWithValue(item.Time);
      cmd.Parameters.AddWithValue(item.SessionId ?? "");
       cmd.Parameters.AddWithValue(item.GrossWeight);
            cmd.Parameters.AddWithValue(item.RightWeight);
       cmd.Parameters.AddWithValue(item.LeftWeight);
            cmd.Parameters.AddWithValue(item.RightBattery);
    cmd.Parameters.AddWithValue(item.LeftBattery);
      cmd.Parameters.AddWithValue(item.RightIsCharging);
          cmd.Parameters.AddWithValue(item.LeftIsCharging);
  cmd.Parameters.AddWithValue(item.RightOnline);
    cmd.Parameters.AddWithValue(item.LeftOnline);
    cmd.Parameters.AddWithValue(item.GrossNet);
   cmd.Parameters.AddWithValue(item.OverLoad);
 cmd.Parameters.AddWithValue(item.OutOfTolerance);

            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task InsertDistanceReadingAsync(NpgsqlConnection conn, PendingInsert item)
        {
            const string sql = @"
INSERT INTO distance_reading(
    time, session_id, distance_mm, connected)
VALUES ($1, $2, $3, $4)
ON CONFLICT (time, session_id) DO NOTHING;@";

         await using var cmd = new NpgsqlCommand(sql, conn);
     cmd.Parameters.AddWithValue(item.Time);
          cmd.Parameters.AddWithValue(item.SessionId ?? "");
            cmd.Parameters.AddWithValue(item.DistanceMm);
            cmd.Parameters.AddWithValue(item.DistanceConnected);

         await cmd.ExecuteNonQueryAsync();
      }

        private static async Task InsertNavReadingAsync(NpgsqlConnection conn, PendingInsert item)
        {
    const string sql = @"
INSERT INTO nav_reading(
    time, session_id, navi_x, navi_y, navi_t, zone_id, zone_name, project_id, mapping_id, map_id, result, vehicle_id)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)
ON CONFLICT (time, session_id) DO NOTHING;@";

          await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(item.Time);
            cmd.Parameters.AddWithValue(item.SessionId ?? "");
            cmd.Parameters.AddWithValue(item.NavX);
  cmd.Parameters.AddWithValue(item.NavY);
  cmd.Parameters.AddWithValue(item.NavT);
       cmd.Parameters.AddWithValue((object?)item.ZoneId ?? DBNull.Value);
     cmd.Parameters.AddWithValue((object?)item.ZoneName ?? DBNull.Value);
       cmd.Parameters.AddWithValue((object?)item.ProjectId ?? DBNull.Value);
         cmd.Parameters.AddWithValue((object?)item.MappingId ?? DBNull.Value);
            cmd.Parameters.AddWithValue((object?)item.MapId ?? DBNull.Value);
            cmd.Parameters.AddWithValue((object?)item.Result ?? DBNull.Value);
   cmd.Parameters.AddWithValue((object?)item.VehicleId ?? DBNull.Value);

   await cmd.ExecuteNonQueryAsync();
}

        private static async Task InsertRfid2chAsync(NpgsqlConnection conn, PendingInsert item)
        {
            const string sql = @"
INSERT INTO rfid_read_agg(
    time, session_id, source, antenna, header, rssi, read_count, epc)
VALUES ($1, $2, 'Keonn2ch', NULL, NULL, $3, $4, $5)
ON CONFLICT (time, session_id, epc) DO NOTHING;@";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(item.Time);
      cmd.Parameters.AddWithValue(item.SessionId ?? "");
    cmd.Parameters.AddWithValue(item.Rssi);
   cmd.Parameters.AddWithValue(item.ReadCount);
            cmd.Parameters.AddWithValue(item.Epc ?? "");

await cmd.ExecuteNonQueryAsync();
        }

      #endregion

  public void Dispose()
        {
            if (_disposed) return;
        _disposed = true;

            // ���� ť ���� �÷���
_flushTimer?.Dispose();
            FlushBatchAsync().GetAwaiter().GetResult();
  _flushLock?.Dispose();
        }

        // ���� ������ ����
        private enum InsertType 
        { 
            WeightReading, 
            DistanceReading, 
         NavReading, 
            Rfid2ch 
  }
        
        private sealed class PendingInsert
        {
        public InsertType Type { get; set; }
            public DateTimeOffset Time { get; set; }
public string? SessionId { get; set; }
            
            // weight_reading �ʵ�
       public int GrossWeight { get; set; }
            public int RightWeight { get; set; }
          public int LeftWeight { get; set; }
   public int RightBattery { get; set; }
      public int LeftBattery { get; set; }
            public bool RightIsCharging { get; set; }
    public bool LeftIsCharging { get; set; }
         public bool RightOnline { get; set; }
            public bool LeftOnline { get; set; }
            public bool GrossNet { get; set; }
 public bool OverLoad { get; set; }
public bool OutOfTolerance { get; set; }
            
       // distance_reading �ʵ�
        public int DistanceMm { get; set; }
  public bool DistanceConnected { get; set; }
            
     // nav_reading �ʵ�
        public long NavX { get; set; }
    public long NavY { get; set; }
          public long NavT { get; set; }
       public string? ZoneId { get; set; }
          public string? ZoneName { get; set; }
    public string? ProjectId { get; set; }
   public string? MappingId { get; set; }
        public string? MapId { get; set; }
          public string? Result { get; set; }
            public string? VehicleId { get; set; }
            
   // rfid_read_agg �ʵ�
            public string? Epc { get; set; }
          public int Rssi { get; set; }
  public int ReadCount { get; set; }
        }
    }
}
