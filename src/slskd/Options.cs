namespace slskd
{
    using slskd.Validation;
    using Soulseek.Diagnostics;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Diagnostics;

    public record Option(char ShortName, string LongName, string EnvironmentVariable, string Key, Type Type, object Default = null, string Description = null);

    public class Options
    {
        public static Options Defaults { get; } = new Options();

        public bool Debug { get; private set; } = Debugger.IsAttached;
        public bool NoLogo { get; private set; } = false;
        public string InstanceName { get; private set; } = "default";
        [Validate]
        public DirectoriesOptions Directories { get; private set; } = new DirectoriesOptions();        
        [Validate]
        public WebOptions Web { get; private set; } = new WebOptions();        
        [Validate]
        public LoggerOptions Logger { get; private set; } = new LoggerOptions();        
        [Validate]
        public FeatureOptions Feature { get; private set; } = new FeatureOptions();
        [Validate]
        public SoulseekOptions Soulseek { get; private set; } = new SoulseekOptions();

        public class DirectoriesOptions
        {
            [Required]
            public string Shared { get; private set; }
            [Required]
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
            [Required]
            public string Password { get; private set; }
            [Required]
            public string Username { get; private set; }
            [Range(1024, 65535)]
            public int? ListenPort { get; private set; } = 50000;
            public DiagnosticLevel DiagnosticLevel { get; private set; } = DiagnosticLevel.Info;
            [Validate]
            public DistributedNetworkOptions DistributedNetwork { get; private set; } = new DistributedNetworkOptions();
            [Validate]
            public ConnectionOptions Connection { get; private set; } = new ConnectionOptions();

            public class ConnectionOptions
            {
                [Validate]
                public TimeoutOptions Timeout { get; private set; } = new TimeoutOptions();
                [Validate]
                public BufferOptions Buffer { get; private set; } = new BufferOptions();

                public class BufferOptions
                {
                    [Range(1024, int.MaxValue)]
                    public int Read { get; private set; } = 16384;
                    [Range(1024, int.MaxValue)]
                    public int Write { get; private set; } = 16384;
                }

                public class TimeoutOptions
                {
                    [Range(1000, int.MaxValue)]
                    public int Connect { get; private set; } = 5000;
                    [Range(1000, int.MaxValue)]
                    public int Inactivity { get; private set; } = 15000;
                }
            }

            public class DistributedNetworkOptions
            {
                public bool Disabled { get; private set; } = false;
                [Range(1, int.MaxValue)]
                public int ChildLimit { get; private set; } = 25;
            }
        }

        public class WebOptions
        {
            [Range(1, 65535)]
            public int Port { get; private set; } = 5000;
            [Validate]
            public HttpsOptions Https { get; private set; } = new HttpsOptions();
            public string UrlBase { get; private set; } = "/";
            [Required(ErrorMessage = "A content root is required")]
            public string ContentPath { get; private set; } = "wwwroot";
            [Validate]
            public AuthenticationOptions Authentication { get; private set; } = new AuthenticationOptions();

            public class AuthenticationOptions
            {
                public bool Disable { get; private set; } = false;
                [Required]
                public string Username { get; private set; } = "slskd";
                [Required]
                public string Password { get; private set; } = "slskd";
                [Validate]
                public JwtOptions Jwt { get; private set; } = new JwtOptions();

                public class JwtOptions
                {
                    [Required]
                    public string Key { get; private set; } = Guid.NewGuid().ToString();
                    [Range(3600, int.MaxValue)]
                    public int Ttl { get; private set; } = 604800000;
                }
            }

            public class HttpsOptions
            {
                [Range(1, 65535)]
                public int Port { get; private set; } = 5001;
                public bool Force { get; private set; } = false;
                [Validate]
                public CertificateOptions Certificate { get; private set; } = new CertificateOptions();

                public class CertificateOptions
                {
                    [FileExists]
                    public string Pfx { get; private set; }
                    public string Password { get; private set; }
                }
            }
        }

        public bool TryValidate(out CompositeValidationResult result)
        {
            result = null;
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(this, new ValidationContext(this), results, true))
            {
                result = new CompositeValidationResult("Invalid configuration", results);
                return false;
            }

            return true;
        }

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
                ShortName: 'g',
                LongName: "generate-cert",
                EnvironmentVariable: null,
                Key: null,
                Type: typeof(bool),
                Default: false,
                Description: "generate X509 certificate and password for HTTPs"),
            new(
                ShortName: 'd',
                LongName: "debug",
                EnvironmentVariable: "DEBUG",
                Key: "slskd:debug",
                Type: typeof(bool),
                Default: Defaults.Debug,
                Description: "run in debug mode"),
            new(
                ShortName: 'n',
                LongName: "no-logo",
                EnvironmentVariable: "NO_LOGO",
                Key: "slskd:nologo",
                Type: typeof(bool),
                Default: Defaults.NoLogo,
                Description: "suppress logo on startup"),
            new(
                ShortName: 'c',
                LongName: "config",
                EnvironmentVariable: "CONFIG",
                Key: null,
                Type: typeof(string),
                Default: Program.DefaultConfigurationFile,
                Description: "path to configuration file"),
            new(
                ShortName: 'i',
                LongName: "instance-name",
                EnvironmentVariable: "INSTANCE_NAME",
                Key: "slskd:instancename",
                Type: typeof(string),
                Default: Defaults.InstanceName,
                Description: "optional; a unique name for this instance"),
            new(
                ShortName: 'o',
                LongName: "downloads",
                EnvironmentVariable: "DOWNLOADS_DIR",
                Key: "slskd:directories:downloads",
                Type: typeof(string),
                Default: Defaults.Directories.Downloads,
                Description: "path where downloaded files are saved"),
            new(
                ShortName: 's',
                LongName: "shared",
                EnvironmentVariable: "SHARED_DIR",
                Key: "slskd:directories:shared",
                Type: typeof(string),
                Default: Defaults.Directories.Shared,
                Description: "path to shared files"),
            new(
                ShortName: 'l',
                LongName: "http-port",
                EnvironmentVariable: "HTTP_PORT",
                Key: "slskd:web:port",
                Type: typeof(int),
                Default: Defaults.Web.Port,
                Description: "HTTP listen port for web server"),
            new(
                ShortName: 'L',
                LongName: "https-port",
                EnvironmentVariable: "HTTPS_PORT",
                Key: "slskd:web:https:port",
                Type: typeof(int),
                Default: Defaults.Web.Https.Port,
                Description: "HTTPS listen port for web server"),
            new(
                ShortName: 'f',
                LongName: "force-https",
                EnvironmentVariable: "HTTPS_FORCE",
                Key: "slskd:web:https:force",
                Type: typeof(bool),
                Default: Defaults.Web.Https.Force,
                Description: "redirect HTTP to HTTPS"),
            new(
                ShortName: default,
                LongName: "https-cert-pfx",
                EnvironmentVariable: "HTTPS_CERT_PFX",
                Key: "slskd:web:https:certificate:pfx",
                Type: typeof(string),
                Default: Defaults.Web.Https.Certificate.Pfx,
                Description: "path to X509 certificate .pfx"),
            new(
                ShortName: default,
                LongName: "https-cert-password",
                EnvironmentVariable: "HTTPS_CERT_PASSWORD",
                Key: "slskd:web:https:certificate:password",
                Type: typeof(string),
                Default: Defaults.Web.Https.Certificate.Password,
                Description: "X509 certificate password"),
            new(
                ShortName: default,
                LongName: "url-base",
                EnvironmentVariable: "URL_BASE",
                Key: "slskd:web:urlbase",
                Type: typeof(string),
                Default: Defaults.Web.UrlBase,
                Description: "base url for web requests"),
            new(
                ShortName: default,
                LongName: "content-path",
                EnvironmentVariable: "CONTENT_PATH",
                Key: "slskd:web:contentpath",
                Type: typeof(string),
                Default: Defaults.Web.ContentPath,
                Description: "path to static web content"),
            new(
                ShortName: 'x',
                LongName: "no-auth",
                EnvironmentVariable: "NO_AUTH",
                Key: "slskd:web:authentication:disable",
                Type: typeof(bool),
                Default: Defaults.Web.Authentication.Disable,
                Description: "disable authentication for web requests"),
            new(
                ShortName: 'u',
                LongName: "username",
                EnvironmentVariable: "USERNAME",
                Key: "slskd:web:authentication:username",
                Type: typeof(string),
                Default: Defaults.Web.Authentication.Username,
                Description: "the username for web UI"),
            new(
                ShortName: 'p',
                LongName: "password",
                EnvironmentVariable: "PASSWORD",
                Key: "slskd:web:authentication:password",
                Type: typeof(string),
                Default: Defaults.Web.Authentication.Password,
                Description: "the password for web UI"),
            new(
                ShortName: default,
                LongName: "jwt-key",
                EnvironmentVariable: "JWT_KEY",
                Key: "slskd:web:authentication:jwt:key",
                Type: typeof(string),
                Default: Defaults.Web.Authentication.Jwt.Key,
                Description: "JWT signing key"),
            new(
                ShortName: default,
                LongName: "jwt-ttl",
                EnvironmentVariable: "JWT_TTL",
                Key: "slskd:web:authentication:jwt:ttl",
                Type: typeof(int),
                Default: Defaults.Web.Authentication.Jwt.Ttl,
                Description: "TTL for JWTs"),
            new(
                ShortName: default,
                LongName: "prometheus",
                EnvironmentVariable: "PROMETHEUS",
                Key:  "slskd:feature:prometheus",
                Type: typeof(bool),
                Default: Defaults.Feature.Prometheus,
                Description: "enable collection and publishing of prometheus metrics"),
            new(
                ShortName: default,
                LongName: "swagger",
                EnvironmentVariable: "SWAGGER",
                Key: "slskd:feature:swagger",
                Type: typeof(bool),
                Default: Defaults.Feature.Swagger,
                Description: "enable swagger documentation and UI"),
            new(
                ShortName: default,
                LongName: "loki",
                EnvironmentVariable: "LOKI",
                Key: "slskd:logger:loki",
                Type: typeof(string),
                Default: Defaults.Logger.Loki,
                Description: "optional; url to a Grafana Loki instance to which to log"),
            new(
                ShortName: default,
                LongName: "slsk-username",
                EnvironmentVariable: "SLSK_USERNAME",
                Key: "slskd:soulseek:username",
                Type: typeof(string),
                Default: Defaults.Soulseek.Username,
                Description: "username for the Soulseek network"),
            new(
                ShortName: default,
                LongName: "slsk-password",
                EnvironmentVariable: "SLSK_PASSWORD",
                Key: "slskd:soulseek:password",
                Type: typeof(string),
                Default: Defaults.Soulseek.Password,
                Description: "password for the Soulseek network"),
            new(
                ShortName: default,
                LongName: "slsk-listen-port",
                EnvironmentVariable: "SLSK_LISTEN_PORT",
                Key: "slskd:soulseek:listenport",
                Type: typeof(int),
                Default: Defaults.Soulseek.ListenPort,
                Description: "port on which to listen for incoming connections"),
            new(
                ShortName: default,
                LongName: "slsk-diag-level",
                EnvironmentVariable: "SLSK_DIAG_LEVEL",
                Key: "slskd:soulseek:diagnosticlevel",
                Type: typeof(DiagnosticLevel),
                Default: Defaults.Soulseek.DiagnosticLevel,
                Description: "minimum diagnostic level (None, Warning, Info, Debug)"),
            new(
                ShortName: default,
                LongName: "slsk-no-dnet",
                EnvironmentVariable: "SLSK_NO_DNET",
                Key: "slskd:soulseek:distributednetwork:disabled",
                Type: typeof(bool),
                Default: Defaults.Soulseek.DistributedNetwork.Disabled,
                Description: "disable the distributed network"),
            new(
                ShortName: default,
                LongName: "slsk-dnet-children",
                EnvironmentVariable: "SLSK_DNET_CHILDREN",
                Key: "slskd:soulseek:distributednetwork:childlimit",
                Type: typeof(int),
                Default: Defaults.Soulseek.DistributedNetwork.ChildLimit,
                Description: "max number of distributed children"),
            new(
                ShortName: default,
                LongName: "slsk-connection-timeout",
                EnvironmentVariable: "SLSK_CONNECTION_TIMEOUT",
                Key: "slskd:soulseek:connection:timeout:connect",
                Type: typeof(int),
                Default: Defaults.Soulseek.Connection.Timeout.Connect,
                Description: "connection timeout, in miliseconds"),
            new(
                ShortName: default,
                LongName: "slsk-inactivity-timeout",
                EnvironmentVariable: "SLSK_INACTIVITY_TIMEOUT",
                Key: "slskd:soulseek:connection:timeout:inactivity",
                Type: typeof(int),
                Default: Defaults.Soulseek.Connection.Timeout.Inactivity,
                Description: "connection inactivity timeout, in miliseconds"),
            new(
                ShortName: default,
                LongName: "slsk-read-buffer",
                EnvironmentVariable: "SLSK_READ_BUFFER",
                Key: "slskd:soulseek:connection:buffer:read",
                Type: typeof(int),
                Default: Defaults.Soulseek.Connection.Buffer.Read,
                Description: "read buffer size for connections"),
            new(
                ShortName: default,
                LongName: "slsk-write-buffer",
                EnvironmentVariable: "SLSK_WRITE_BUFFER",
                Key: "slskd:soulseek:connection:buffer:write",
                Type: typeof(int),
                Default: Defaults.Soulseek.Connection.Buffer.Write,
                Description: "write buffer size for connections"),
        };
    }
}