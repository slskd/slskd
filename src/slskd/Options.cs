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

using Microsoft.Extensions.Options;

namespace slskd
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using FluentFTP;
    using slskd.Configuration;
    using slskd.Validation;
    using Soulseek.Diagnostics;
    using Utility.CommandLine;
    using Utility.EnvironmentVariables;

    /// <summary>
    ///     Disambiguates options derived at startup from options that may update at run time.
    /// </summary>
    /// <remarks>
    ///     This class is added directly to dependency injection, but <see cref="Options"/> is not, so consumers must inject <see cref="OptionsAtStartup"/>
    ///     instead of <see cref="Options"/> to make it clear that these options will not change.  Options that may change should be accessed by injecting
    ///     <see cref="IOptionsMonitor{T}"/> or <see cref="IOptionsSnapshot{T}"/>, depending on the lifetime of the component.
    /// </remarks>
    public class OptionsAtStartup : Options
    {
    }

    /// <summary>
    ///     Application options.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This class contains all application options, which may be sourced from (in order of precedence):
    ///         <list type="bullet">
    ///             <item>
    ///                 <term>Defaults</term>
    ///                 <description>Default values, statically defined in this class.</description>
    ///             </item>
    ///             <item>
    ///                 <term>Environment Variables</term>
    ///                 <description>Environment variables set at either the system or user scope.</description>
    ///             </item>
    ///             <item>
    ///                 <term>YAML Configuration File</term>
    ///                 <description>A YAML file containing a mapping of this class.</description>
    ///             </item>
    ///             <item>
    ///                 <term>Command Line</term>
    ///                 <description>Options provided via the command line when starting the application.</description>
    ///             </item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         Only the YAML configuration source can change at runtime and consumers of this class must be aware of this, either injecting
    ///         <see cref="IOptionsMonitor{T}"/> in components with a singleton lifetime, or <see cref="IOptionsSnapshot{T}"/> for transient or scoped lifetimes.
    ///     </para>
    ///     <para>
    ///         To obtain the Options specified at startup (discarding any updates that may have been applied since), inject <see cref="OptionsAtStartup"/>.
    ///     </para>
    ///     <para>
    ///         Options specified via the command line can not be overwritten by changes to the YAML file.  This is by design due to the immutable
    ///         nature of the command line string after the application is started.
    ///     </para>
    /// </remarks>
    public class Options
    {
        /// <summary>
        ///     Gets a value indicating whether to display the application version.
        /// </summary>
        [Argument('v', "version")]
        [Description("display version information")]
        [Obsolete("Used only for documentation; see Program for actual implementation")]
        public bool ShowVersion { get; private set; } = false;

        /// <summary>
        ///     Gets a value indicating whether to display a list of command line arguments.
        /// </summary>
        [Argument('h', "help")]
        [Description("display command line usage")]
        [Obsolete("Used only for documentation; see Program for actual implementation")]
        public bool ShowHelp { get; private set; } = false;

        /// <summary>
        ///     Gets a value indicating whether to display a list of configuration environment variables.
        /// </summary>
        [Argument('e', "envars")]
        [Description("display environment variables")]
        [Obsolete("Used only for documentation; see Program for actual implementation")]
        public bool ShowEnvironmentVariables { get; private set; } = false;

        /// <summary>
        ///     Gets a value indicating whether to generate an X509 certificate and password.
        /// </summary>
        [Argument('g', "generate-cert")]
        [Description("generate X509 certificate and password for HTTPs")]
        [Obsolete("Used only for documentation; see Program for actual implementation")]
        public bool GenerateCertificate { get; private set; } = false;

        /// <summary>
        ///     Gets the path where application data is saved.
        /// </summary>
        [Argument(default, "appdir")]
        [EnvironmentVariable("APP_DIR")]
        [Description("path where application data is saved")]
        [Obsolete("Used only for documentation; see Program for actual implementation")]
        public string AppDirectory { get; private set; } = Program.DefaultAppDirectory;


        /// <summary>
        ///     Gets a value indicating whether the application should run in debug mode.
        /// </summary>
        [Argument('d', "debug")]
        [EnvironmentVariable("DEBUG")]
        [Description("run in debug mode")]
        [RequiresRestart]
        public bool Debug { get; private set; } = Debugger.IsAttached;

        /// <summary>
        ///     Gets a value indicating whether the logo should be suppressed on startup.
        /// </summary>
        [Argument('n', "no-logo")]
        [EnvironmentVariable("NO_LOGO")]
        [Description("suppress logo on startup")]
        [RequiresRestart]
        public bool NoLogo { get; private set; } = false;

        /// <summary>
        ///     Gets a value indicating whether the application should quit after initialization.
        /// </summary>
        [Argument('x', "no-start")]
        [EnvironmentVariable("NO_START")]
        [Description("quit the application after initialization")]
        [RequiresRestart]
        public bool NoStart { get; private set; } = false;

        /// <summary>
        ///     Gets a value indicating whether the application should connect to the Soulseek network on startup.
        /// </summary>
        [Argument(default, "no-connect")]
        [EnvironmentVariable("NO_CONNECT")]
        [Description("do not connect to the Soulseek network on startup")]
        [RequiresRestart]
        public bool NoConnect { get; private set; } = false;

        /// <summary>
        ///     Gets a value indicating whether the application should scan shared directories on startup.
        /// </summary>
        [Argument(default, "no-share-scan")]
        [EnvironmentVariable("NO_SHARE_SCAN")]
        [Description("do not scan shares on startup")]
        [RequiresRestart]
        public bool NoShareScan { get; private set; } = false;

        /// <summary>
        ///     Gets a value indicating whether the application should check for a newer version on startup.
        /// </summary>
        [Argument(default, "no-version-check")]
        [EnvironmentVariable("NO_VERSION_CHECK")]
        [Description("do not check for newer version at startup")]
        [RequiresRestart]
        public bool NoVersionCheck { get; private set; } = false;

        /// <summary>
        ///     Gets the unique name for this instance.
        /// </summary>
        [Argument('i', "instance-name")]
        [EnvironmentVariable("INSTANCE_NAME")]
        [Description("optional; a unique name for this instance")]
        [RequiresRestart]
        public string InstanceName { get; private set; } = "default";

        /// <summary>
        ///     Gets directory options.
        /// </summary>
        [Validate]
        [RequiresRestart]
        public DirectoriesOptions Directories { get; private set; } = new DirectoriesOptions();

        /// <summary>
        ///     Gets filter options.
        /// </summary>
        [Validate]
        public FiltersOptions Filters { get; private set; } = new FiltersOptions();

        /// <summary>
        ///     Gets a list of rooms to automatically join upon connection.
        /// </summary>
        [Argument(default, "rooms")]
        [EnvironmentVariable("ROOMS")]
        [Description("a list of rooms to automatically join")]
        public string[] Rooms { get; private set; } = Array.Empty<string>();

        /// <summary>
        ///     Gets options for the web UI.
        /// </summary>
        [Validate]
        public WebOptions Web { get; private set; } = new WebOptions();

        /// <summary>
        ///     Gets logger options.
        /// </summary>
        [Validate]
        [RequiresRestart]
        public LoggerOptions Logger { get; private set; } = new LoggerOptions();

        /// <summary>
        ///     Gets feature options.
        /// </summary>
        [Validate]
        [RequiresRestart]
        public FeatureOptions Feature { get; private set; } = new FeatureOptions();

        /// <summary>
        ///     Gets options for the Soulseek client.
        /// </summary>
        [Validate]
        public SoulseekOptions Soulseek { get; private set; } = new SoulseekOptions();

        /// <summary>
        ///     Gets options for external integrations.
        /// </summary>
        [Validate]
        public IntegrationOptions Integration { get; private set; } = new IntegrationOptions();

        /// <summary>
        ///     Directory options.
        /// </summary>
        public class DirectoriesOptions : IValidatableObject
        {
            /// <summary>
            ///     Gets the path where incomplete downloads are saved.
            /// </summary>
            [Argument(default, "incomplete")]
            [EnvironmentVariable("INCOMPLETE_DIR")]
            [Description("path where incomplete downloads are saved")]
            [DirectoryExists]
            [RequiresRestart]
            public string Incomplete { get; private set; } = Program.DefaultIncompleteDirectory;

            /// <summary>
            ///     Gets the path where downloaded files are saved.
            /// </summary>
            [Argument('o', "downloads")]
            [EnvironmentVariable("DOWNLOADS_DIR")]
            [Description("path where downloaded files are saved")]
            [DirectoryExists]
            [RequiresRestart]
            public string Downloads { get; private set; } = Program.DefaultDownloadsDirectory;

            /// <summary>
            ///     Gets the list of paths to shared files.
            /// </summary>
            [Argument('s', "shared")]
            [EnvironmentVariable("SHARED_DIR")]
            [Description("path to shared files")]
            public string[] Shared { get; private set; } = Array.Empty<string>();

            /// <summary>
            ///     Extended validation.
            /// </summary>
            /// <param name="validationContext"></param>
            /// <returns></returns>
            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                var results = new List<ValidationResult>();

                (string Raw, string Mask, string Alias, string Path) Digest(string share)
                {
                    var matches = Regex.Matches(share, @"^(-?)\[(.*)\](.*)$");

                    if (matches.Any())
                    {
                        return (share, Compute.MaskHash(Directory.GetParent(matches[0].Groups[3].Value)?.FullName ?? share), matches[0].Groups[2].Value, matches[0].Groups[3].Value);
                    }

                    return (share, Compute.MaskHash(Directory.GetParent(share)?.FullName ?? share), share.Split(new[] { '/', '\\' }).Last(), share);
                }

                bool IsRoot((string Raw, string Mask, string Alias, string Path) share) => share.Path == "/" || share.Path == "\\" || Path.GetPathRoot(share.Path) == share.Path;

                // starts with '/', 'X:\', or '\\'
                bool IsAbsolutePath(string share) => Regex.IsMatch(share.ToLocalOSPath(), @"^(\[.*\])?(\/|[a-zA-Z]:\\|\\\\).*$");

                var relativePaths = Shared.Where(share => !IsAbsolutePath(share));
                foreach (var relativePath in relativePaths)
                {
                    results.Add(new ValidationResult($"Share {relativePath} contains a relative path; only absolute paths are supported."));
                }

                var digestedShared = Shared
                    .Select(share => Digest(share.TrimEnd('/', '\\')))
                    .ToHashSet();

                var roots = digestedShared.Where(share => IsRoot(share));
                foreach (var root in roots)
                {
                    results.Add(new ValidationResult($"Share {root.Raw} is a root path, which is not supported."));
                }

                digestedShared = digestedShared.Where(share => !IsRoot(share)).ToHashSet();

                var overlapping = digestedShared.GroupBy(share => share.Mask + share.Alias).Where(group => group.Count() > 1);
                foreach (var overlap in overlapping)
                {
                    results.Add(new ValidationResult($"Shares {string.Join(", ", overlap.Select(s => $"'{s.Raw}'"))} overlap"));
                }

                var duplicates = digestedShared.GroupBy(share => share.Path).Where(group => group.Count() > 1);
                foreach (var dupe in duplicates)
                {
                    results.Add(new ValidationResult($"Shares {string.Join(", ", dupe.Select(s => $"'{s.Raw}'"))} alias the same path"));
                }

                foreach (var share in digestedShared.Where(s => s.Alias != null))
                {
                    if (string.IsNullOrWhiteSpace(share.Alias))
                    {
                        results.Add(new ValidationResult($"Share '{share.Raw}' is invalid; alias may not be null, empty or consist of only whitespace"));
                    }
                    else if (share.Alias.Contains('\\') || share.Alias.Contains('/'))
                    {
                        results.Add(new ValidationResult($"Share '{share.Raw}' is invalid; aliases may not contain path separators '/' or '\\'"));
                    }
                }

                return results;
            }
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
            [RequiresRestart]
            public bool Prometheus { get; private set; } = false;

            /// <summary>
            ///     Gets a value indicating whether swagger documentation and UI should be enabled.
            /// </summary>
            [Argument(default, "swagger")]
            [EnvironmentVariable("SWAGGER")]
            [Description("enable swagger documentation and UI")]
            [RequiresRestart]
            public bool Swagger { get; private set; } = false;
        }

        /// <summary>
        ///     Filter options.
        /// </summary>
        public class FiltersOptions : IValidatableObject
        {
            /// <summary>
            ///     Gets the list of shared file filters.
            /// </summary>
            [Argument(default, "share-filter")]
            [EnvironmentVariable("SHARE_FILTER")]
            [Description("regular expressions to filter files from shares")]
            public string[] Share { get; private set; } = Array.Empty<string>();

            /// <summary>
            ///     Extended validation.
            /// </summary>
            /// <param name="validationContext"></param>
            /// <returns></returns>
            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                var results = new List<ValidationResult>();

                foreach (var filter in Share)
                {
                    if (!filter.IsValidRegex())
                    {
                        results.Add(new ValidationResult($"Share filter '{filter}' is not a valid regular expression"));
                    }
                }

                return results;
            }
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
            [RequiresRestart]
            public string Loki { get; private set; } = null;
        }

        /// <summary>
        ///     Soulseek client options.
        /// </summary>
        public class SoulseekOptions
        {
            /// <summary>
            ///     Gets the username for the Soulseek network.
            /// </summary>
            [Argument(default, "slsk-username")]
            [EnvironmentVariable("SLSK_USERNAME")]
            [Description("username for the Soulseek network")]
            [RequiresReconnect]
            public string Username { get; private set; } = null;

            /// <summary>
            ///     Gets the password for the Soulseek network.
            /// </summary>
            [Argument(default, "slsk-password")]
            [EnvironmentVariable("SLSK_PASSWORD")]
            [Description("password for the Soulseek network")]
            [RequiresReconnect]
            [JsonIgnore]
            public string Password { get; private set; } = null;

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
            [RequiresRestart]
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
                public class ProxyOptions : IValidatableObject
                {
                    /// <summary>
                    ///     Gets a value indicating whether the proxy is enabled.
                    /// </summary>
                    [Argument(default, "slsk-proxy")]
                    [EnvironmentVariable("SLSK_PROXY_ENABLED")]
                    [Description("enable connection proxy")]
                    public bool Enabled { get; private set; } = false;

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
                    [JsonIgnore]
                    public string Password { get; private set; }

                    /// <summary>
                    ///     Extended validation.
                    /// </summary>
                    /// <param name="validationContext"></param>
                    /// <returns></returns>
                    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
                    {
                        var results = new List<ValidationResult>();

                        if (Enabled && string.IsNullOrWhiteSpace(Address))
                        {
                            results.Add(new ValidationResult($"The Enabled field is true, but no Address has been specified."));
                        }

                        if (Enabled && !Port.HasValue)
                        {
                            results.Add(new ValidationResult($"The Enabled field is true, but no Port has been specified."));
                        }

                        return results;
                    }
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
            [RequiresRestart]
            public int Port { get; private set; } = 5000;

            /// <summary>
            ///     Gets HTTPS options.
            /// </summary>
            [Validate]
            [RequiresRestart]
            public HttpsOptions Https { get; private set; } = new HttpsOptions();

            /// <summary>
            ///     Gets the base url for web requests.
            /// </summary>
            [Argument(default, "url-base")]
            [EnvironmentVariable("URL_BASE")]
            [Description("base url for web requests")]
            [RequiresRestart]
            public string UrlBase { get; private set; } = "/";

            /// <summary>
            ///     Gets the path to static web content.
            /// </summary>
            [Argument(default, "content-path")]
            [EnvironmentVariable("CONTENT_PATH")]
            [Description("path to static web content")]
            [StringLength(255, MinimumLength = 1)]
            [RequiresRestart]
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
                [RequiresRestart]
                public bool Disabled { get; private set; } = false;

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
                [JsonIgnore]
                public string Password { get; private set; } = Program.AppName;

                /// <summary>
                ///     Gets JWT options.
                /// </summary>
                [Validate]
                [RequiresRestart]
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
                    [StringLength(255, MinimumLength = 16)]
                    [RequiresRestart]
                    public string Key { get; private set; } = Guid.NewGuid().ToString();

                    /// <summary>
                    ///     Gets the TTL for JWTs.
                    /// </summary>
                    [Argument(default, "jwt-ttl")]
                    [EnvironmentVariable("JWT_TTL")]
                    [Description("TTL for JWTs")]
                    [Range(3600, int.MaxValue)]
                    [RequiresRestart]
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
                [RequiresRestart]
                public int Port { get; private set; } = 5001;

                /// <summary>
                ///     Gets a value indicating whether HTTP requests should be redirected to HTTPS.
                /// </summary>
                [Argument('f', "force-https")]
                [EnvironmentVariable("HTTPS_FORCE")]
                [Description("redirect HTTP to HTTPS")]
                [RequiresRestart]
                public bool Force { get; private set; } = false;

                /// <summary>
                ///     Gets certificate options.
                /// </summary>
                [Validate]
                [RequiresRestart]
                public CertificateOptions Certificate { get; private set; } = new CertificateOptions();

                /// <summary>
                ///     Certificate options.
                /// </summary>
                [X509Certificate]
                public class CertificateOptions
                {
                    /// <summary>
                    ///     Gets the path to the the X509 certificate .pfx file.
                    /// </summary>
                    [Argument(default, "https-cert-pfx")]
                    [EnvironmentVariable("HTTPS_CERT_PFX")]
                    [Description("path to X509 certificate .pfx")]
                    [FileExists]
                    [RequiresRestart]
                    public string Pfx { get; private set; }

                    /// <summary>
                    ///     Gets the password for the X509 certificate.
                    /// </summary>
                    [Argument(default, "https-cert-password")]
                    [EnvironmentVariable("HTTPS_CERT_PASSWORD")]
                    [Description("X509 certificate password")]
                    [RequiresRestart]
                    [JsonIgnore]
                    public string Password { get; private set; }
                }
            }
        }

        /// <summary>
        ///     Options for external integrations.
        /// </summary>
        public class IntegrationOptions
        {
            /// <summary>
            ///     Gets FTP options.
            /// </summary>
            [Validate]
            public FTPOptions FTP { get; private set; } = new FTPOptions();

            /// <summary>
            ///     Gets Pushbullet options.
            /// </summary>
            [Validate]
            public PushbulletOptions Pushbullet { get; private set; } = new PushbulletOptions();

            /// <summary>
            ///     FTP options.
            /// </summary>
            public class FTPOptions : IValidatableObject
            {
                /// <summary>
                ///     Gets a value indicating whether the FTP integration is enabled.
                /// </summary>
                [Argument(default, "ftp")]
                [EnvironmentVariable("FTP")]
                [Description("enable FTP integration")]
                public bool Enabled { get; private set; }

                /// <summary>
                ///     Gets the FTP address.
                /// </summary>
                [Argument(default, "ftp-address")]
                [EnvironmentVariable("FTP_ADDRESS")]
                [Description("FTP address")]
                public string Address { get; private set; }

                /// <summary>
                ///     Gets the FTP port.
                /// </summary>
                [Argument(default, "ftp-port")]
                [EnvironmentVariable("FTP_PORT")]
                [Description("FTP port")]
                [Range(1, 65535)]
                public int Port { get; private set; } = 21;

                /// <summary>
                ///     Gets the FTP encryption mode.
                /// </summary>
                [Argument(default, "ftp-encryption-mode")]
                [EnvironmentVariable("FTP_ENCRYPTION_MODE")]
                [Description("FTP encryption mode; none, implicit, explicit, auto")]
                [Enum(typeof(FtpEncryptionMode))]
                public string EncryptionMode { get; private set; } = "auto";

                /// <summary>
                ///     Gets a value indicating whether FTP certificate errors should be ignored.
                /// </summary>
                [Argument(default, "ftp-ignore-certificate-errors")]
                [EnvironmentVariable("FTP_IGNORE_CERTIFICATE_ERRORS")]
                [Description("ignore FTP certificate errors")]
                public bool IgnoreCertificateErrors { get; private set; } = false;

                /// <summary>
                ///     Gets the FTP username.
                /// </summary>
                [Argument(default, "ftp-username")]
                [EnvironmentVariable("FTP_USERNAME")]
                [Description("FTP username")]
                public string Username { get; private set; }

                /// <summary>
                ///     Gets the FTP password.
                /// </summary>
                [Argument(default, "ftp-password")]
                [EnvironmentVariable("FTP_PASSWORD")]
                [Description("FTP password")]
                [JsonIgnore]
                public string Password { get; private set; }

                /// <summary>
                ///     Gets the remote path for uploads.
                /// </summary>
                [Argument(default, "ftp-remote-path")]
                [EnvironmentVariable("FTP_REMOTE_PATH")]
                [Description("remote path for FTP uploads")]
                public string RemotePath { get; private set; } = "/";

                /// <summary>
                ///     Gets a value indicating whether existing files should be overwritten.
                /// </summary>
                [Argument(default, "ftp-overwrite-existing")]
                [EnvironmentVariable("FTP_OVERWRITE_EXISTING")]
                [Description("overwrite existing files when uploading to FTP")]
                public bool OverwriteExisting { get; private set; } = true;

                /// <summary>
                ///     Gets the connection timeout value, in milliseconds.
                /// </summary>
                [Argument(default, "ftp-connection-timeout")]
                [EnvironmentVariable("FTP_CONNECTION_TIMEOUT")]
                [Description("FTP connection timeout, in milliseconds")]
                [Range(0, int.MaxValue)]
                public int ConnectionTimeout { get; private set; } = 5000;

                /// <summary>
                ///     Gets the number of times failing uploads will be retried.
                /// </summary>
                [Argument(default, "ftp-retry-attempts")]
                [EnvironmentVariable("FTP_RETRY_ATTEMPTS")]
                [Description("number of times failing FTP uploads will be retried")]
                [Range(0, 5)]
                public int RetryAttempts { get; private set; } = 3;

                /// <summary>
                ///     Extended validation.
                /// </summary>
                /// <param name="validationContext"></param>
                /// <returns></returns>
                public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
                {
                    var results = new List<ValidationResult>();

                    if (Enabled && string.IsNullOrWhiteSpace(Address))
                    {
                        results.Add(new ValidationResult($"The Enabled field is true, but no Address has been specified."));
                    }

                    return results;
                }
            }

            /// <summary>
            ///     Pushbullet options.
            /// </summary>
            public class PushbulletOptions : IValidatableObject
            {
                /// <summary>
                ///     Gets a value indicating whether the Pushbullet integration is enabled.
                /// </summary>
                [Argument(default, "pushbullet")]
                [EnvironmentVariable("PUSHBULLET")]
                [Description("enable Pushbullet integration")]
                public bool Enabled { get; private set; } = false;

                /// <summary>
                ///     Gets the Pushbullet API access token.
                /// </summary>
                [Argument(default, "pushbullet-token")]
                [EnvironmentVariable("PUSHBULLET_ACCESS_TOKEN")]
                [Description("Pushbullet access token")]
                public string AccessToken { get; private set; }

                /// <summary>
                ///     Gets the prefix for Pushbullet notification titles.
                /// </summary>
                [Argument(default, "pushbullet-prefix")]
                [EnvironmentVariable("PUSHBULLET_NOTIFICATION_PREFIX")]
                [Description("prefix for Pushbullet notification titles")]
                public string NotificationPrefix { get; private set; } = "From slskd:";

                /// <summary>
                ///     Gets a value indicating whether a Pushbullet notification should be sent when a private message is received.
                /// </summary>
                [Argument(default, "pushbullet-notify-on-pm")]
                [EnvironmentVariable("PUSHBULLET_NOTIFY_ON_PRIVATE_MESSAGE")]
                [Description("send Pushbullet notifications when private messages are received")]
                public bool NotifyOnPrivateMessage { get; private set; } = true;

                /// <summary>
                ///     Gets a value indicating whether a Pushbullet notification should be sent when the currently logged
                ///     in user's username is mentioned in a room.
                /// </summary>
                [Argument(default, "pushbullet-notify-on-room-mention")]
                [EnvironmentVariable("PUSHBULLET_NOTIFY_ON_ROOM_MENTION")]
                [Description("send Pushbullet notifications when your username is mentioned in a room")]
                public bool NotifyOnRoomMention { get; private set; } = true;

                /// <summary>
                ///     Gets the number of times failing Pushbullet notifications will be retried.
                /// </summary>
                [Argument(default, "pushbullet-retry-attempts")]
                [EnvironmentVariable("PUSHBULLET_RETRY_ATTEMPTS")]
                [Description("number of times failing Pushbullet notifications will be retried")]
                [Range(0, 5)]
                public int RetryAttempts { get; private set; } = 3;

                /// <summary>
                ///     Gets the cooldown time for Pushbullet notifications, in milliseconds.
                /// </summary>
                [Argument(default, "pushbullet-cooldown")]
                [EnvironmentVariable("PUSHBULLET_COOLDOWN_TIME")]
                [Description("cooldown time for Pushbullet notifications, in milliseconds")]
                public int CooldownTime { get; private set; } = 900000; // 15 minutes

                /// <summary>
                ///     Extended validation.
                /// </summary>
                /// <param name="validationContext"></param>
                /// <returns></returns>
                public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
                {
                    var results = new List<ValidationResult>();

                    if (Enabled && string.IsNullOrWhiteSpace(AccessToken))
                    {
                        results.Add(new ValidationResult($"The Enabled field is true, but no AccessToken has been specified."));
                    }

                    return results;
                }
            }
        }
    }
}