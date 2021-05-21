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
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Diagnostics;
    using slskd.Validation;
    using Soulseek.Diagnostics;
    using Utility.CommandLine;
    using Utility.EnvironmentVariables;

    /// <summary>
    ///     Application options.
    /// </summary>
    public class Options
    {
        /// <summary>
        ///     Gets a value indicating whether to display the application version.
        /// </summary>
        [Argument('v', "version")]
        [Description("display version information")]
        public bool ShowVersion { get; private set; } = false;

        /// <summary>
        ///     Gets a value indicating whether to display a list of command line arguments.
        /// </summary>
        [Argument('h', "help")]
        [Description("display command line usage")]
        public bool ShowHelp { get; private set; } = false;

        /// <summary>
        ///     Gets a value indicating whether to display a list of configuration environment variables.
        /// </summary>
        [Argument('e', "envars")]
        [Description("display environment variables")]
        public bool ShowEnvironmentVariables { get; private set; } = false;

        /// <summary>
        ///     Gets a value indicating whether to generate an X509 certificate and password.
        /// </summary>
        [Argument('g', "generate-cert")]
        [Description("generate X509 certificate and password for HTTPs")]
        public bool GenerateCertificate { get; private set; } = false;

        /// <summary>
        ///     Gets the path to the application configuration file.
        /// </summary>
        [Argument('c', "config")]
        [EnvironmentVariable("CONFIG")]
        [Description("path to configuration file")]
        public string ConfigurationFile { get; private set; } = Program.DefaultConfigurationFile;

        /// <summary>
        ///     Gets a value indicating whether the application should run in debug mode.
        /// </summary>
        [Argument('d', "debug")]
        [EnvironmentVariable("DEBUG")]
        [Description("run in debug mode")]
        public bool Debug { get; private set; } = Debugger.IsAttached;

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

        /// <summary>
        ///     Directory options.
        /// </summary>
        public class DirectoriesOptions
        {
            /// <summary>
            ///     Gets the path where application data is saved.
            /// </summary>
            [Argument(default, "app")]
            [EnvironmentVariable("APP_DIR")]
            [Description("path where application data is saved")]
            [DirectoryExists]
            public string App { get; private set; } = Program.DefaultAppDirectory;

            /// <summary>
            ///     Gets the path where incomplete downloads are saved.
            /// </summary>
            [Argument(default, "incomplete")]
            [EnvironmentVariable("INCOMPLETE_DIR")]
            [Description("path where incomplete downloads are saved")]
            [DirectoryExists]
            public string Incomplete { get; private set; } = Program.DefaultIncompleteDirectory;

            /// <summary>
            ///     Gets the path where downloaded files are saved.
            /// </summary>
            [Argument('o', "downloads")]
            [EnvironmentVariable("DOWNLOADS_DIR")]
            [Description("path where downloaded files are saved")]
            [DirectoryExists]
            public string Downloads { get; private set; } = Program.DefaultDownloadsDirectory;

            /// <summary>
            ///     Gets the path to shared files.
            /// </summary>
            [Argument('s', "shared")]
            [EnvironmentVariable("SHARED_DIR")]
            [Description("path to shared files")]
            [DirectoryExists]
            public string Shared { get; private set; } = Program.DefaultSharedDirectory;
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

        /// <summary>
        ///     Soulseek client options.
        /// </summary>
        public class SoulseekOptions
        {
            /// <summary>
            ///     Gets the password for the Soulseek network.
            /// </summary>
            [Argument(default, "slsk-password")]
            [EnvironmentVariable("SLSK_PASSWORD")]
            [Description("password for the Soulseek network")]
            [Required]
            public string Password { get; private set; } = null;

            /// <summary>
            ///     Gets the username for the Soulseek network.
            /// </summary>
            [Argument(default, "slsk-username")]
            [EnvironmentVariable("SLSK_USERNAME")]
            [Description("username for the Soulseek network")]
            [Required]
            public string Username { get; private set; } = null;

            /// <summary>
            ///     Gets the port on which to listen for incoming connections.
            /// </summary>
            [Argument(default, "slsk-listen-port")]
            [EnvironmentVariable("SLSK_LISTEN_PORT")]
            [Description("port on which to listen for incoming connections")]
            [Range(1024, 65535)]
            public int ListenPort { get; private set; } = 50000;

            /// <summary>
            ///     Gets the minimum diagnostic level.
            /// </summary>
            [Argument(default, "slsk-diag-level")]
            [EnvironmentVariable("SLSK_DIAG_LEVEL")]
            [Description("minimum diagnostic level (None, Warning, Info, Debug)")]
            public DiagnosticLevel DiagnosticLevel { get; private set; } = DiagnosticLevel.Info;

            /// <summary>
            ///     Gets options for the distributed network.
            /// </summary>
            [Validate]
            public DistributedNetworkOptions DistributedNetwork { get; private set; } = new DistributedNetworkOptions();

            /// <summary>
            ///     Gets connection options.
            /// </summary>
            [Validate]
            public ConnectionOptions Connection { get; private set; } = new ConnectionOptions();

            /// <summary>
            ///     Connection options.
            /// </summary>
            public class ConnectionOptions
            {
                /// <summary>
                ///     Gets connection timeout options.
                /// </summary>
                [Validate]
                public TimeoutOptions Timeout { get; private set; } = new TimeoutOptions();

                /// <summary>
                ///     Gets connection buffer options.
                /// </summary>
                [Validate]
                public BufferOptions Buffer { get; private set; } = new BufferOptions();

                /// <summary>
                ///     Gets connection proxy options.
                /// </summary>
                [Validate]
                public ProxyOptions Proxy { get; private set; } = new ProxyOptions();

                /// <summary>
                ///     Connection buffer options.
                /// </summary>
                public class BufferOptions
                {
                    /// <summary>
                    ///     Gets the connection read buffer size.
                    /// </summary>
                    [Argument(default, "slsk-read-buffer")]
                    [EnvironmentVariable("SLSK_READ_BUFFER")]
                    [Description("read buffer size for connections")]
                    [Range(1024, int.MaxValue)]
                    public int Read { get; private set; } = 16384;

                    /// <summary>
                    ///     Gets the connection write buffer size.
                    /// </summary>
                    [Argument(default, "slsk-write-buffer")]
                    [EnvironmentVariable("SLSK_WRITE_BUFFER")]
                    [Description("write buffer size for connections")]
                    [Range(1024, int.MaxValue)]
                    public int Write { get; private set; } = 16384;
                }

                /// <summary>
                ///     Connection timeout options.
                /// </summary>
                public class TimeoutOptions
                {
                    /// <summary>
                    ///     Gets the connection timeout value, in milliseconds.
                    /// </summary>
                    [Argument(default, "slsk-connection-timeout")]
                    [EnvironmentVariable("SLSK_CONNECTION_TIMEOUT")]
                    [Description("connection timeout, in milliseconds")]
                    [Range(1000, int.MaxValue)]
                    public int Connect { get; private set; } = 10000;

                    /// <summary>
                    ///     Gets the connection inactivity timeout, in milliseconds.
                    /// </summary>
                    [Argument(default, "slsk-inactivity-timeout")]
                    [EnvironmentVariable("SLSK_INACTIVITY_TIMEOUT")]
                    [Description("connection inactivity timeout, in milliseconds")]
                    [Range(1000, int.MaxValue)]
                    public int Inactivity { get; private set; } = 15000;
                }

                /// <summary>
                ///     Connection proxy options.
                /// </summary>
                public class ProxyOptions
                {
                    /// <summary>
                    ///     Gets the proxy address.
                    /// </summary>
                    [Argument(default, "slsk-proxy-address")]
                    [EnvironmentVariable("SLSK_PROXY_ADDRESS")]
                    [Description("connection proxy address")]
                    [StringLength(255, MinimumLength = 1)]
                    public string Address { get; private set; }

                    /// <summary>
                    ///     Gets the proxy port.
                    /// </summary>
                    [Argument(default, "slsk-proxy-port")]
                    [EnvironmentVariable("SLSK_PROXY_PORT")]
                    [Description("connection proxy port")]
                    [Range(1, 65535)]
                    public int? Port { get; private set; }

                    /// <summary>
                    ///     Gets the proxy username, if applicable.
                    /// </summary>
                    [Argument(default, "slsk-proxy-username")]
                    [EnvironmentVariable("SLSK_PROXY_USERNAME")]
                    [Description("connection proxy username")]
                    [StringLength(255, MinimumLength = 1)]
                    public string Username { get; private set; }

                    /// <summary>
                    ///     Gets the proxy password, if applicable.
                    /// </summary>
                    [Argument(default, "slsk-proxy-password")]
                    [EnvironmentVariable("SLSK_PROXY_PASSWORD")]
                    [Description("connection proxy password")]
                    [StringLength(255, MinimumLength = 1)]
                    public string Password { get; private set; }
                }
            }

            /// <summary>
            ///     Distributed network options.
            /// </summary>
            public class DistributedNetworkOptions
            {
                /// <summary>
                ///     Gets a value indicating whether the distributed network should be disabled.
                /// </summary>
                [Argument(default, "slsk-no-dnet")]
                [EnvironmentVariable("SLSK_NO_DNET")]
                [Description("disable the distributed network")]
                public bool Disabled { get; private set; } = false;

                /// <summary>
                ///     Gets the distributed child connection limit.
                /// </summary>
                [Argument(default, "slsk-dnet-children")]
                [EnvironmentVariable("SLSK_DNET_CHILDREN")]
                [Description("max number of distributed children")]
                [Range(1, int.MaxValue)]
                public int ChildLimit { get; private set; } = 25;
            }
        }

        /// <summary>
        ///     Web server options.
        /// </summary>
        public class WebOptions
        {
            /// <summary>
            ///     Gets the HTTP listen port.
            /// </summary>
            [Argument('l', "http-port")]
            [EnvironmentVariable("HTTP_PORT")]
            [Description("HTTP listen port for web UI")]
            [Range(1, 65535)]
            public int Port { get; private set; } = 5000;

            /// <summary>
            ///     Gets HTTPS options.
            /// </summary>
            [Validate]
            public HttpsOptions Https { get; private set; } = new HttpsOptions();

            /// <summary>
            ///     Gets the base url for web requests.
            /// </summary>
            [Argument(default, "url-base")]
            [EnvironmentVariable("URL_BASE")]
            [Description("base url for web requests")]
            public string UrlBase { get; private set; } = "/";

            /// <summary>
            ///     Gets the path to static web content.
            /// </summary>
            [Argument(default, "content-path")]
            [EnvironmentVariable("CONTENT_PATH")]
            [Description("path to static web content")]
            [StringLength(255, MinimumLength = 1)]
            public string ContentPath { get; private set; } = "wwwroot";

            /// <summary>
            ///     Gets authentication options.
            /// </summary>
            [Validate]
            public AuthenticationOptions Authentication { get; private set; } = new AuthenticationOptions();

            /// <summary>
            ///     Authentication options.
            /// </summary>
            public class AuthenticationOptions
            {
                /// <summary>
                ///     Gets a value indicating whether authentication should be disabled.
                /// </summary>
                [Argument('x', "no-auth")]
                [EnvironmentVariable("NO_AUTH")]
                [Description("disable authentication for web requests")]
                public bool Disable { get; private set; } = false;

                /// <summary>
                ///     Gets the username for the web UI.
                /// </summary>
                [Argument('u', "username")]
                [EnvironmentVariable("USERNAME")]
                [Description("username for web UI")]
                [StringLength(255, MinimumLength = 1)]
                public string Username { get; private set; } = Program.AppName;

                /// <summary>
                ///     Gets the password for the web UI.
                /// </summary>
                [Argument('p', "password")]
                [EnvironmentVariable("PASSWORD")]
                [Description("password for web UI")]
                [StringLength(255, MinimumLength = 1)]
                public string Password { get; private set; } = Program.AppName;

                /// <summary>
                ///     Gets JWT options.
                /// </summary>
                [Validate]
                public JwtOptions Jwt { get; private set; } = new JwtOptions();

                /// <summary>
                ///     JWT options.
                /// </summary>
                public class JwtOptions
                {
                    /// <summary>
                    ///     Gets the key with which to sign JWTs.
                    /// </summary>
                    [Argument(default, "jwt-key")]
                    [EnvironmentVariable("JWT_KEY")]
                    [Description("JWT signing key")]
                    [StringLength(255, MinimumLength = 1)]
                    public string Key { get; private set; } = Guid.NewGuid().ToString();

                    /// <summary>
                    ///     Gets the TTL for JWTs.
                    /// </summary>
                    [Argument(default, "jwt-ttl")]
                    [EnvironmentVariable("JWT_TTL")]
                    [Description("TTL for JWTs")]
                    [Range(3600, int.MaxValue)]
                    public int Ttl { get; private set; } = 604800000;
                }
            }

            /// <summary>
            ///     HTTPS options.
            /// </summary>
            public class HttpsOptions
            {
                /// <summary>
                ///     Gets the HTTPS listen port.
                /// </summary>
                [Argument('L', "https-port")]
                [EnvironmentVariable("HTTPS_PORT")]
                [Description("HTTPS listen port for web UI")]
                [Range(1, 65535)]
                public int Port { get; private set; } = 5001;

                /// <summary>
                ///     Gets a value indicating whether HTTP requests should be redirected to HTTPS.
                /// </summary>
                [Argument('f', "force-https")]
                [EnvironmentVariable("HTTPS_FORCE")]
                [Description("redirect HTTP to HTTPS")]
                public bool Force { get; private set; } = false;

                /// <summary>
                ///     Gets certificate options.
                /// </summary>
                [Validate]
                public CertificateOptions Certificate { get; private set; } = new CertificateOptions();

                /// <summary>
                ///     Certificate options.
                /// </summary>
                public class CertificateOptions
                {
                    /// <summary>
                    ///     Gets the path to the the X509 certificate .pfx file.
                    /// </summary>
                    [Argument(default, "https-cert-pfx")]
                    [EnvironmentVariable("HTTPS_CERT_PFX")]
                    [Description("path to X509 certificate .pfx")]
                    [FileExists]
                    public string Pfx { get; private set; }

                    /// <summary>
                    ///     Gets the password for the X509 certificate.
                    /// </summary>
                    [Argument(default, "https-cert-password")]
                    [EnvironmentVariable("HTTPS_CERT_PASSWORD")]
                    [Description("X509 certificate password")]
                    public string Password { get; private set; }
                }
            }
        }
    }
}