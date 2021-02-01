namespace slskd
{
    using Soulseek.Diagnostics;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    public record Option(char ShortName, string LongName, string EnvironmentVariable, string Key, Type Type, object Default = null, string Description = null)
    {
        public CommandLineArgument ToCommandLineArgument()
            => new (ShortName, LongName, Type, Key, Description);

        public EnvironmentVariable ToEnvironmentVariable()
            => new (EnvironmentVariable, Type, Key, Description);

        public DefaultValue ToDefaultValue()
            => new (Key, Type, Default);
    }

    public class Options
    {
        public static IEnumerable<Option> Map => new Option[]
        {
            new(
                ShortName: 'v',
                LongName: "version",
                EnvironmentVariable: null,
                Key: null,
                Type: typeof(bool),
                Default: false,
                Description: "display version information"),
            new(
                ShortName: 'h',
                LongName: "help",
                EnvironmentVariable: null,
                Key: null,
                Type: typeof(bool),
                Default: false,
                Description: "display command line usage"),
            new(
                ShortName: 'e',
                LongName: "envars",
                EnvironmentVariable: null,
                Key: null,
                Type: typeof(bool),
                Default: false,
                Description: "display environment variables"),
            new(
                ShortName: 'd',
                LongName: "debug",
                EnvironmentVariable: "DEBUG",
                Key: "slskd:debug",
                Type: typeof(bool),
                Default: Debugger.IsAttached,
                Description: "run in debug mode"),
            new(
                ShortName: 'n',
                LongName: "no-logo",
                EnvironmentVariable: "NO_LOGO",
                Key: "slskd:nologo",
                Type: typeof(bool),
                Default: false,
                Description: "suppress logo on startup"),
            new(
                ShortName: 'i',
                LongName: "instance-name",
                EnvironmentVariable: "INSTANCE_NAME",
                Key: "slskd:instancename",
                Type: typeof(string),
                Default: "default",
                Description: "optional; a unique name for this instance"),
            new(
                ShortName: 'o',
                LongName: "downloads",
                EnvironmentVariable: "DOWNLOADS_DIR",
                Key: "slskd:directories:downloads",
                Type: typeof(string),
                Default: null,
                Description: "the path where downloaded files are saved"),
            new(
                ShortName: 's',
                LongName: "shared",
                EnvironmentVariable: "SHARED_DIR",
                Key: "slskd:directories:shared",
                Type: typeof(string),
                Default: null,
                Description: "the path to shared files"),
            new(
                ShortName: 'l',
                LongName: "http-port",
                EnvironmentVariable: "HTTP_PORT",
                Key: "slskd:web:port",
                Type: typeof(int),
                Default: 5000,
                Description: "HTTP listen port for web server"),
            new(
                ShortName: 'L',
                LongName: "https-port",
                EnvironmentVariable: "HTTPS_PORT",
                Key: "slskd:web:https:port",
                Type: typeof(int),
                Default: 5001,
                Description: "HTTPS listen port for web server"),
            new(
                ShortName: 'f',
                LongName: "force-https",
                EnvironmentVariable: "HTTPS_FORCE",
                Key: "slskd:web:https:force",
                Type: typeof(bool),
                Default: false,
                Description: "redirect HTTP to HTTPS"),
            new(
                ShortName: default,
                LongName: "url-base",
                EnvironmentVariable: "URL_BASE",
                Key: "slskd:web:urlbase",
                Type: typeof(string),
                Default: "/",
                Description: "base url for web requests"),
            new(
                ShortName: default,
                LongName: "content-path",
                EnvironmentVariable: "CONTENT_PATH",
                Key: "slskd:web:contentpath",
                Type: typeof(string),
                Default: "wwwroot",
                Description: "path to static web content"),
            new(
                ShortName: 'x',
                LongName: "no-auth",
                EnvironmentVariable: "NO_AUTH",
                Key: "slskd:web:authentication:disable",
                Type: typeof(bool),
                Default: false,
                Description: "disable authentication for web requests"),
            new(
                ShortName: 'u',
                LongName: "username",
                EnvironmentVariable: "USERNAME",
                Key: "slskd:web:authentication:username",
                Type: typeof(string),
                Default: "slskd",
                Description: "the username for the web UI"),
            new(
                ShortName: 'p',
                LongName: "password",
                EnvironmentVariable: "PASSWORD",
                Key: "slskd:web:authentication:password",
                Type: typeof(string),
                Default: "slskd",
                Description: "the password for web UI"),
            new(
                ShortName: default,
                LongName: "jwt-key",
                EnvironmentVariable: "JWT_KEY",
                Key: "slskd:web:jwt:key",
                Type: typeof(string),
                Default: Guid.NewGuid(),
                Description: "the key with which to sign JWTs"),
            new(
                ShortName: default,
                LongName: "jwt-ttl",
                EnvironmentVariable: "JWT_TTL",
                Key: "slskd:web:jwt:ttl",
                Type: typeof(int),
                Default: 604800000,
                Description: "the TTL for JWTs"),
            new(
                ShortName: default,
                LongName: "prometheus",
                EnvironmentVariable: "PROMETHEUS",
                Key:  "slskd:feature:prometheus",
                Type: typeof(bool),
                Default: false,
                Description: "enable collection and publishing of prometheus metrics"),
            new(
                ShortName: default,
                LongName: "swagger",
                EnvironmentVariable: "SWAGGER",
                Key: "slskd:feature:swagger",
                Type: typeof(bool),
                Default: false,
                Description: "enable swagger documentation and UI"),
            new(
                ShortName: default,
                LongName: "loki",
                EnvironmentVariable: "LOKI",
                Key: "slskd:logger:loki",
                Type: typeof(string),
                Description: "optional; the url to a Grafana Loki instance to which to log"),
            new(
                ShortName: default,
                LongName: "soulseek-username",
                EnvironmentVariable: "SOULSEEK_USERNAME",
                Key: "slskd:soulseek:username",
                Type: typeof(string),
                Description: "the username for the Soulseek network"),
            new(
                ShortName: default,
                LongName: "soulseek-password",
                EnvironmentVariable: "SOULSEEK_PASSWORD",
                Key: "slskd:soulseek:password",
                Type: typeof(string),
                Description: "the password for the Soulseek network"),
            new(
                ShortName: default,
                LongName: "soulseek-listen-port",
                EnvironmentVariable: "SOULSEEK_LISTEN_PORT",
                Key: "slskd:soulseek:listenport",
                Type: typeof(int),
                Default: 50000,
                Description: "the port on which to listen for incoming connections"),
            new(
                ShortName: default,
                LongName: "soulseek-diagnostic-level",
                EnvironmentVariable: "SOULSEEK_DIAGNOSTIC_LEVEL",
                Key: "slskd:soulseek:diagnosticlevel",
                Type: typeof(DiagnosticLevel),
                Default: DiagnosticLevel.Info,
                Description: "the minimum diagnostic level (None, Warning, Info, Debug)"),
            new(
                ShortName: default,
                LongName: "soulseek-no-dnet",
                EnvironmentVariable: "SOULSEEK_NO_DNET",
                Key: "slskd:soulseek:distributednetwork:disabled",
                Type: typeof(bool),
                Default: false,
                Description: "disables the distributed network"),
            new(
                ShortName: default,
                LongName: "soulseek-dnet-children",
                EnvironmentVariable: "SOULSEEK_DNET_CHILDREN",
                Key: "slskd:soulseek:distributednetwork:childlimit",
                Type: typeof(int),
                Default: 25,
                Description: "sets the limit for the number of distributed children"),
            new(
                ShortName: default,
                LongName: "soulseek-connection-timeout",
                EnvironmentVariable: "SOULSEEK_CONNECTION_TIMEOUT",
                Key: "slskd:soulseek:connection:timeout:connect",
                Type: typeof(int),
                Default: 5000,
                Description: "sets the connection timeout, in miliseconds"),
            new(
                ShortName: default,
                LongName: "soulseek-inactivity-timeout",
                EnvironmentVariable: "SOULSEEK_INACTIVITY_TIMEOUT",
                Key: "slskd:soulseek:connection:timeout:inactivity",
                Type: typeof(int),
                Default: 15000,
                Description: "sets the connection inactivity timeout, in miliseconds"),
            new(
                ShortName: default,
                LongName: "soulseek-read-buffer",
                EnvironmentVariable: "SOULSEEK_READ_BUFFER",
                Key: "slskd:soulseek:connection:buffer:read",
                Type: typeof(int),
                Default: 16384,
                Description: "sets the read buffer size for connections"),
            new(
                ShortName: default,
                LongName: "soulseek-write-buffer",
                EnvironmentVariable: "SOULSEEK_WRITE_BUFFER",
                Key: "slskd:soulseek:connection:buffer:write",
                Type: typeof(int),
                Default: 16384,
                Description: "sets the write buffer size for connections"),
        };

        public bool Debug { get; private set; } = Debugger.IsAttached;
        public bool NoLogo { get; private set; } = false;
        public string InstanceName { get; private set; } = "default";
        public DirectoriesOptions Directories { get; private set; } = new DirectoriesOptions();
        public WebOptions Web { get; private set; } = new WebOptions();
        public LoggerOptions Logger { get; private set; } = new LoggerOptions();
        public FeatureOptions Feature { get; private set; } = new FeatureOptions();
        public SoulseekOptions Soulseek { get; private set; } = new SoulseekOptions();

        public class DirectoriesOptions
        {
            public string Shared { get; private set; }
            public string Downloads { get; private set; }
        }

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
            public string Password { get; private set; }
            public string Username { get; private set; }
            public int? ListenPort { get; private set; } = 50000;
            public DiagnosticLevel DiagnosticLevel { get; private set; } = DiagnosticLevel.Info;
            public DistributedNetworkOptions DistributedNetwork { get; private set; } = new DistributedNetworkOptions();
            public ConnectionOptions Connection { get; private set; } = new ConnectionOptions();

            public class ConnectionOptions
            {
                public TimeoutOptions Timeout { get; private set; } = new TimeoutOptions();
                public BufferOptions Buffer { get; private set; } = new BufferOptions();

                public class BufferOptions
                {
                    public int Read { get; private set; } = 16384;
                    public int Write { get; private set; } = 16384;
                }

                public class TimeoutOptions
                {
                    public int Connect { get; private set; } = 5000;
                    public int Inactivity { get; private set; } = 15000;
                }
            }

            public class DistributedNetworkOptions
            {
                public bool Disabled { get; private set; } = false;
                public int ChildLimit { get; private set; } = 25;
            }
        }

        public class WebOptions
        {
            public int Port { get; private set; } = 5000;
            public HttpsOptions Https { get; private set; } = new HttpsOptions();
            public string UrlBase { get; private set; } = "/";
            public string ContentPath { get; private set; } = "wwwroot";
            public AuthenticationOptions Authentication { get; private set; } = new AuthenticationOptions();
            public JwtOptions Jwt { get; private set; } = new JwtOptions();

            public class AuthenticationOptions
            {
                public bool Disable { get; private set; } = false;
                public string Username { get; private set; } = "slskd";
                public string Password { get; private set; } = "slskd";
            }

            public class JwtOptions
            {
                public string Key { get; private set; } = Guid.NewGuid().ToString();
                public int Ttl { get; private set; } = 604800000;
            }

            public class HttpsOptions
            {
                public int Port { get; private set; } = 5001;
                public bool Force { get; private set; } = false;
            }
        }
    }
}