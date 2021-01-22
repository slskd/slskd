namespace slskd
{
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    public class ConfigurationAttribute : Attribute
    {
        public ConfigurationAttribute(string key)
        {
            Key = key;
        }

        public string Key { get; set; }
    }

    public static class Configuration
    {
        public static void Populate(this IConfiguration configuration, Type targetType = null, [CallerMemberName] string caller = default(string))
        {
            var type = targetType ?? GetCallingType(caller);
            var targetProperties = GetTargetProperties(type);

            foreach (var property in targetProperties)
            {
                var propertyType = property.Value.PropertyType;

                string value = configuration.GetValue<string>(property.Key);

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

            if (callingMethod == default(MethodBase))
            {
                throw new InvalidOperationException($"Unable to determine the containing type of the calling method '{caller}'.  Explicitly specify the originating Type.");
            }

            return callingMethod.DeclaringType;
        }

        private static Dictionary<string, PropertyInfo> GetTargetProperties(Type type)
        {
            Dictionary<string, PropertyInfo> properties = new Dictionary<string, PropertyInfo>();

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static))
            {
                // attempt to fetch the ArgumentAttribute of the property
                CustomAttributeData attribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == typeof(ConfigurationAttribute).Name);

                // if found, extract the Name property and add it to the dictionary
                if (attribute != default(CustomAttributeData))
                {
                    string name = ((string)attribute.ConstructorArguments[0].Value)?.Replace("_", string.Empty).Replace("-", string.Empty);

                    if (!properties.ContainsKey(name))
                    {
                        properties.Add(name, property);
                    }
                }
            }

            return properties;
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
    }
}
