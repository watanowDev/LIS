using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Interfaces;

namespace WATA.LIS.Core.Model.SystemConfig
{
    public class MainConfigModel : IMainModel
    {
        public string device_type { get; set; }
        public string workLocationId { get; set; }
        public string projectId { get; set; }
        public string mappingId { get; set; }
        public string mapId { get; set; }
        public string vehicleId { get; set; }
        // optional: action deduplication window in milliseconds (will be clamped 200~500)
        public int? action_dedup_ms { get; set; }

        // Database configuration (optional; defaults applied when not set)
        public string db_host { get; set; } // default: localhost
        public int? db_port { get; set; }   // default: 5432
        public string db_database { get; set; } // e.g., forkliftDB
        public string db_username { get; set; } // e.g., postgres
        public string db_password { get; set; } // e.g., wata20190430
        public string db_search_path { get; set; } // default: "lis_core,public"
    }
}



