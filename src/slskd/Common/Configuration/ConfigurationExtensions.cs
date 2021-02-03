namespace slskd.Configuration
{
    public static class ConfigurationExtensions
    {
        public static CommandLineArgument ToCommandLineArgument(this Option option)
            => new(option.ShortName, option.LongName, option.Type, option.Key, option.Description);

        public static EnvironmentVariable ToEnvironmentVariable(this Option option)
            => new(option.EnvironmentVariable, option.Type, option.Key, option.Description);

        public static DefaultValue ToDefaultValue(this Option option)
            => new(option.Key, option.Type, option.Default);
    }
}
