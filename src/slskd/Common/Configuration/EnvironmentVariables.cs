namespace slskd.Configuration
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    /// <summary>
    ///     Indicates that the property is to be used as a target for automatic population of values from environment variables
    ///     when invoking the <see cref="EnvironmentVariables.Populate(Type, string)"/> method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class EnvironmentVariableAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="EnvironmentVariableAttribute"/> class.
        /// </summary>
        /// <param name="name">The name of the environment variable</param>
        public EnvironmentVariableAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        ///     Gets or sets the name of the environment variable.
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    ///     Provides static methods used to populate properties from environment variable values.
    /// </summary>
    public static class EnvironmentVariables
    {
        /// <summary>
        ///     Populates the properties in the invoking class marked with the
        ///     <see cref="EnvironmentVariableAttribute"/><see cref="Attribute"/> with the values specified in environment variables.
        /// </summary>
        /// <param name="caller">Internal parameter used to identify the calling method.</param>
        /// <param name="prefix">The optional prefix for variable names.</param>
        public static void Populate(string prefix = null, [CallerMemberName] string caller = default)
        {
            Populate(GetCallingType(caller), prefix);
        }

        /// <summary>
        ///     Populates the properties in the invoking class marked with the
        ///     <see cref="EnvironmentVariableAttribute"/><see cref="Attribute"/> with the values specified in environment variables.
        /// </summary>
        /// <param name="type">
        ///     The Type for which the static properties matching the list of environment variables are to be populated.
        /// </param>
        /// <param name="prefix">The optional prefix for variable names.</param>
        public static void Populate(Type type, string prefix = "")
        {
            prefix ??= string.Empty;

            var targetProperties = GetTargetProperties(type);

            foreach (var property in targetProperties)
            {
                var propertyType = property.Value.PropertyType;

                string value = Environment.GetEnvironmentVariable(prefix + property.Key);

                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                object convertedValue;

                if (propertyType == typeof(bool))
                {
                    convertedValue = value?.Equals("true", StringComparison.InvariantCultureIgnoreCase) ?? false;
                }
                else if (propertyType.IsArray || (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>)))
                {
                    Type valueType;

                    if (propertyType.IsArray)
                    {
                        valueType = propertyType.GetElementType();
                    }
                    else
                    {
                        valueType = propertyType.GetGenericArguments()[0];
                    }

                    // create a list to store converted values
                    Type valueListType = typeof(List<>).MakeGenericType(valueType);
                    var valueList = (IList)Activator.CreateInstance(valueListType);

                    // populate the list
                    foreach (object v in value.Split(',').Select(s => s.Trim()))
                    {
                        valueList.Add(ChangeType(v, property.Key, valueType));
                    }

                    if (propertyType.IsArray)
                    {
                        var valueArray = Array.CreateInstance(propertyType.GetElementType(), valueList.Count);

                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            valueArray.SetValue(valueList[i], i);
                        }

                        convertedValue = valueArray;
                    }
                    else
                    {
                        convertedValue = valueList;
                    }
                }
                else
                {
                    convertedValue = ChangeType(value, property.Key, property.Value.PropertyType);
                }

                property.Value.SetValue(null, convertedValue);
            }
        }

        private static Type GetCallingType(string caller)
        {
            var callingMethod = new StackTrace().GetFrames()
                .Select(f => f.GetMethod())
                .FirstOrDefault(m => m.Name == caller);

            if (callingMethod == default)
            {
                throw new InvalidOperationException($"Unable to determine the containing type of the calling method '{caller}'.  Explicitly specify the originating Type.");
            }

            return callingMethod.DeclaringType;
        }

        private static object ChangeType(object value, string name, Type type)
        {
            try
            {
                if (type.IsEnum)
                {
                    return Enum.Parse(type, (string)value, true);
                }

                return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is OverflowException || ex is ArgumentNullException)
            {
                string message = $"Failed to convert value '{value}' to target type {type}";
                throw new ArgumentException(message, name, ex);
            }
        }

        private static Dictionary<string, PropertyInfo> GetTargetProperties(Type type)
        {
            Dictionary<string, PropertyInfo> properties = new Dictionary<string, PropertyInfo>();

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static))
            {
                // attempt to fetch the ArgumentAttribute of the property
                CustomAttributeData attribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == typeof(EnvironmentVariableAttribute).Name);

                // if found, extract the Name property and add it to the dictionary
                if (attribute != default(CustomAttributeData))
                {
                    string name = (string)attribute.ConstructorArguments[0].Value;

                    if (!properties.ContainsKey(name))
                    {
                        properties.Add(name, property);
                    }
                }
            }

            return properties;
        }
    }
}