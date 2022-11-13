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
    using System.Linq;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using FluentFTP;
    using NetTools;
    using slskd.Authentication;
    using slskd.Configuration;
    using slskd.Cryptography;
    using slskd.Network;
    using slskd.Shares;
    using slskd.Validation;
    using Soulseek.Diagnostics;
    using Utility.CommandLine;
    using Utility.EnvironmentVariables;
    using YamlDotNet.Serialization;

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
    public class Options : IValidatableObject
    {
        /// <summary>
        ///     Gets a value indicating whether to display the application version.
        /// </summary>
        [Argument('v', "version")]
        [Description("display version information")]
        [Obsolete("Used only for documentation; see Program for actual implementation")]
        [JsonIgnore]
        [YamlIgnore]
        public bool ShowVersion { get; init; } = false;

        /// <summary>
        ///     Gets a value indicating whether to display a list of command line arguments.
        /// </summary>
        [Argument('h', "help")]
        [Description("display command line usage")]
        [Obsolete("Used only for documentation; see Program for actual implementation")]
        [JsonIgnore]
        [YamlIgnore]
        public bool ShowHelp { get; init; } = false;

        /// <summary>
        ///     Gets a value indicating whether to display a list of configuration environment variables.
        /// </summary>
        [Argument('e', "envars")]
        [Description("display environment variables")]
        [Obsolete("Used only for documentation; see Program for actual implementation")]
        [JsonIgnore]
        [YamlIgnore]
        public bool ShowEnvironmentVariables { get; init; } = false;

        /// <summary>
        ///     Gets a value indicating whether to generate an X509 certificate and password.
        /// </summary>
        [Argument('g', "generate-cert")]
        [Description("generate X509 certificate and password for HTTPS")]
        [Obsolete("Used only for documentation; see Program for actual implementation")]
        [JsonIgnore]
        [YamlIgnore]
        public bool GenerateCertificate { get; init; } = false;

        /// <summary>
        ///     Gets a value indicating whether to generate a random secret.
        /// </summary>
        [Argument('k', "generate-secret")]
        [Description("generate random secret of the specified length")]
        [Obsolete("Used only for documentation; see Program for actual implementation")]
        [JsonIgnore]
        [YamlIgnore]
        public bool GenerateSecret { get; init; } = false;

        /// <summary>
        ///     Gets a value indicating whether the application should run in debug mode.
        /// </summary>
        [Argument('d', "debug")]
        [EnvironmentVariable("DEBUG")]
        [Description("run in debug mode")]
        [RequiresRestart]
        public bool Debug { get; init; } = Debugger.IsAttached;

        /// <summary>
        ///     Gets a value indicating whether remote configuration of options is allowed.
        /// </summary>
        [Argument(default, "remote-configuration")]
        [EnvironmentVariable("REMOTE_CONFIGURATION")]
        [Description("allow remote configuration")]
        public bool RemoteConfiguration { get; init; } = false;

        /// <summary>
        ///     Gets the unique name for this instance.
        /// </summary>
        [Argument('i', "instance-name")]
        [EnvironmentVariable("INSTANCE_NAME")]
        [Description("optional; a unique name for this instance")]
        [RequiresRestart]
        public string InstanceName { get; init; } = "default";

        /// <summary>
        ///     Gets optional flags.
        /// </summary>
        [Validate]
        public FlagsOptions Flags { get; init; } = new FlagsOptions();

        /// <summary>
        ///     Gets the path where application data is saved.
        /// </summary>
        [Argument('a', "app-dir")]
        [EnvironmentVariable("APP_DIR")]
        [Description("path where application data is saved")]
        [Obsolete("Used only for documentation; see Program for actual implementation")]
        [JsonIgnore]
        [YamlIgnore]
        public string AppDirectory { get; init; } = Program.DefaultAppDirectory;

        /// <summary>
        ///     Gets the path where application data is saved.
        /// </summary>
        [Argument('c', "config")]
        [EnvironmentVariable("CONFIG")]
        [Description("path to configuration file")]
        [Obsolete("Used only for documentation; see Program for actual implementation")]
        [JsonIgnore]
        [YamlIgnore]
        public string ConfigurationFile { get; init; } = Program.DefaultConfigurationFile;

        /// <summary>
        ///     Gets network options.
        /// </summary>
        [Validate]
        public NetworkOptions Network { get; init; } = new NetworkOptions();

        /// <summary>
        ///     Gets directory options.
        /// </summary>
        [Validate]
        [RequiresRestart]
        public DirectoriesOptions Directories { get; init; } = new DirectoriesOptions();

        /// <summary>
        ///     Gets share options.
        /// </summary>
        [Validate]
        public SharesOptions Shares { get; init; } = new SharesOptions();

        /// <summary>
        ///     Gets global options.
        /// </summary>
        [Validate]
        public GlobalOptions Global { get; init; } = new GlobalOptions();

        /// <summary>
        ///     Gets user groups.
        /// </summary>
        [Validate]
        public GroupsOptions Groups { get; init; } = new GroupsOptions();

        /// <summary>
        ///     Gets filter options.
        /// </summary>
        [Validate]
        public FiltersOptions Filters { get; init; } = new FiltersOptions();

        /// <summary>
        ///     Gets a list of rooms to automatically join upon connection.
        /// </summary>
        [Argument(default, "rooms")]
        [EnvironmentVariable("ROOMS")]
        [Description("a list of rooms to automatically join")]
        public string[] Rooms { get; init; } = Array.Empty<string>();

        /// <summary>
        ///     Gets options for the web UI.
        /// </summary>
        [Validate]
        public WebOptions Web { get; init; } = new WebOptions();

        /// <summary>
        ///     Gets logger options.
        /// </summary>
        [Validate]
        [RequiresRestart]
        public LoggerOptions Logger { get; init; } = new LoggerOptions();

        /// <summary>
        ///     Gets metrics options.
        /// </summary>
        [Validate]
        [RequiresRestart]
        public MetricsOptions Metrics { get; init; } = new MetricsOptions();

        /// <summary>
        ///     Gets feature options.
        /// </summary>
        [Validate]
        [RequiresRestart]
        public FeatureOptions Feature { get; init; } = new FeatureOptions();

        /// <summary>
        ///     Gets options for the Soulseek client.
        /// </summary>
        [Validate]
        public SoulseekOptions Soulseek { get; init; } = new SoulseekOptions();

        /// <summary>
        ///     Gets options for external integrations.
        /// </summary>
        [Validate]
        public IntegrationOptions Integration { get; init; } = new IntegrationOptions();

        /// <summary>
        ///     Handles top-level validation that doesn't fit anywhere else.
        /// </summary>
        /// <param name="validationContext"></param>
        /// <returns></returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            if (InstanceName == "local" && Network.Mode.ToEnum<OperationMode>() == OperationMode.Agent)
            {
                results.Add(new ValidationResult("Instance name must be something other than 'local' when operating in Network Agent mode"));
            }

            return results;
        }

        /// <summary>
        ///     Optional flags.
        /// </summary>
        public class FlagsOptions
        {
            /// <summary>
            ///     Gets a value indicating whether the logo should be suppressed on startup.
            /// </summary>
            [Argument('n', "no-logo")]
            [EnvironmentVariable("NO_LOGO")]
            [Description("suppress logo on startup")]
            [RequiresRestart]
            public bool NoLogo { get; init; } = false;

            /// <summary>
            ///     Gets a value indicating whether the application should quit after initialization.
            /// </summary>
            [Argument('x', "no-start")]
            [EnvironmentVariable("NO_START")]
            [Description("quit the application after initialization")]
            [RequiresRestart]
            public bool NoStart { get; init; } = false;

            /// <summary>
            ///     Gets a value indicating whether the application should connect to the Soulseek network on startup.
            /// </summary>
            [Argument(default, "no-connect")]
            [EnvironmentVariable("NO_CONNECT")]
            [Description("do not connect to the Soulseek network on startup")]
            [RequiresRestart]
            public bool NoConnect { get; init; } = false;

            /// <summary>
            ///     Gets a value indicating whether the application should scan shared directories on startup.
            /// </summary>
            [Argument(default, "no-share-scan")]
            [EnvironmentVariable("NO_SHARE_SCAN")]
            [Description("do not scan shares on startup")]
            [RequiresRestart]
            public bool NoShareScan { get; init; } = false;

            /// <summary>
            ///     Gets a value indicating whether shares should be forcibly re-scanned on startup.
            /// </summary>
            [Argument(default, "force-share-scan")]
            [EnvironmentVariable("FORCE_SHARE_SCAN")]
            [Description("force a share scan on startup")]
            [RequiresRestart]
            public bool ForceShareScan { get; init; } = false;

            /// <summary>
            ///     Gets a value indicating whether the application should check for a newer version on startup.
            /// </summary>
            [Argument(default, "no-version-check")]
            [EnvironmentVariable("NO_VERSION_CHECK")]
            [Description("do not check for newer version at startup")]
            [RequiresRestart]
            public bool NoVersionCheck { get; init; } = false;

            /// <summary>
            ///     Gets a value indicating whether Entity Framework queries should be logged.
            /// </summary>
            [Argument(default, "log-sql")]
            [EnvironmentVariable("LOG_SQL")]
            [Description("log SQL queries generated by Entity Framework")]
            [RequiresRestart]
            public bool LogSQL { get; init; } = false;

            /// <summary>
            ///     Gets a value indicating whether the application should run in experimental mode.
            /// </summary>
            [Argument(default, "experimental")]
            [EnvironmentVariable("EXPERIMENTAL")]
            [Description("run in experimental mode")]
            [RequiresRestart]
            public bool Experimental { get; init; } = false;

            /// <summary>
            ///     Gets a value indicating whether the application should run in volatile mode.
            /// </summary>
            [Argument(default, "volatile")]
            [EnvironmentVariable("VOLATILE")]
            [Description("use volatile data storage (all data will be lost at shutdown)")]
            [RequiresRestart]
            public bool Volatile { get; init; } = false;
        }

        /// <summary>
        ///     Network options.
        /// </summary>
        public class NetworkOptions : IValidatableObject
        {
            /// <summary>
            ///     Gets the application operation mode.
            /// </summary>
            [Argument('m', "network-operation-mode")]
            [EnvironmentVariable("NETWORK_OPERATION_MODE")]
            [Description("network operation mode; controller, agent")]
            [RequiresRestart]
            [Enum(typeof(OperationMode))]
            public string Mode { get; init; } = slskd.Network.OperationMode.Controller.ToString().ToLowerInvariant();

            /// <summary>
            ///     Gets the controller configuration.
            /// </summary>
            public NetworkControllerConfigurationOptions Controller { get; init; } = new NetworkControllerConfigurationOptions();

            /// <summary>
            ///     Gets the agent configuration.
            /// </summary>
            public Dictionary<string, NetworkAgentConfigurationOptions> Agents { get; init; } = new();

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                var mode = Mode.ToEnum<OperationMode>();
                var results = new List<ValidationResult>();
                var modeResults = new List<ValidationResult>();

                if (mode == slskd.Network.OperationMode.Agent && !Validator.TryValidateObject(Controller, new ValidationContext(Controller), modeResults, validateAllProperties: true))
                {
                    results.Add(new CompositeValidationResult("Controller", modeResults));
                }
                else
                {
                    foreach (var (name, agent) in Agents)
                    {
                        var res = new List<ValidationResult>();
                        if (!Validator.TryValidateObject(agent, new ValidationContext(agent), res, validateAllProperties: true))
                        {
                            modeResults.Add(new CompositeValidationResult(name, res));
                        }
                    }

                    if (modeResults.Any())
                    {
                        results.Add(new CompositeValidationResult("Agents", modeResults));
                    }
                }

                return results;
            }

            /// <summary>
            ///     Network controller configuration options.
            /// </summary>
            public class NetworkControllerConfigurationOptions
            {
                /// <summary>
                ///     Gets the controller address.
                /// </summary>
                [Argument(default, "controller-address")]
                [EnvironmentVariable("CONTROLLER_ADDRESS")]
                [Description("controller address url")]
                [Url]
                [NotNullOrWhiteSpace]
                public string Address { get; init; }

                /// <summary>
                ///     Gets a value indicating whether controller certificate errors should be ignored.
                /// </summary>
                [Argument(default, "controller-ignore-certificate-errors")]
                [EnvironmentVariable("CONTROLLER_IGNORE_CERTIFICATE_ERRORS")]
                [Description("ignore controller certificate errors")]
                public bool IgnoreCertificateErrors { get; init; } = false;

                /// <summary>
                ///     Gets the controller API key.
                /// </summary>
                [Argument(default, "controller-api-key")]
                [EnvironmentVariable("CONTROLLER_API_KEY")]
                [Description("controller api key")]
                [StringLength(255, MinimumLength = 16)]
                [NotNullOrWhiteSpace]
                [Secret]
                public string ApiKey { get; init; }

                /// <summary>
                ///     Gets the controller secret.
                /// </summary>
                [Argument(default, "controller-secret")]
                [EnvironmentVariable("CONTROLLER_SECRET")]
                [Description("shared secret")]
                [StringLength(255, MinimumLength = 16)]
                [NotNullOrWhiteSpace]
                [Secret]
                public string Secret { get; init; }
            }

            /// <summary>
            ///     Network agent configuration options.
            /// </summary>
            public class NetworkAgentConfigurationOptions
            {
                /// <summary>
                ///     Gets the agent secret.
                /// </summary>
                [StringLength(255, MinimumLength = 16)]
                [NotNullOrWhiteSpace]
                [Secret]
                public string Secret { get; init; }
            }
        }

        /// <summary>
        ///     Directory options.
        /// </summary>
        public class DirectoriesOptions
        {
            /// <summary>
            ///     Gets the path where incomplete downloads are saved.
            /// </summary>
            [Argument(default, "incomplete")]
            [EnvironmentVariable("INCOMPLETE_DIR")]
            [Description("path where incomplete downloads are saved")]
            [DirectoryExists(ensureWriteable: true)]
            [RequiresRestart]
            public string Incomplete { get; init; } = Program.DefaultIncompleteDirectory;

            /// <summary>
            ///     Gets the path where downloaded files are saved.
            /// </summary>
            [Argument('o', "downloads")]
            [EnvironmentVariable("DOWNLOADS_DIR")]
            [Description("path where downloaded files are saved")]
            [DirectoryExists(ensureWriteable: true)]
            [RequiresRestart]
            public string Downloads { get; init; } = Program.DefaultDownloadsDirectory;
        }

        /// <summary>
        ///     Share options.
        /// </summary>
        public class SharesOptions : IValidatableObject
        {
            /// <summary>
            ///     Gets the list of paths to shared files.
            /// </summary>
            [Argument('s', "shared")]
            [EnvironmentVariable("SHARED_DIR")]
            [Description("paths to shared files")]
            public string[] Directories { get; init; } = Array.Empty<string>();

            /// <summary>
            ///     Gets the list of shared file filters.
            /// </summary>
            [Argument(default, "share-filter")]
            [EnvironmentVariable("SHARE_FILTER")]
            [Description("regular expressions to filter files from shares")]
            public string[] Filters { get; init; } = Array.Empty<string>();

            /// <summary>
            ///     Share caching options.
            /// </summary>
            [Validate]
            public ShareCacheOptions Cache { get; init; } = new ShareCacheOptions();

            /// <summary>
            ///     Extended validation.
            /// </summary>
            /// <param name="validationContext"></param>
            /// <returns></returns>
            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                var results = new List<ValidationResult>();

                results.AddRange(ValidateShares());
                results.AddRange(ValidateFilters());

                return results;
            }

            private IEnumerable<ValidationResult> ValidateShares()
            {
                var results = new List<ValidationResult>();

                bool IsBlankPath(string share) => Regex.IsMatch(share.LocalizePath(), @"^(!|-){0,1}(\[.*\])$");
                Directories.Where(share => IsBlankPath(share)).ToList()
                    .ForEach(blank => results.Add(new ValidationResult($"Share {blank} doees not specify a path")));

                bool IsRootMount(string share) => Regex.IsMatch(share.LocalizePath(), @"^(!|-){0,1}(\[.*\])/$");
                Directories.Where(share => IsRootMount(share)).ToList()
                    .ForEach(blank => results.Add(new ValidationResult($"Share {blank} specifies a root mount, which is not supported.")));

                // starts with '/', 'X:', or '\\'
                bool IsAbsolutePath(string share) => Regex.IsMatch(share.LocalizePath(), @"^(!|-){0,1}(\[.*\])?(\/|[a-zA-Z]:|\\\\).*$");
                Directories.Where(share => !IsAbsolutePath(share)).ToList()
                    .ForEach(relativePath => results.Add(new ValidationResult($"Share {relativePath} contains a relative path; only absolute paths are supported.")));

                (string Raw, string Alias, string Path) Digest(string share)
                {
                    var matches = Regex.Matches(share, @"^(!|-){0,1}\[(.*)\](.*)$");

                    if (matches.Any())
                    {
                        return (share, matches[0].Groups[2].Value, matches[0].Groups[3].Value);
                    }

                    return (share, share.Split(new[] { '/', '\\' }).Last(), share);
                }

                var digestedShared = Directories
                    .Select(share => Digest(share.TrimEnd('/', '\\')))
                    .ToHashSet();

                // make sure all aliases are distinct
                digestedShared.GroupBy(share => share.Alias).Where(group => group.Count() > 1).ToList()
                    .ForEach(overlap => results.Add(new ValidationResult($"Shares {string.Join(", ", overlap.Select(s => $"'{s.Raw}'"))} collide. Specify an alias for one or both to disambiguate them.")));

                // make sure the same path hasn't been specified twice, under different aliases
                digestedShared.GroupBy(share => share.Path).Where(group => group.Count() > 1).ToList()
                    .ForEach(dupe => results.Add(new ValidationResult($"Shares {string.Join(", ", dupe.Select(s => $"'{s.Raw}'"))} alias the same path")));

                // make sure each share has an alias, or is alias-able, and that the alias is valid
                foreach (var share in digestedShared)
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

            private IEnumerable<ValidationResult> ValidateFilters()
            {
                var results = new List<ValidationResult>();

                foreach (var filter in Filters)
                {
                    if (!filter.IsValidRegex())
                    {
                        results.Add(new ValidationResult($"Share filter '{filter}' is not a valid regular expression"));
                    }
                }

                return results;
            }

            /// <summary>
            ///     Share caching options.
            /// </summary>
            public class ShareCacheOptions
            {
                /// <summary>
                ///     Gets the type of storage to use for the share cache.
                /// </summary>
                [Argument(default, "share-cache-storage-mode")]
                [EnvironmentVariable("SHARE_CACHE_STORAGE_MODE")]
                [Description("the type of storage to use for the cache")]
                [Enum(typeof(StorageMode))]
                [RequiresRestart]
                public string StorageMode { get; init; } = slskd.Shares.StorageMode.Memory.ToString().ToLowerInvariant();

                /// <summary>
                ///     Gets the number of workers to use while scanning shares.
                /// </summary>
                [Argument(default, "share-cache-workers")]
                [EnvironmentVariable("SHARE_CACHE_WORKERS")]
                [Description("the number of workers to use while scanning shares")]
                [Range(1, 128)]
                [RequiresRestart]
                public int Workers { get; init; } = Environment.ProcessorCount;
            }
        }

        /// <summary>
        ///     Global options.
        /// </summary>
        public class GlobalOptions
        {
            /// <summary>
            ///     Gets global upload options.
            /// </summary>
            [Validate]
            public GlobalUploadOptions Upload { get; init; } = new GlobalUploadOptions();

            /// <summary>
            ///     Gets global download options.
            /// </summary>
            [Validate]
            public GlobalDownloadOptions Download { get; init; } = new GlobalDownloadOptions();

            /// <summary>
            ///     Global upload options.
            /// </summary>
            public class GlobalUploadOptions
            {
                /// <summary>
                ///     Gets the limit for the total number of upload slots.
                /// </summary>
                [Argument(default, "upload-slots")]
                [EnvironmentVariable("UPLOAD_SLOTS")]
                [Description("the total number of upload slots")]
                [RequiresRestart]
                [Range(1, int.MaxValue)]
                public int Slots { get; init; } = 10;

                /// <summary>
                ///     Gets the total upload speed limit.
                /// </summary>
                [Argument(default, "upload-speed-limit")]
                [EnvironmentVariable("UPLOAD_SPEED_LIMIT")]
                [Description("the total upload speed limit")]
                [Range(1, int.MaxValue)]
                public int SpeedLimit { get; init; } = int.MaxValue;
            }

            /// <summary>
            ///     Gets global download options.
            /// </summary>
            public class GlobalDownloadOptions
            {
                /// <summary>
                ///     Gets the limit for the total number of download slots.
                /// </summary>
                [Argument(default, "download-slots")]
                [EnvironmentVariable("DOWNLOAD_SLOTS")]
                [Description("the total number of download slots")]
                [RequiresRestart]
                [Range(1, int.MaxValue)]
                public int Slots { get; init; } = int.MaxValue;

                /// <summary>
                ///     Gets the total download speed limit.
                /// </summary>
                [Argument(default, "download-speed-limit")]
                [EnvironmentVariable("DOWNLOAD_SPEED_LIMIT")]
                [Description("the total download speed limit")]
                [Range(1, int.MaxValue)]
                public int SpeedLimit { get; init; } = int.MaxValue;
            }
        }

        /// <summary>
        ///     User groups.
        /// </summary>
        public class GroupsOptions : IValidatableObject
        {
            /// <summary>
            ///     Gets options for the default user group.
            /// </summary>
            /// <remarks>
            ///     These options apply to users that are not privileged, have not been identified as leechers,
            ///     and have not been added as a member of any group.
            /// </remarks>
            [Validate]
            public BuiltInOptions Default { get; init; } = new BuiltInOptions();

            /// <summary>
            ///     Gets options for the leecher user group.
            /// </summary>
            /// <remarks>
            ///     These options apply to users that have been identified as leechers, and have not been added as a member of any group.
            /// </remarks>
            [Validate]
            public LeecherOptions Leechers { get; init; } = new LeecherOptions();

            /// <summary>
            ///     Gets options for the blacklisted user group.
            /// </summary>
            public BlacklistedOptions Blacklisted { get; init; } = new BlacklistedOptions();

            /// <summary>
            ///     Gets user defined groups and options.
            /// </summary>
            [Validate]
            public Dictionary<string, UserDefinedOptions> UserDefined { get; init; } = new Dictionary<string, UserDefinedOptions>();

            /// <summary>
            ///     Extended validation.
            /// </summary>
            /// <param name="validationContext"></param>
            /// <returns></returns>
            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                var builtInGroups = new[] { Application.PrivilegedGroup, Application.DefaultGroup, Application.LeecherGroup };
                var intersection = UserDefined.Keys.Intersect(builtInGroups);

                return intersection.Select(group => new ValidationResult($"User defined group '{group}' collides with a built in group.  Choose a different name."));
            }

            /// <summary>
            ///     Built in user group options.
            /// </summary>
            public class BuiltInOptions
            {
                /// <summary>
                ///     Gets upload options.
                /// </summary>
                [Validate]
                public UploadOptions Upload { get; init; } = new UploadOptions();
            }

            /// <summary>
            ///     Built in blacklisted group options.
            /// </summary>
            public class BlacklistedOptions
            {
                /// <summary>
                ///     Gets the list of group member usernames.
                /// </summary>
                public string[] Members { get; init; } = Array.Empty<string>();
            }

            /// <summary>
            ///     Built in leecher group options.
            /// </summary>
            public class LeecherOptions
            {
                /// <summary>
                ///     Gets leecher threshold options.
                /// </summary>
                [Validate]
                public ThresholdOptions Thresholds { get; init; } = new ThresholdOptions();

                /// <summary>
                ///     Gets upload options.
                /// </summary>
                [Validate]
                public UploadOptions Upload { get; init; } = new UploadOptions();
            }

            /// <summary>
            ///     Leecher threshold options.
            /// </summary>
            public class ThresholdOptions
            {
                /// <summary>
                ///     Gets the minimum number of shared files required to avoid being classified as a leecher.
                /// </summary>
                [Range(1, int.MaxValue)]
                public int Files { get; init; } = 1;

                /// <summary>
                ///     Gets the minimum number of shared directories required to avoid being classified as a leecher.
                /// </summary>
                [Range(1, int.MaxValue)]
                public int Directories { get; init; } = 1;
            }

            /// <summary>
            ///     User defined user group options.
            /// </summary>
            public class UserDefinedOptions
            {
                /// <summary>
                ///     Gets upload options.
                /// </summary>
                [Validate]
                public UploadOptions Upload { get; init; } = new UploadOptions();

                /// <summary>
                ///     Gets the list of group member usernames.
                /// </summary>
                public string[] Members { get; init; } = Array.Empty<string>();
            }

            /// <summary>
            ///     User group upload options.
            /// </summary>
            public class UploadOptions
            {
                /// <summary>
                ///     Gets the priority of the group.
                /// </summary>
                [Range(1, int.MaxValue)]
                public int Priority { get; init; } = 1;

                /// <summary>
                ///     Gets the queue strategy for the group.
                /// </summary>
                [Enum(typeof(Transfers.QueueStrategy))]
                public string Strategy { get; init; } = Transfers.QueueStrategy.RoundRobin.ToString().ToLowerInvariant();

                /// <summary>
                ///     Gets the limit for the total number of upload slots for the group.
                /// </summary>
                [Range(1, int.MaxValue)]
                public int Slots { get; init; } = int.MaxValue;

                /// <summary>
                ///     Gets the total upload speed limit for the group.
                /// </summary>
                [Range(1, int.MaxValue)]
                public int SpeedLimit { get; init; } = int.MaxValue;
            }
        }

        /// <summary>
        ///     Feature options.
        /// </summary>
        public class FeatureOptions
        {
            /// <summary>
            ///     Gets a value indicating whether swagger documentation and UI should be enabled.
            /// </summary>
            [Argument(default, "swagger")]
            [EnvironmentVariable("SWAGGER")]
            [Description("enable swagger documentation and UI")]
            [RequiresRestart]
            public bool Swagger { get; init; } = false;
        }

        /// <summary>
        ///     Filter options.
        /// </summary>
        public class FiltersOptions
        {
            /// <summary>
            ///     Gets search filter options.
            /// </summary>
            [Validate]
            public SearchOptions Search { get; init; } = new SearchOptions();

            /// <summary>
            ///     Search filter options.
            /// </summary>
            public class SearchOptions : IValidatableObject
            {
                /// <summary>
                ///     Gets the list of search request filters.
                /// </summary>
                [Argument(default, "search-request-filter")]
                [EnvironmentVariable("SEARCH_REQUEST_FILTER")]
                [Description("regular expressions to filter incoming search requests")]
                public string[] Request { get; init; } = Array.Empty<string>();

                /// <summary>
                ///     Extended validation.
                /// </summary>
                /// <param name="validationContext"></param>
                /// <returns></returns>
                public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
                {
                    var results = new List<ValidationResult>();

                    foreach (var filter in Request)
                    {
                        if (!filter.IsValidRegex())
                        {
                            results.Add(new ValidationResult($"Search request filter '{filter}' is not a valid regular expression"));
                        }
                    }

                    return results;
                }
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
            public string Loki { get; init; } = null;
        }

        /// <summary>
        ///     Metrics options.
        /// </summary>
        public class MetricsOptions
        {
            /// <summary>
            ///     Gets a value indicating whether the metrics endpoint should be enabled.
            /// </summary>
            [Argument(default, "metrics")]
            [EnvironmentVariable("METRICS")]
            [Description("enable metrics")]
            [RequiresRestart]
            public bool Enabled { get; init; } = false;

            /// <summary>
            ///     Gets the url for the metrics endpoint.
            /// </summary>
            [Argument(default, "metrics-url")]
            [EnvironmentVariable("METRICS_URL")]
            [Description("url for metrics")]
            [RequiresRestart]
            public string Url { get; init; } = "/metrics";

            /// <summary>
            ///     Gets metrics endpoint authentication options.
            /// </summary>
            [Validate]
            public MetricsAuthenticationOptions Authentication { get; init; } = new MetricsAuthenticationOptions();

            /// <summary>
            ///     Metrics endpoint authentication options.
            /// </summary>
            public class MetricsAuthenticationOptions
            {
                /// <summary>
                ///     Gets a value indicating whether authentication should be disabled.
                /// </summary>
                [Argument(default, "metrics-no-auth")]
                [EnvironmentVariable("METRICS_NO_AUTH")]
                [Description("disable authentication for metrics requests")]
                [RequiresRestart]
                public bool Disabled { get; init; } = false;

                /// <summary>
                ///     Gets the username for the metrics endpoint.
                /// </summary>
                [Argument(default, "metrics-username")]
                [EnvironmentVariable("METRICS_USERNAME")]
                [Description("username for metrics")]
                [StringLength(255, MinimumLength = 1)]
                [RequiresRestart]
                public string Username { get; init; } = Program.AppName;

                /// <summary>
                ///     Gets the password for the metrics endpoint.
                /// </summary>
                [Argument(default, "metrics-password")]
                [EnvironmentVariable("METRICS_PASSWORD")]
                [Description("password for metrics")]
                [StringLength(255, MinimumLength = 1)]
                [Secret]
                [RequiresRestart]
                public string Password { get; init; } = Program.AppName;
            }
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
            public string Username { get; init; } = null;

            /// <summary>
            ///     Gets the password for the Soulseek network.
            /// </summary>
            [Argument(default, "slsk-password")]
            [EnvironmentVariable("SLSK_PASSWORD")]
            [Description("password for the Soulseek network")]
            [Secret]
            [RequiresReconnect]
            public string Password { get; init; } = null;

            /// <summary>
            ///     Gets the description of the Soulseek user.
            /// </summary>
            [Argument(default, "slsk-description")]
            [EnvironmentVariable("SLSK_DESCRIPTION")]
            [Description("user description for the Soulseek network")]
            public string Description { get; init; } = "A slskd user. https://github.com/slskd/slskd";

            /// <summary>
            ///     Gets the port on which to listen for incoming connections.
            /// </summary>
            [Argument(default, "slsk-listen-port")]
            [EnvironmentVariable("SLSK_LISTEN_PORT")]
            [Description("port on which to listen for incoming connections")]
            [Range(1024, 65535)]
            public int ListenPort { get; init; } = 50000;

            /// <summary>
            ///     Gets the minimum diagnostic level.
            /// </summary>
            [Argument(default, "slsk-diag-level")]
            [EnvironmentVariable("SLSK_DIAG_LEVEL")]
            [Description("minimum diagnostic level (None, Warning, Info, Debug)")]
            [RequiresRestart]
            public DiagnosticLevel DiagnosticLevel { get; init; } = DiagnosticLevel.Info;

            /// <summary>
            ///     Gets options for the distributed network.
            /// </summary>
            [Validate]
            public DistributedNetworkOptions DistributedNetwork { get; init; } = new DistributedNetworkOptions();

            /// <summary>
            ///     Gets connection options.
            /// </summary>
            [Validate]
            public ConnectionOptions Connection { get; init; } = new ConnectionOptions();

            /// <summary>
            ///     Connection options.
            /// </summary>
            public class ConnectionOptions
            {
                /// <summary>
                ///     Gets connection timeout options.
                /// </summary>
                [Validate]
                public TimeoutOptions Timeout { get; init; } = new TimeoutOptions();

                /// <summary>
                ///     Gets connection buffer options.
                /// </summary>
                [Validate]
                public BufferOptions Buffer { get; init; } = new BufferOptions();

                /// <summary>
                ///     Gets connection proxy options.
                /// </summary>
                [Validate]
                public ProxyOptions Proxy { get; init; } = new ProxyOptions();

                /// <summary>
                ///     Connection buffer options.
                /// </summary>
                public class BufferOptions
                {
                    /// <summary>
                    ///     Gets the connection read buffer size, in bytes.
                    /// </summary>
                    [Argument(default, "slsk-read-buffer")]
                    [EnvironmentVariable("SLSK_READ_BUFFER")]
                    [Description("read buffer size for connections")]
                    [Range(1024, int.MaxValue)]
                    public int Read { get; init; } = 16384;

                    /// <summary>
                    ///     Gets the connection write buffer size, in bytes.
                    /// </summary>
                    [Argument(default, "slsk-write-buffer")]
                    [EnvironmentVariable("SLSK_WRITE_BUFFER")]
                    [Description("write buffer size for connections")]
                    [Range(1024, int.MaxValue)]
                    public int Write { get; init; } = 16384;

                    /// <summary>
                    ///     Gets the read/write buffer size for transfers, in bytes.
                    /// </summary>
                    [Argument(default, "slsk-transfer-buffer")]
                    [EnvironmentVariable("SLSK_TRANSFER_BUFFER")]
                    [Description("read/write buffer size for transfers")]
                    [Range(81920, int.MaxValue)]
                    public int Transfer { get; init; } = 262144;

                    /// <summary>
                    ///     Gets the size of the queue for double buffered writes.
                    /// </summary>
                    [Argument(default, "slsk-write-queue")]
                    [EnvironmentVariable("SLSK_WRITE_QUEUE")]
                    [Description("queue size for double buffered writes")]
                    [Range(5, 5000)]
                    public int WriteQueue { get; init; } = 50;
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
                    public int Connect { get; init; } = 10000;

                    /// <summary>
                    ///     Gets the connection inactivity timeout, in milliseconds.
                    /// </summary>
                    [Argument(default, "slsk-inactivity-timeout")]
                    [EnvironmentVariable("SLSK_INACTIVITY_TIMEOUT")]
                    [Description("connection inactivity timeout, in milliseconds")]
                    [Range(1000, int.MaxValue)]
                    public int Inactivity { get; init; } = 15000;
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
                    public bool Enabled { get; init; } = false;

                    /// <summary>
                    ///     Gets the proxy address.
                    /// </summary>
                    [Argument(default, "slsk-proxy-address")]
                    [EnvironmentVariable("SLSK_PROXY_ADDRESS")]
                    [Description("connection proxy address")]
                    [StringLength(255, MinimumLength = 1)]
                    public string Address { get; init; }

                    /// <summary>
                    ///     Gets the proxy port.
                    /// </summary>
                    [Argument(default, "slsk-proxy-port")]
                    [EnvironmentVariable("SLSK_PROXY_PORT")]
                    [Description("connection proxy port")]
                    [Range(1, 65535)]
                    public int? Port { get; init; }

                    /// <summary>
                    ///     Gets the proxy username, if applicable.
                    /// </summary>
                    [Argument(default, "slsk-proxy-username")]
                    [EnvironmentVariable("SLSK_PROXY_USERNAME")]
                    [Description("connection proxy username")]
                    [StringLength(255, MinimumLength = 1)]
                    public string Username { get; init; }

                    /// <summary>
                    ///     Gets the proxy password, if applicable.
                    /// </summary>
                    [Argument(default, "slsk-proxy-password")]
                    [EnvironmentVariable("SLSK_PROXY_PASSWORD")]
                    [Description("connection proxy password")]
                    [StringLength(255, MinimumLength = 1)]
                    [Secret]
                    public string Password { get; init; }

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
                public bool Disabled { get; init; } = false;

                /// <summary>
                ///     Gets a value indicating whether to accept distributed child connections.
                /// </summary>
                [Argument(default, "slsk-dnet-no-children")]
                [EnvironmentVariable("SLSK_DNET_NO_CHILDREN")]
                [Description("do not accept distributed children")]
                public bool DisableChildren { get; init; } = false;

                /// <summary>
                ///     Gets the distributed child connection limit.
                /// </summary>
                [Argument(default, "slsk-dnet-children")]
                [EnvironmentVariable("SLSK_DNET_CHILDREN")]
                [Description("max number of distributed children")]
                [Range(1, int.MaxValue)]
                public int ChildLimit { get; init; } = 25;

                /// <summary>
                ///     Gets a value indicating whether distributed network logging should be enabled.
                /// </summary>
                [Argument(default, "slsk-dnet-logging")]
                [EnvironmentVariable("SLSK_DNET_LOGGING")]
                [Description("enable distributed network logging")]
                public bool Logging { get; init; } = false;
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
            public int Port { get; init; } = 5000;

            /// <summary>
            ///     Gets HTTPS options.
            /// </summary>
            [Validate]
            [RequiresRestart]
            public HttpsOptions Https { get; init; } = new HttpsOptions();

            /// <summary>
            ///     Gets the base url for web requests.
            /// </summary>
            [Argument(default, "url-base")]
            [EnvironmentVariable("URL_BASE")]
            [Description("base url for web requests")]
            [RequiresRestart]
            public string UrlBase { get; init; } = "/";

            /// <summary>
            ///     Gets the path to static web content.
            /// </summary>
            [Argument(default, "content-path")]
            [EnvironmentVariable("CONTENT_PATH")]
            [Description("path to static web content")]
            [StringLength(255, MinimumLength = 1)]
            [DirectoryExists]
            [DirectoryIsRelative]
            [RequiresRestart]
            public string ContentPath { get; init; } = "wwwroot";

            /// <summary>
            ///     Gets a value indicating whether HTTP request logging should be enabled.
            /// </summary>
            [Argument(default, "http-logging")]
            [EnvironmentVariable("HTTP_LOGGING")]
            [Description("enable http request logging")]
            [RequiresRestart]
            public bool Logging { get; init; } = false;

            /// <summary>
            ///     Gets authentication options.
            /// </summary>
            [Validate]
            public WebAuthenticationOptions Authentication { get; init; } = new WebAuthenticationOptions();

            /// <summary>
            ///     Authentication options.
            /// </summary>
            public class WebAuthenticationOptions
            {
                /// <summary>
                ///     Gets a value indicating whether authentication should be disabled.
                /// </summary>
                [Argument('X', "no-auth")]
                [EnvironmentVariable("NO_AUTH")]
                [Description("disable authentication for web requests")]
                [RequiresRestart]
                public bool Disabled { get; init; } = false;

                /// <summary>
                ///     Gets the username for the web UI.
                /// </summary>
                [Argument('u', "username")]
                [EnvironmentVariable("USERNAME")]
                [Description("username for web UI")]
                [StringLength(255, MinimumLength = 1)]
                public string Username { get; init; } = Program.AppName;

                /// <summary>
                ///     Gets the password for the web UI.
                /// </summary>
                [Argument('p', "password")]
                [EnvironmentVariable("PASSWORD")]
                [Description("password for web UI")]
                [StringLength(255, MinimumLength = 1)]
                [Secret]
                public string Password { get; init; } = Program.AppName;

                /// <summary>
                ///     Gets JWT options.
                /// </summary>
                [Validate]
                [RequiresRestart]
                public JwtOptions Jwt { get; init; } = new JwtOptions();

                /// <summary>
                ///     Gets API keys.
                /// </summary>
                [Validate]
                public Dictionary<string, ApiKeyOptions> ApiKeys { get; init; } = new Dictionary<string, ApiKeyOptions>();

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
                    [Secret]
                    [RequiresRestart]
                    public string Key { get; init; } = Cryptography.Random.GetBytes(16).ToBase62();

                    /// <summary>
                    ///     Gets the TTL for JWTs, in milliseconds.
                    /// </summary>
                    [Argument(default, "jwt-ttl")]
                    [EnvironmentVariable("JWT_TTL")]
                    [Description("TTL for JWTs")]
                    [Range(3600, int.MaxValue)]
                    [RequiresRestart]
                    public int Ttl { get; init; } = 604800000;
                }

                /// <summary>
                ///     API key options.
                /// </summary>
                public class ApiKeyOptions : IValidatableObject
                {
                    /// <summary>
                    ///     Gets the API key value.
                    /// </summary>
                    [Description("API key value")]
                    [StringLength(255, MinimumLength = 16)]
                    [Secret]
                    public string Key { get; init; }

                    /// <summary>
                    ///     Gets the role for the key.
                    /// </summary>
                    [Description("user role for the key; readonly, readwrite, administrator")]
                    [Enum(typeof(Role))]
                    public string Role { get; init; } = slskd.Authentication.Role.ReadOnly.ToString();

                    /// <summary>
                    ///     Gets the comma separated list of CIDRs that are authorized to use the key.
                    /// </summary>
                    [Description("An optional comma separated list of CIDRs that are authorized to use the key")]
                    public string Cidr { get; init; } = "0.0.0.0/0,::/0";

                    /// <summary>
                    ///     Extended validation.
                    /// </summary>
                    /// <param name="validationContext"></param>
                    /// <returns></returns>
                    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
                    {
                        var results = new List<ValidationResult>();

                        foreach (var cidr in Cidr.Split(','))
                        {
                            try
                            {
                                _ = IPAddressRange.Parse(cidr);
                            }
                            catch (Exception ex)
                            {
                                results.Add(new ValidationResult($"CIDR {cidr} is invalid: {ex.Message}"));
                            }
                        }

                        return results;
                    }
                }
            }

            /// <summary>
            ///     HTTPS options.
            /// </summary>
            public class HttpsOptions
            {
                /// <summary>
                ///     Gets a value indicating whether HTTPS should be disabled.
                /// </summary>
                [Argument(default, "no-https")]
                [EnvironmentVariable("NO_HTTPS")]
                [Description("disable HTTPS")]
                [RequiresRestart]
                public bool Disabled { get; init; } = false;

                /// <summary>
                ///     Gets the HTTPS listen port.
                /// </summary>
                [Argument('L', "https-port")]
                [EnvironmentVariable("HTTPS_PORT")]
                [Description("HTTPS listen port for web UI")]
                [Range(1, 65535)]
                [RequiresRestart]
                public int Port { get; init; } = 5001;

                /// <summary>
                ///     Gets a value indicating whether HTTP requests should be redirected to HTTPS.
                /// </summary>
                [Argument('f', "force-https")]
                [EnvironmentVariable("HTTPS_FORCE")]
                [Description("redirect HTTP to HTTPS")]
                [RequiresRestart]
                public bool Force { get; init; } = false;

                /// <summary>
                ///     Gets certificate options.
                /// </summary>
                [Validate]
                [RequiresRestart]
                public CertificateOptions Certificate { get; init; } = new CertificateOptions();

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
                    public string Pfx { get; init; }

                    /// <summary>
                    ///     Gets the password for the X509 certificate.
                    /// </summary>
                    [Argument(default, "https-cert-password")]
                    [EnvironmentVariable("HTTPS_CERT_PASSWORD")]
                    [Description("X509 certificate password")]
                    [RequiresRestart]
                    [Secret]
                    public string Password { get; init; }
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
            public FtpOptions Ftp { get; init; } = new FtpOptions();

            /// <summary>
            ///     Gets Pushbullet options.
            /// </summary>
            [Validate]
            public PushbulletOptions Pushbullet { get; init; } = new PushbulletOptions();

            /// <summary>
            ///     FTP options.
            /// </summary>
            public class FtpOptions : IValidatableObject
            {
                /// <summary>
                ///     Gets a value indicating whether the FTP integration is enabled.
                /// </summary>
                [Argument(default, "ftp")]
                [EnvironmentVariable("FTP")]
                [Description("enable FTP integration")]
                public bool Enabled { get; init; }

                /// <summary>
                ///     Gets the FTP address.
                /// </summary>
                [Argument(default, "ftp-address")]
                [EnvironmentVariable("FTP_ADDRESS")]
                [Description("FTP address")]
                public string Address { get; init; }

                /// <summary>
                ///     Gets the FTP port.
                /// </summary>
                [Argument(default, "ftp-port")]
                [EnvironmentVariable("FTP_PORT")]
                [Description("FTP port")]
                [Range(1, 65535)]
                public int Port { get; init; } = 21;

                /// <summary>
                ///     Gets the FTP encryption mode.
                /// </summary>
                [Argument(default, "ftp-encryption-mode")]
                [EnvironmentVariable("FTP_ENCRYPTION_MODE")]
                [Description("FTP encryption mode; none, implicit, explicit, auto")]
                [Enum(typeof(FtpEncryptionMode))]
                public string EncryptionMode { get; init; } = FtpEncryptionMode.Auto.ToString().ToLowerInvariant();

                /// <summary>
                ///     Gets a value indicating whether FTP certificate errors should be ignored.
                /// </summary>
                [Argument(default, "ftp-ignore-certificate-errors")]
                [EnvironmentVariable("FTP_IGNORE_CERTIFICATE_ERRORS")]
                [Description("ignore FTP certificate errors")]
                public bool IgnoreCertificateErrors { get; init; } = false;

                /// <summary>
                ///     Gets the FTP username.
                /// </summary>
                [Argument(default, "ftp-username")]
                [EnvironmentVariable("FTP_USERNAME")]
                [Description("FTP username")]
                public string Username { get; init; }

                /// <summary>
                ///     Gets the FTP password.
                /// </summary>
                [Argument(default, "ftp-password")]
                [EnvironmentVariable("FTP_PASSWORD")]
                [Description("FTP password")]
                [Secret]
                public string Password { get; init; }

                /// <summary>
                ///     Gets the remote path for uploads.
                /// </summary>
                [Argument(default, "ftp-remote-path")]
                [EnvironmentVariable("FTP_REMOTE_PATH")]
                [Description("remote path for FTP uploads")]
                public string RemotePath { get; init; } = "/";

                /// <summary>
                ///     Gets a value indicating whether existing files should be overwritten.
                /// </summary>
                [Argument(default, "ftp-overwrite-existing")]
                [EnvironmentVariable("FTP_OVERWRITE_EXISTING")]
                [Description("overwrite existing files when uploading to FTP")]
                public bool OverwriteExisting { get; init; } = true;

                /// <summary>
                ///     Gets the connection timeout value, in milliseconds.
                /// </summary>
                [Argument(default, "ftp-connection-timeout")]
                [EnvironmentVariable("FTP_CONNECTION_TIMEOUT")]
                [Description("FTP connection timeout, in milliseconds")]
                [Range(0, int.MaxValue)]
                public int ConnectionTimeout { get; init; } = 5000;

                /// <summary>
                ///     Gets the number of times failing uploads will be retried.
                /// </summary>
                [Argument(default, "ftp-retry-attempts")]
                [EnvironmentVariable("FTP_RETRY_ATTEMPTS")]
                [Description("number of times failing FTP uploads will be retried")]
                [Range(0, 5)]
                public int RetryAttempts { get; init; } = 3;

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
                public bool Enabled { get; init; } = false;

                /// <summary>
                ///     Gets the Pushbullet API access token.
                /// </summary>
                [Argument(default, "pushbullet-token")]
                [EnvironmentVariable("PUSHBULLET_ACCESS_TOKEN")]
                [Description("Pushbullet access token")]
                [Secret]
                public string AccessToken { get; init; }

                /// <summary>
                ///     Gets the prefix for Pushbullet notification titles.
                /// </summary>
                [Argument(default, "pushbullet-prefix")]
                [EnvironmentVariable("PUSHBULLET_NOTIFICATION_PREFIX")]
                [Description("prefix for Pushbullet notification titles")]
                public string NotificationPrefix { get; init; } = "From slskd:";

                /// <summary>
                ///     Gets a value indicating whether a Pushbullet notification should be sent when a private message is received.
                /// </summary>
                [Argument(default, "pushbullet-notify-on-pm")]
                [EnvironmentVariable("PUSHBULLET_NOTIFY_ON_PRIVATE_MESSAGE")]
                [Description("send Pushbullet notifications when private messages are received")]
                public bool NotifyOnPrivateMessage { get; init; } = true;

                /// <summary>
                ///     Gets a value indicating whether a Pushbullet notification should be sent when the currently logged
                ///     in user's username is mentioned in a room.
                /// </summary>
                [Argument(default, "pushbullet-notify-on-room-mention")]
                [EnvironmentVariable("PUSHBULLET_NOTIFY_ON_ROOM_MENTION")]
                [Description("send Pushbullet notifications when your username is mentioned in a room")]
                public bool NotifyOnRoomMention { get; init; } = true;

                /// <summary>
                ///     Gets the number of times failing Pushbullet notifications will be retried.
                /// </summary>
                [Argument(default, "pushbullet-retry-attempts")]
                [EnvironmentVariable("PUSHBULLET_RETRY_ATTEMPTS")]
                [Description("number of times failing Pushbullet notifications will be retried")]
                [Range(0, 5)]
                public int RetryAttempts { get; init; } = 3;

                /// <summary>
                ///     Gets the cooldown time for Pushbullet notifications, in milliseconds.
                /// </summary>
                [Argument(default, "pushbullet-cooldown")]
                [EnvironmentVariable("PUSHBULLET_COOLDOWN_TIME")]
                [Description("cooldown time for Pushbullet notifications, in milliseconds")]
                public int CooldownTime { get; init; } = 900000; // 15 minutes

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