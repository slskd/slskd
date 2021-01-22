namespace slskd
{
    using System.Collections.Generic;

    public class RuntimeOptions
    {
        public class Soulseek
        {
            public string Username { get; set; } = InstanceOptions.Username;
            public string Password { get; set; } = InstanceOptions.Password;
            public int ListenPort { get; set; } = 50000;
            public DistributedNetworkOptions DistributedNetwork { get; set; }
            public ConnectionOptions Connection { get; set; }

            public class DistributedNetworkOptions
            {
                public bool Enabled { get; set; } = true;
                public int ChildLimit { get; set; } = 10;
            }

            public class ConnectionOptions
            {
                public TimeoutOptions Timeout { get; set; }
                public BufferOptions Buffer { get; set; }

                public class TimeoutOptions
                {
                    public int Connect { get; set; } = 5000;
                    public int Inactivity { get; set; } = 15000;
                }

                public class BufferOptions
                {
                    public int Read { get; set; } = 16384;
                    public int Write { get; set; } = 16384;
                }
            }
        }

        public class Authentication
        {
            public IEnumerable<User> Users { get; set; }
            public IEnumerable<ApiKey> ApiKeys { get; set; }

            public class User
            {
                public string Name { get; set; }
                public string Password { get; set; }
                public Role Role { get; set; }
            }

            public class ApiKey
            {
                public string Key { get; set; }
                public Role Role { get; set; }
            }
        }
    }
}
