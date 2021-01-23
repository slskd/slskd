namespace slskd
{
    using System;
    using System.Collections.Generic;

    public class RuntimeOptions
    {
        public static IEnumerable<EnvironmentVariable> EnvironmentVariableMap => new[]
        {
            new EnvironmentVariable {
                Description = "run in debug mode",
                Name = "SLSK_DEBUG",
                Key = "slskd:debug"
            },
            new EnvironmentVariable {
                Description = "optional; a unique name for this instance",
                Name = "SLSK_INSTANCE_NAME",
                Key = "slskd:instancename"
            },
            new EnvironmentVariable {
                Description = "enable collection and publish of prometheus metrics",
                Name = "SLSK_PROMETHEUS",
                Key = "slskd:feature:prometheus"
            },
            new EnvironmentVariable {
                Description = "enable swagger documentation",
                Name = "SLSK_SWAGGER",
                Key = "slskd:feature:swagger"
            },
            new EnvironmentVariable {
                Description = "jwt signing key",
                Name = "SLSKD_JWT_KEY",
                Key = "slskd:web:jwt:key"
            }
        };

        public static IEnumerable<Argument> ArgumentMap => new[]
        {
            new Argument
            {
                Description = "print usage",
                ShortName = 'h',
                LongName = "help",
                Key = "slskd:showhelp"
            },
            new Argument {
                Description = "display version information",
                ShortName = 'v',
                LongName = "version",
                Key = "slskd:showversion"
            },
            new Argument {
                Description = "run in debug mode",
                ShortName = 'd',
                LongName = "debug",
                Key = "slskd:debug"
            },
            new Argument
            {
                Description = "",
                ShortName = 'i',
                LongName = "instance-name",
                Key = "slskd:instancename"
            }
        };

        public class slskd
        {
            public bool ShowHelp { get; private set; } = false;
            public bool ShowVersion { get; private set; } = false;
            public bool Debug { get; private set; } = false;
            public bool NoLogo { get; private set; } = false;
            public bool NoAuth { get; private set; } = false;
            public string InstanceName { get; private set; } = "default";
            public FeatureOptions Feature { get; private set; } = new FeatureOptions();
            public LoggerOptions Logger { get; private set; } = new LoggerOptions();
            public WebOptions Web { get; private set; } = new WebOptions();

            public class FeatureOptions
            {
                public bool Prometheus { get; private set; } = false;
                public bool Swagger { get; private set; } = false;
            }

            public class LoggerOptions
            {
                public string Loki { get; private set; } = null;
            }

            public class WebOptions
            {
                public string UrlBase { get; private set; } = "/";
                public string ContentPath { get; private set; } = "wwwroot";
                public JwtOptions Jwt { get; private set; } = new JwtOptions();

                public class JwtOptions
                {
                    public int TTL { get; private set; } = 1234567890;
                    public string Key { get; private set; } = Guid.NewGuid().ToString();
                }
            }
        }

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
