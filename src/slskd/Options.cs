using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace slskd
{
    public class Options
    {
        public class slskd 
        {
            public Soulseek Soulseek { get; set; }
            public IEnumerable<User> Users { get; set; }
            public IEnumerable<ApiKey> ApiKeys { get; set; }
        }

        public class Soulseek
        {
            public SoulseekCredentials Credentials { get; set; }
            public SoulseekDistributedNetwork DistributedNetwork { get; set; }

            [Range(1024, 65535)]
            public int ListenPort { get; set; }

            public class SoulseekCredentials
            {
                public string Username { get; set; }
                public string Password { get; set; }
            }

            public class SoulseekDistributedNetwork
            {
                public bool Enable { get; set; }
                public int ChildLimit { get; set; }
            }
        }

        public class User
        {
            public string Username { get; set; }
            public string PasswordHash { get; set; }
            public Roles Role { get; set; }
        }

        public class ApiKey
        {
            public string Key { get; set; }
            public Roles Role { get; set; }
        }
    }
}
