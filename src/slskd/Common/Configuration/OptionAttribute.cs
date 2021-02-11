namespace slskd.Common.Configuration
{
    using System;

    public class OptionAttribute : Attribute
    {
        public OptionAttribute(
            char shortName,
            string longName,
            string environmentVariable,
            string description)
        {
            ShortName = shortName;
            LongName = longName;
            EnvironmentVariable = environmentVariable;
            Description = description;
        }

        public char ShortName { get; set; }
        public string LongName { get; set; }
        public string EnvironmentVariable { get; set; }
        public string Description { get; set; }
    }
}
