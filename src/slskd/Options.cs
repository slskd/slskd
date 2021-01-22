using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace slskd
{
    public class Options
    {
        public slskdOptions slskd { get; set; }

        public class slskdOptions
        {
            public bool? Debug { get; set; }
            public bool? NoLogo { get; set; }
            public bool? NoAuth { get; set; }
            public string InstanceName { get; set; }
            public bool? Prometheus { get; set; }
            public bool? Swagger { get; set; }
            public string Loki { get; set; }
        }
    }
}
