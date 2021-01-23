namespace slskd
{
    using Microsoft.Extensions.Configuration;
    using System;
    using System.IO;
    using System.Linq;
    using Utility.CommandLine;
    using Utility.EnvironmentVariables;

    public class InstanceOptions
    {
        [Argument('h', "help", "print usage")]
        public static bool ShowHelp { get; private set; }

        [Argument('v', "version", "display version information")]
        public static bool ShowVersion { get; private set; }

        [EnvironmentVariable("SLSKD_DEBUG")]
        [Configuration("slskd:debug")]
        [Argument('d', "debug", "run in debug mode")]
        public static bool Debug { get; private set; }

        [EnvironmentVariable("SLSKD_NO_LOGO")]
        [Configuration("slskd:no_logo")]
        [Argument('n', "no-logo", "suppress logo on startup")]
        public static bool DisableLogo { get; private set; }

        [EnvironmentVariable("SLSKD_NO_AUTH")]
        [Configuration("slskd:no_auth")]
        [Argument('x', "no-auth", "disable authentication for web requests")]
        public static bool DisableAuthentication { get; private set; }

        [EnvironmentVariable("SLSKD_INSTANCE_NAME")]
        [Configuration("slskd:instance_name")]
        [Argument('i', "instance-name", "optional; a unique name for this instance")]
        public static string InstanceName { get; private set; } = "default";

        [EnvironmentVariable("SLSKD_FEATURE_PROMETHEUS")]
        [Configuration("slskd:feature:prometheus")]
        [Argument('m', "prometheus", "enable collection and publish of prometheus metrics")]
        public static bool EnablePrometheus { get; private set; }

        [EnvironmentVariable("SLSKD_FEATURE_SWAGGER")]
        [Configuration("slskd:feature:swagger")]
        [Argument('s', "swagger", "enable swagger documentation")]
        public static bool EnableSwagger { get; private set; }

        [EnvironmentVariable("SLSKD_LOKI")]
        [Configuration("slskd:logger:loki")]
        [Argument('l', "loki", "optional; the url to a Grafana Loki instance to which to log")]
        public static string LokiUrl { get; private set; }

        [EnvironmentVariable("SLSKD_URL_BASE")]
        [Configuration("slskd:web:url_base")]
        [Argument('b', "url-base", "base url for web requests")]
        public static string UrlBase { get; private set; } = "/";

        [EnvironmentVariable("SLSKD_CONTENT_PATH")]
        [Configuration("slskd:web:content_path")]
        [Argument('c', "content-path", "path to static web content")]
        public static string ContentPath { get; private set; } = Path.Combine(Path.GetDirectoryName(new Uri(AppContext.BaseDirectory).AbsolutePath), "wwwroot");

        [EnvironmentVariable("SLSKD_JWT_KEY")]
        [Configuration("slskd:web:jwt:key")]
        [Argument('k', "jwt-key", "optional; the key with which to sign JWTs")]
        public static string JwtKey { get; private set; } = Guid.NewGuid().ToString();

        [EnvironmentVariable("SLSKD_JWT_TTL")]
        [Configuration("slskd:web:jwt:ttl")]
        [Argument('t', "jwt-ttl", "optional; the TTL for JWTs")]
        public static int JwtTTL { get; private set; } = 604800000;

        [EnvironmentVariable("SLSKD_USERNAME")]
        [Configuration("soulseek:username")]
        [Argument('u', "username", "the username for the Soulseek network")]
        public static string Username { get; private set; }

        [EnvironmentVariable("SLSKD_PASSWORD")]
        [Configuration("soulseek:password")]
        [Argument('p', "password", "the password for the Soulseek network")]
        public static string Password { get; private set; }

        public static bool EnableLoki { get; private set; }

        //public static void Populate(string configurationFile = null, bool argumentsOnly = false)
        //{
        //    if (!argumentsOnly)
        //    {
        //        try
        //        {
        //            EnvironmentVariables.Populate(typeof(InstanceOptions));
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"Error parsing environment variables: {ex.Message}");
        //            return;
        //        }

        //        try
        //        {
        //            var config = new ConfigurationBuilder()
        //                .SetBasePath(Directory.GetCurrentDirectory())
        //                .AddMapping(new[] 
        //                {
        //                    new OptionMap {
        //                        Description = "run in debug mode",
        //                        EnvironmentVariable = "SLSK_DEBUG",
        //                        ShortName = 'd',
        //                        LongName = "debug",
        //                        Key = "slskd:debug"
        //                    },
        //                    new OptionMap {
        //                        Description = "optional; a unique name for this instance",
        //                        EnvironmentVariable = "SLSK_INSTANCE_NAME",
        //                        ShortName = 'i',
        //                        LongName = "instance-name",
        //                        Key = "slskd:instancename"
        //                    }
        //                }, Environment.CommandLine, configurationFile, optional: true, reloadOnChange: false)
        //                .Build();

        //            Console.WriteLine(config.GetDebugView());

        //            config.Populate(typeof(InstanceOptions));
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"Error parsing configuration file '{configurationFile}': {ex.Message}");
        //            return;
        //        }
        //    }

        //    try
        //    {
        //        Arguments.Populate(typeof(InstanceOptions), clearExistingValues: false);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error parsing command line input: {ex.Message}");
        //        return;
        //    }

        //    EnableLoki = !string.IsNullOrEmpty(LokiUrl);
        //    UrlBase = UrlBase.StartsWith("/") ? UrlBase : "/" + UrlBase;
        //    ContentPath = Path.GetFullPath(ContentPath);
        //}

        public static void PrintHelp()
        {
            PrintArguments();
        }

        public static void PrintArguments()
        {
            static string GetLongName(ArgumentInfo info)
                => info.Property.PropertyType == typeof(bool) ? info.LongName : $"{info.LongName} <{info.Property.PropertyType.Name.ToLowerInvariant()}>";

            static string GetShortName(ArgumentInfo info)
                => info.ShortName > 0 ? $"-{info.ShortName}|" : "   ";

            var arguments = Arguments.GetArgumentInfo(typeof(InstanceOptions));
            var longestName = arguments.Select(a => GetLongName(a)).Max(n => n.Length);

            Console.WriteLine("\nusage: slskd [arguments]\n");
            Console.WriteLine("arguments:\n");
            foreach (var info in arguments)
            {
                var envar = info.Property.CustomAttributes
                    ?.Where(a => a.AttributeType == typeof(EnvironmentVariableAttribute)).FirstOrDefault()
                    ?.ConstructorArguments.FirstOrDefault().Value;

                var result = $"  {GetShortName(info)}--{GetLongName(info).PadRight(longestName + 3)}{info.HelpText}";
                Console.WriteLine(result);
            }
        }

        public static void PrintEnvironmentVariables()
        {
            static string GetEnvironmentVariableName(ArgumentInfo info, bool includeType = false)
                => info.Property.CustomAttributes
                    ?.Where(a => a.AttributeType == typeof(EnvironmentVariableAttribute)).FirstOrDefault()
                    ?.ConstructorArguments.FirstOrDefault().Value?.ToString() + (includeType ? $" <{info.Property.PropertyType.Name.ToLowerInvariant()}>" : "");

            var arguments = Arguments.GetArgumentInfo(typeof(InstanceOptions));
            var longestName = arguments.Select(a => GetEnvironmentVariableName(a, includeType: true)).Where(a => a != null).Max(n => n.Length);

            Console.WriteLine("\nenvironment variables (arguments have precedence):\n");
            foreach (var info in arguments)
            {
                var envar = GetEnvironmentVariableName(info, includeType: false);

                if (string.IsNullOrEmpty(envar)) continue;

                var result = $"  {GetEnvironmentVariableName(info, includeType: true).PadRight(longestName + 3)}{info.HelpText}";
                Console.WriteLine(result);
            }
        }
    }
}
