namespace slskd
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using YamlDotNet.Serialization;

    public record Option(char ShortName, string LongName, string EnvironmentVariable, Type Type, string Key, string Description = null)
    {
        public CommandLineArgument ToCommandLineArgument()
            => new CommandLineArgument(ShortName, LongName, Type, Key, Description);

        public EnvironmentVariable ToEnvironmentVariable()
            => new EnvironmentVariable(EnvironmentVariable, Type, Key, Description);
    }

    public class Options
    {
        public static IEnumerable<Option> Map => new Option[]
        {
            new(
                ShortName: 'v',
                LongName: "version",
                EnvironmentVariable: null,
                Type: typeof(bool),
                Key: null,
                Description: "display version information"),
            new(
                ShortName: 'h',
                LongName: "help",
                EnvironmentVariable: null,
                Type: typeof(bool),
                Key: null,
                Description: "display command line usage"),
            new(
                ShortName: 'e',
                LongName: "envars",
                EnvironmentVariable: null,
                Type: typeof(bool),
                Key: null,
                Description: "display environment variables"),
            new(
                ShortName: 'd',
                LongName: "debug",
                EnvironmentVariable: "DEBUG",
                Type: typeof(bool),
                Key: "slskd:debug",
                Description: "run in debug mode"),
            new(
                ShortName: 'n',
                LongName: "no-logo",
                EnvironmentVariable: "NO_LOGO",
                Type: typeof(bool),
                Key: "slskd:nologo",
                Description: "suppress logo on startup"),
            new(
                ShortName: 'x',
                LongName: "no-auth",
                EnvironmentVariable: "NO_AUTH",
                Type: typeof(bool),
                Key: "slskd:web:noauth",
                Description: "disable authentication for web requests"),
            new(
                ShortName: 'i',
                LongName: "instance-name",
                EnvironmentVariable: "INSTANCE_NAME",
                Type: typeof(string),
                Key: "slskd:instancename",
                Description: "optional; a unique name for this instance"),
            new(
                ShortName: 'o',
                LongName: "port",
                EnvironmentVariable: "PORT",
                Type: typeof(string),
                Key: "slskd:web:port",
                Description: "listen port for web server"),
            new(
                ShortName: 'b',
                LongName: "url-base",
                EnvironmentVariable: "URL_BASE",
                Type: typeof(string),
                Key: "slskd:web:urlbase",
                Description: "base url for web requests"),
            new(
                ShortName: 'c',
                LongName: "content-path",
                EnvironmentVariable: "CONTENT_PATH",
                Type: typeof(string),
                Key: "slskd:web:contentpath",
                Description: "path to static web content"),
            new(
                ShortName: 'k',
                LongName: "jwt-key",
                EnvironmentVariable: "JWT_KEY",
                Type: typeof(string),
                Key: "slskd:web:jwt:key",
                Description: "the key with which to sign JWTs"),
            new(
                ShortName: 't',
                LongName: "jwt-ttl",
                EnvironmentVariable: "JWT_TTL",
                Type: typeof(int),
                Key: "slskd:web:jwt:ttl",
                Description: "the TTL for JWTs"),
            new(
                ShortName: 'u',
                LongName: "username",
                EnvironmentVariable: "USERNAME",
                Type: typeof(string),
                Key: "slskd:soulseek:username",
                Description: "the username for the Soulseek network"),
            new(
                ShortName: 'p',
                LongName: "password",
                EnvironmentVariable: "PASSWORD",
                Type: typeof(string),
                Key: "slskd:soulseek:password",
                Description: "the password for the Soulseek network"),
            new(
                ShortName: 'l',
                LongName: "listen-port",
                EnvironmentVariable: "LISTEN_PORT",
                Type: typeof(int),
                Key: "slskd:soulseek:listenport",
                Description: "the port on which to listen"),
            new(
                ShortName: 'n',
                LongName: "distributed-network",
                EnvironmentVariable: "DNET",
                Type: typeof(bool),
                Key: "slskd:soulseek:distributednetwork:enabled",
                Description: "enables the distributed network"),
            new(
                ShortName: 'c',
                LongName: "child-limit",
                EnvironmentVariable: "DNET_CHILDREN",
                Type: typeof(int),
                Key: "slskd:soulseek:distributednetwork:childlimit",
                Description: "sets the limit for the number of distributed children"),
            new(
                ShortName: default,
                LongName: "prometheus",
                EnvironmentVariable: "PROMETHEUS",
                Type: typeof(bool),
                Key:  "slskd:feature:prometheus",
                Description: "enable collection and publishing of prometheus metrics"),
            new(
                ShortName: default,
                LongName: "swagger",
                EnvironmentVariable: "SWAGGER",
                Type: typeof(bool),
                Key: "slskd:feature:swagger",
                Description: "enable swagger documentation and UI"),
            new(
                ShortName: default,
                LongName: "loki",
                EnvironmentVariable: "LOKI",
                Type: typeof(string),
                Key: "slskd:logger:loki",
                Description: "optional; the url to a Grafana Loki instance to which to log"),
        };

        [YamlIgnore]
        public bool IsUsingDefaultCredentials => Username == Password && Password == "slskd";

        public string Username { get; private set; } = "slskd";
        public string Password { get; private set; } = "slskd";
        public string InstanceName { get; private set; } = "default";
        public bool Debug { get; private set; } = Debugger.IsAttached;
        public bool NoLogo { get; private set; } = false;
        public WebOptions Web { get; private set; } = new WebOptions();
        public SoulseekOptions Soulseek { get; private set; } = new SoulseekOptions();
        public FeatureOptions Feature { get; private set; } = new FeatureOptions();
        public LoggerOptions Logger { get; private set; } = new LoggerOptions();

        public class FeatureOptions
        {
            public bool Prometheus { get; private set; } = false;
            public bool Swagger { get; private set; } = false;
        }

        public class LoggerOptions
        {
            public string Loki { get; private set; } = null;
        }

        public class SoulseekOptions
        {
            public ConnectionOptions Connection { get; set; } = new ConnectionOptions();
            public DistributedNetworkOptions DistributedNetwork { get; set; } = new DistributedNetworkOptions();
            public int? ListenPort { get; set; } = 50000;
            public string Password { get; set; }
            public string Username { get; set; }

            public class ConnectionOptions
            {
                public TimeoutOptions Timeout { get; set; } = new TimeoutOptions();
                public BufferOptions Buffer { get; set; } = new BufferOptions();

                public class BufferOptions
                {
                    public int Read { get; set; } = 16384;
                    public int Write { get; set; } = 16384;
                }

                public class TimeoutOptions
                {
                    public int Connect { get; set; } = 5000;
                    public int Inactivity { get; set; } = 15000;
                }
            }

            public class DistributedNetworkOptions
            {
                public bool Enabled { get; set; } = true;
                public int ChildLimit { get; set; } = 10;
            }
        }

        public class WebOptions
        {
            public bool NoAuth { get; private set; } = false;
            public string UrlBase { get; private set; } = "/";
            public string ContentPath { get; private set; } = "wwwroot";
            public JwtOptions Jwt { get; private set; } = new JwtOptions();

            public class JwtOptions
            {
                public string Key { get; private set; } = Guid.NewGuid().ToString();
                public int Ttl { get; private set; } = 604800000;
            }
        }
    }
}