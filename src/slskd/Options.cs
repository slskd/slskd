// <copyright file="Options.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using slskd.Common.Configuration;
    using slskd.Validation;
    using Soulseek.Diagnostics;
    using Utility.CommandLine;
    using Utility.EnvironmentVariables;

    /// <summary>
    ///     Defines an option mapping.
    /// </summary>
    public record Option(char ShortName, string LongName, string EnvironmentVariable, string Key, Type Type, object Default = null, string Description = null);

    /// <summary>
    ///     Application options (immutable options required at startup).
    /// </summary>
    public class Options
    {
        ///// <summary>
        /////     Gets the list of option mappings.
        ///// </summary>
        ///// <remarks>
        /////     <para>
        /////         This array lists all application options. Each element in the list is a mapping object that dictates inputs
        /////         (command line, envar, config file) for the option, a default value, description, and most importantly, the
        /////         configuration key which will ultimately contain the derived value.
        /////     </para>
        /////     <para>
        /////         Configuration keys are a construct of the <see cref="Microsoft.Extensions.Configuration"/> namespace and, in a
        /////         nutshell, represent an object map as a series of key-value pairs with nested properties represented as
        /////         colon-separated strings. Acceptable 'Key' values in this array must either be null (the value will not be
        /////         mapped to anything in Options), or a colon-separated string corresponding to a property somewhere inside Options.
        /////     </para>
        /////     <para>
        /////         This mapping exists so that disparate configuration keys from various configuration sources can be overridden
        /////         by subsequent sources; for example, --foo (cli), FOOBAR (envar) and foo.bar (yaml) would result in three
        /////         distinct keys.  With this mapping all three can be made to map to the same key and the application can
        /////         dictate order of precedence.
        /////     </para>
        /////     <para>
        /////         Some options, specifically those lacking a 'Key', are derived prior to the configuration file being loaded and
        /////         their values are not needed in Options.  This approach should be reserved for flags that may result in the
        /////         application carrying out a command, and then exiting.  For example, --help, --version.  These options are
        /////         generally properties in <see cref="Program"/> and are marked with <see cref="ArgumentAttribute"/> and
        /////         potentially <see cref="EnvironmentVariableAttribute"/> to allow for mapping by other means.
        /////     </para>
        /////     <para>
        /////         The order in which mappings appear in the array dictate the order in which they appear when listed in --help
        /////         and --envars.
        /////     </para>
        ///// </remarks>
        //public static IEnumerable<Option> Map => new Option[]
        //{
        //    new(
        //        ShortName: 'v',
        //        LongName: "version",
        //        EnvironmentVariable: null,
        //        Key: null,
        //        Type: typeof(bool),
        //        Default: false,
        //        Description: "display version information"),
        //    new(
        //        ShortName: 'h',
        //        LongName: "help",
        //        EnvironmentVariable: null,
        //        Key: null,
        //        Type: typeof(bool),
        //        Default: false,
        //        Description: "display command line usage"),
        //    new(
        //        ShortName: 'e',
        //        LongName: "envars",
        //        EnvironmentVariable: null,
        //        Key: null,
        //        Type: typeof(bool),
        //        Default: false,
        //        Description: "display environment variables"),
        //    new(
        //        ShortName: 'g',
        //        LongName: "generate-cert",
        //        EnvironmentVariable: null,
        //        Key: null,
        //        Type: typeof(bool),
        //        Default: false,
        //        Description: "generate X509 certificate and password for HTTPs"),
        //    new(
        //        ShortName: 'c',
        //        LongName: "config",
        //        EnvironmentVariable: "CONFIG",
        //        Key: null,
        //        Type: typeof(string),
        //        Default: Program.DefaultConfigurationFile,
        //        Description: "path to configuration file"),
        //    new(
        //        ShortName: 'd',
        //        LongName: "debug",
        //        EnvironmentVariable: "DEBUG",
        //        Key: "slskd:debug",
        //        Type: typeof(bool),
        //        Default: Defaults.Debug,
        //        Description: "run in debug mode"),
        //    new(
        //        ShortName: 'n',
        //        LongName: "no-logo",
        //        EnvironmentVariable: "NO_LOGO",
        //        Key: "slskd:nologo",
        //        Type: typeof(bool),
        //        Default: Defaults.NoLogo,
        //        Description: "suppress logo on startup"),
        //    new(
        //        ShortName: 'i',
        //        LongName: "instance-name",
        //        EnvironmentVariable: "INSTANCE_NAME",
        //        Key: "slskd:instancename",
        //        Type: typeof(string),
        //        Default: Defaults.InstanceName,
        //        Description: "optional; a unique name for this instance"),
        //    new(
        //        ShortName: 'o',
        //        LongName: "downloads",
        //        EnvironmentVariable: "DOWNLOADS_DIR",
        //        Key: "slskd:directories:downloads",
        //        Type: typeof(string),
        //        Default: Defaults.Directories.Downloads,
        //        Description: "path where downloaded files are saved"),
        //    new(
        //        ShortName: 's',
        //        LongName: "shared",
        //        EnvironmentVariable: "SHARED_DIR",
        //        Key: "slskd:directories:shared",
        //        Type: typeof(string),
        //        Default: Defaults.Directories.Shared,
        //        Description: "path to shared files"),
        //    new(
        //        ShortName: 'l',
        //        LongName: "http-port",
        //        EnvironmentVariable: "HTTP_PORT",
        //        Key: "slskd:web:port",
        //        Type: typeof(int),
        //        Default: Defaults.Web.Port,
        //        Description: "HTTP listen port for web server"),
        //    new(
        //        ShortName: 'L',
        //        LongName: "https-port",
        //        EnvironmentVariable: "HTTPS_PORT",
        //        Key: "slskd:web:https:port",
        //        Type: typeof(int),
        //        Default: Defaults.Web.Https.Port,
        //        Description: "HTTPS listen port for web server"),
        //    new(
        //        ShortName: 'f',
        //        LongName: "force-https",
        //        EnvironmentVariable: "HTTPS_FORCE",
        //        Key: "slskd:web:https:force",
        //        Type: typeof(bool),
        //        Default: Defaults.Web.Https.Force,
        //        Description: "redirect HTTP to HTTPS"),
        //    new(
        //        ShortName: default,
        //        LongName: "https-cert-pfx",
        //        EnvironmentVariable: "HTTPS_CERT_PFX",
        //        Key: "slskd:web:https:certificate:pfx",
        //        Type: typeof(string),
        //        Default: Defaults.Web.Https.Certificate.Pfx,
        //        Description: "path to X509 certificate .pfx"),
        //    new(
        //        ShortName: default,
        //        LongName: "https-cert-password",
        //        EnvironmentVariable: "HTTPS_CERT_PASSWORD",
        //        Key: "slskd:web:https:certificate:password",
        //        Type: typeof(string),
        //        Default: Defaults.Web.Https.Certificate.Password,
        //        Description: "X509 certificate password"),
        //    new(
        //        ShortName: default,
        //        LongName: "url-base",
        //        EnvironmentVariable: "URL_BASE",
        //        Key: "slskd:web:urlbase",
        //        Type: typeof(string),
        //        Default: Defaults.Web.UrlBase,
        //        Description: "base url for web requests"),
        //    new(
        //        ShortName: default,
        //        LongName: "content-path",
        //        EnvironmentVariable: "CONTENT_PATH",
        //        Key: "slskd:web:contentpath",
        //        Type: typeof(string),
        //        Default: Defaults.Web.ContentPath,
        //        Description: "path to static web content"),
        //    new(
        //        ShortName: 'x',
        //        LongName: "no-auth",
        //        EnvironmentVariable: "NO_AUTH",
        //        Key: "slskd:web:authentication:disable",
        //        Type: typeof(bool),
        //        Default: Defaults.Web.Authentication.Disable,
        //        Description: "disable authentication for web requests"),
        //    new(
        //        ShortName: 'u',
        //        LongName: "username",
        //        EnvironmentVariable: "USERNAME",
        //        Key: "slskd:web:authentication:username",
        //        Type: typeof(string),
        //        Default: Defaults.Web.Authentication.Username,
        //        Description: "the username for web UI"),
        //    new(
        //        ShortName: 'p',
        //        LongName: "password",
        //        EnvironmentVariable: "PASSWORD",
        //        Key: "slskd:web:authentication:password",
        //        Type: typeof(string),
        //        Default: Defaults.Web.Authentication.Password,
        //        Description: "the password for web UI"),
        //    new(
        //        ShortName: default,
        //        LongName: "jwt-key",
        //        EnvironmentVariable: "JWT_KEY",
        //        Key: "slskd:web:authentication:jwt:key",
        //        Type: typeof(string),
        //        Default: Defaults.Web.Authentication.Jwt.Key,
        //        Description: "JWT signing key"),
        //    new(
        //        ShortName: default,
        //        LongName: "jwt-ttl",
        //        EnvironmentVariable: "JWT_TTL",
        //        Key: "slskd:web:authentication:jwt:ttl",
        //        Type: typeof(int),
        //        Default: Defaults.Web.Authentication.Jwt.Ttl,
        //        Description: "TTL for JWTs"),
        //    new(
        //        ShortName: default,
        //        LongName: "prometheus",
        //        EnvironmentVariable: "PROMETHEUS",
        //        Key: "slskd:feature:prometheus",
        //        Type: typeof(bool),
        //        Default: Defaults.Feature.Prometheus,
        //        Description: "enable collection and publishing of prometheus metrics"),
        //    new(
        //        ShortName: default,
        //        LongName: "swagger",
        //        EnvironmentVariable: "SWAGGER",
        //        Key: "slskd:feature:swagger",
        //        Type: typeof(bool),
        //        Default: Defaults.Feature.Swagger,
        //        Description: "enable swagger documentation and UI"),
        //    new(
        //        ShortName: default,
        //        LongName: "loki",
        //        EnvironmentVariable: "LOKI",
        //        Key: "slskd:logger:loki",
        //        Type: typeof(string),
        //        Default: Defaults.Logger.Loki,
        //        Description: "optional; url to a Grafana Loki instance to which to log"),
        //    new(
        //        ShortName: default,
        //        LongName: "slsk-username",
        //        EnvironmentVariable: "SLSK_USERNAME",
        //        Key: "slskd:soulseek:username",
        //        Type: typeof(string),
        //        Default: Defaults.Soulseek.Username,
        //        Description: "username for the Soulseek network"),
        //    new(
        //        ShortName: default,
        //        LongName: "slsk-password",
        //        EnvironmentVariable: "SLSK_PASSWORD",
        //        Key: "slskd:soulseek:password",
        //        Type: typeof(string),
        //        Default: Defaults.Soulseek.Password,
        //        Description: "password for the Soulseek network"),
        //    new(
        //        ShortName: default,
        //        LongName: "slsk-listen-port",
        //        EnvironmentVariable: "SLSK_LISTEN_PORT",
        //        Key: "slskd:soulseek:listenport",
        //        Type: typeof(int),
        //        Default: Defaults.Soulseek.ListenPort,
        //        Description: "port on which to listen for incoming connections"),
        //    new(
        //        ShortName: default,
        //        LongName: "slsk-diag-level",
        //        EnvironmentVariable: "SLSK_DIAG_LEVEL",
        //        Key: "slskd:soulseek:diagnosticlevel",
        //        Type: typeof(DiagnosticLevel),
        //        Default: Defaults.Soulseek.DiagnosticLevel,
        //        Description: "minimum diagnostic level (None, Warning, Info, Debug)"),
        //    new(
        //        ShortName: default,
        //        LongName: "slsk-no-dnet",
        //        EnvironmentVariable: "SLSK_NO_DNET",
        //        Key: "slskd:soulseek:distributednetwork:disabled",
        //        Type: typeof(bool),
        //        Default: Defaults.Soulseek.DistributedNetwork.Disabled,
        //        Description: "disable the distributed network"),
        //    new(
        //        ShortName: default,
        //        LongName: "slsk-dnet-children",
        //        EnvironmentVariable: "SLSK_DNET_CHILDREN",
        //        Key: "slskd:soulseek:distributednetwork:childlimit",
        //        Type: typeof(int),
        //        Default: Defaults.Soulseek.DistributedNetwork.ChildLimit,
        //        Description: "max number of distributed children"),
        //    new(
        //        ShortName: default,
        //        LongName: "slsk-connection-timeout",
        //        EnvironmentVariable: "SLSK_CONNECTION_TIMEOUT",
        //        Key: "slskd:soulseek:connection:timeout:connect",
        //        Type: typeof(int),
        //        Default: Defaults.Soulseek.Connection.Timeout.Connect,
        //        Description: "connection timeout, in miliseconds"),
        //    new(
        //        ShortName: default,
        //        LongName: "slsk-inactivity-timeout",
        //        EnvironmentVariable: "SLSK_INACTIVITY_TIMEOUT",
        //        Key: "slskd:soulseek:connection:timeout:inactivity",
        //        Type: typeof(int),
        //        Default: Defaults.Soulseek.Connection.Timeout.Inactivity,
        //        Description: "connection inactivity timeout, in miliseconds"),
        //    new(
        //        ShortName: default,
        //        LongName: "slsk-read-buffer",
        //        EnvironmentVariable: "SLSK_READ_BUFFER",
        //        Key: "slskd:soulseek:connection:buffer:read",
        //        Type: typeof(int),
        //        Default: Defaults.Soulseek.Connection.Buffer.Read,
        //        Description: "read buffer size for connections"),
        //    new(
        //        ShortName: default,
        //        LongName: "slsk-write-buffer",
        //        EnvironmentVariable: "SLSK_WRITE_BUFFER",
        //        Key: "slskd:soulseek:connection:buffer:write",
        //        Type: typeof(int),
        //        Default: Defaults.Soulseek.Connection.Buffer.Write,
        //        Description: "write buffer size for connections"),
        //};

        /// <summary>
        ///     Gets a value indicating whether the application should run in debug mode.
        /// </summary>
        [Argument('d', "debug")]
        [EnvironmentVariable("DEBUG")]
        [Description("run in debug mode")]
        public bool Debug { get; private set; } = Debugger.IsAttached;

        [Argument('e', "envars")]
        [Description("display environment variables")]
        public bool ShowEnvironmentVariables { get; private set; } = false;

        [Argument('h', "help")]
        [Description("display command line usage")]
        public bool ShowHelp { get; private set; } = false;

        [Argument('v', "version")]
        [Description("display version information")]
        public bool ShowVersion { get; private set; } = false;

        [Argument('c', "config")]
        [EnvironmentVariable("CONFIG")]
        [Description("path to configuration file")]
        public string ConfigurationFile { get; private set; } = Program.DefaultConfigurationFile;

        [Argument('g', "generate-cert")]
        [Description("generate X509 certificate and password for HTTPs")]
        public bool GenerateCertificate { get; private set; } = false;

        /// <summary>
        ///     Gets a value indicating whether the logo should be suppressed on startup.
        /// </summary>
        [Argument('n', "no-logo")]
        [EnvironmentVariable("NO_LOGO")]
        [Description("suppress logo on startup")]
        public bool NoLogo { get; private set; } = false;

        /// <summary>
        ///     Gets the unique name for this instance.
        /// </summary>
        [Argument('i', "instance-name")]
        [EnvironmentVariable("INSTANCE_NAME")]
        [Description("optional; a unique name for this instance")]
        public string InstanceName { get; private set; } = "default";

        /// <summary>
        ///     Gets directory options.
        /// </summary>
        [Validate]
        public DirectoriesOptions Directories { get; private set; } = new DirectoriesOptions();

        /// <summary>
        ///     Gets options for the web UI.
        /// </summary>
        [Validate]
        public WebOptions Web { get; private set; } = new WebOptions();

        /// <summary>
        ///     Gets logger options.
        /// </summary>
        [Validate]
        public LoggerOptions Logger { get; private set; } = new LoggerOptions();

        /// <summary>
        ///     Gets feature options.
        /// </summary>
        [Validate]
        public FeatureOptions Feature { get; private set; } = new FeatureOptions();

        /// <summary>
        ///     Gets options for the Soulseek client.
        /// </summary>
        [Validate]
        public SoulseekOptions Soulseek { get; private set; } = new SoulseekOptions();

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

        /// <summary>
        ///     Directory options.
        /// </summary>
        public class DirectoriesOptions
        {
            /// <summary>
            ///     Gets the path to shared files.
            /// </summary>
            [Argument('s', "shared")]
            [EnvironmentVariable("SHARED_DIR")]
            [Description("path to shared files")]
            [Required]
            public string Shared { get; private set; } = null;

            /// <summary>
            ///     Gets the path where downloaded files are saved.
            /// </summary>
            [Argument('o', "downloads")]
            [EnvironmentVariable("DOWNLOADS_DIR")]
            [Description("path where downloaded files are saved")]
            [Required]
            public string Downloads { get; private set; } = null;
        }

        /// <summary>
        ///     Feature options.
        /// </summary>
        public class FeatureOptions
        {
            /// <summary>
            ///     Gets a value indicating whether prometheus metrics should be collected and published.
            /// </summary>
            [Argument(default, "prometheus")]
            [EnvironmentVariable("PROMETHEUS")]
            [Description("enable collection and publishing of prometheus metrics")]
            public bool Prometheus { get; private set; } = false;

            /// <summary>
            ///     Gets a value indicating whether swagger documentation and UI should be enabled.
            /// </summary>
            [Argument(default, "swagger")]
            [EnvironmentVariable("SWAGGER")]
            [Description("enable swagger documentation and UI")]
            public bool Swagger { get; private set; } = false;
        }

        /// <summary>
        ///     Logger options.
        /// </summary>
        public class LoggerOptions
        {
            /// <summary>
            ///     Gets the URL to a Grafana Loki instance to which to log.
            /// </summary>
            [Argument(default, "loki")]
            [EnvironmentVariable("LOKI")]
            [Description("optional; url to a Grafana Loki instance to which to log")]
            public string Loki { get; private set; } = null;
        }

        public class SoulseekOptions
        {
            [Argument(default, "slsk-password")]
            [EnvironmentVariable("SLSK_PASSWORD")]
            [Description("password for the Soulseek network")]
            [Required]
            public string Password { get; private set; } = null;

            [Argument(default, "slsk-username")]
            [EnvironmentVariable("SLSK_USERNAME")]
            [Description("username for the Soulseek network")]
            [Required]
            public string Username { get; private set; } = null;

            [Argument(default, "slsk-listen-port")]
            [EnvironmentVariable("SLSK_LISTEN_PORT")]
            [Range(1024, 65535)]
            public int ListenPort { get; private set; } = 50000;

            [Argument(default, "slsk-diag-level")]
            [EnvironmentVariable("SLSK_DIAG_LEVEL")]
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
                    [Argument(default, "slsk-read-buffer")]
                    [EnvironmentVariable("SLSK_READ_BUFFER")]
                    [Range(1024, int.MaxValue)]
                    public int Read { get; private set; } = 16384;

                    [Argument(default, "slsk-write-buffer")]
                    [EnvironmentVariable("SLSK_WRITE_BUFFER")]
                    [Range(1024, int.MaxValue)]
                    public int Write { get; private set; } = 16384;
                }

                public class TimeoutOptions
                {
                    [Argument(default, "slsk-connection-timeout")]
                    [EnvironmentVariable("SLSK_CONNECTION_TIMEOUT")]
                    [Range(1000, int.MaxValue)]
                    public int Connect { get; private set; } = 5000;

                    [Argument(default, "slsk-inactivity-timeout")]
                    [EnvironmentVariable("SLSK_INACTIVITY_TIMEOUT")]
                    [Range(1000, int.MaxValue)]
                    public int Inactivity { get; private set; } = 15000;
                }
            }

            public class DistributedNetworkOptions
            {
                [Argument(default, "slsk-no-dnet")]
                [EnvironmentVariable("SLSK_NO_DNET")]
                public bool Disabled { get; private set; } = false;

                [Argument(default, "slsk-dnet-children")]
                [EnvironmentVariable("SLSK_DNET_CHILDREN")]
                [Range(1, int.MaxValue)]
                public int ChildLimit { get; private set; } = 25;
            }
        }

        public class WebOptions
        {
            [Argument('l', "http-port")]
            [EnvironmentVariable("HTTP_PORT")]
            [Option('l', "http-port", "HTTP_PORT", "HTTP listen port for web UI")]
            [Range(1, 65535)]
            public int Port { get; private set; } = 5000;

            [Validate]
            public HttpsOptions Https { get; private set; } = new HttpsOptions();

            [Argument(default, "url-base")]
            [EnvironmentVariable("URL_BASE")]
            [Option(default, "url-base", "URL_BASE", "base url for web requests")]
            public string UrlBase { get; private set; } = "/";

            [Argument(default, "content-path")]
            [EnvironmentVariable("CONTENT_PATH")]
            [Option(default, "content-path", "CONTENT_PATH", "path to static web content")]
            [Required]
            public string ContentPath { get; private set; } = "wwwroot";

            [Validate]
            public AuthenticationOptions Authentication { get; private set; } = new AuthenticationOptions();

            public class AuthenticationOptions
            {
                [Argument('x', "no-auth")]
                [EnvironmentVariable("NO_AUTH")]
                [Option('x', "no-auth", "NO_AUTH", "disable authentication for web requests")]
                public bool Disable { get; private set; } = false;

                [Argument('u', "username")]
                [EnvironmentVariable("USERNAME")]
                [Option('u', "username", "USERNAME", "username for web UI")]
                [Required]
                public string Username { get; private set; } = "slskd";

                [Argument('p', "password")]
                [EnvironmentVariable("PASSWORD")]
                [Option('p', "password", "PASSWORD", "password for web UI")]
                [Required]
                public string Password { get; private set; } = "slskd";

                [Validate]
                public JwtOptions Jwt { get; private set; } = new JwtOptions();

                public class JwtOptions
                {
                    [Argument(default, "jwt-key")]
                    [EnvironmentVariable("JWT_KEY")]
                    [Option(default, "jwt-key", "JWT_KEY", "JWT signing key")]
                    [Required]
                    public string Key { get; private set; } = Guid.NewGuid().ToString();

                    [Argument(default, "jwt-ttl")]
                    [EnvironmentVariable("JWT_TTL")]
                    [Option(default, "jwt-ttl", "JWT_TTL", "TTL for JWTs")]
                    [Range(3600, int.MaxValue)]
                    public int Ttl { get; private set; } = 604800000;
                }
            }

            public class HttpsOptions
            {
                [Argument('L', "https-port")]
                [EnvironmentVariable("HTTPS_PORT")]
                [Option('L', "https-port", "HTTPS_PORT", "HTTPS listen port for web UI")]
                [Range(1, 65535)]
                public int Port { get; private set; } = 5001;

                [Argument('f', "force-https")]
                [EnvironmentVariable("HTTPS_FORCE")]
                [Option('f', "force-https", "HTTPS_FORCE", "redirect HTTP to HTTPS")]
                public bool Force { get; private set; } = false;

                [Validate]
                public CertificateOptions Certificate { get; private set; } = new CertificateOptions();

                public class CertificateOptions
                {
                    [Argument(default, "https-cert-pfx")]
                    [EnvironmentVariable("HTTPS_CERT_PFX")]
                    [Option(default, "https-cert-pfx", "HTTPS_CERT_PFX", "path to X509 certificate .pfx")]
                    [FileExists]
                    public string Pfx { get; private set; }

                    [Argument(default, "https-cert-password")]
                    [EnvironmentVariable("HTTPS_CERT_PASSWORD")]
                    [Option(default, "https-cert-password", "HTTPS_CERT_PASSWORD", "X509 certificate password")]
                    public string Password { get; private set; }
                }
            }
        }
    }
}