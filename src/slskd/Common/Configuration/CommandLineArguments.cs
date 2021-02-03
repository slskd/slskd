namespace slskd.Configuration
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     Provides extension method(s) for the CommandLineArguments namespace.
    /// </summary>
    public static class CommandLineArgumentsExtensions
    {
        /// <summary>
        ///     Gets the DeclaringType of the first method on the stack whose name matches the specified <paramref name="caller"/>.
        /// </summary>
        /// <param name="caller">The name of the calling method for which the DeclaringType is to be fetched.</param>
        /// <returns>The DeclaringType of the first method on the stack whose name matches the specified <paramref name="caller"/>.</returns>
        internal static Type GetCallingType(string caller)
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

        /// <summary>
        ///     Removes the outermost pair of enclosing quotes.
        /// </summary>
        /// <param name="value">The string to trim.</param>
        /// <returns>The trimmed string.</returns>
        internal static string TrimOuterQuotes(this string value)
        {
            if (value.StartsWith("\"", StringComparison.InvariantCultureIgnoreCase) && value.EndsWith("\"", StringComparison.InvariantCultureIgnoreCase))
            {
                value = value.Trim('"');
            }
            else if (value.StartsWith("'", StringComparison.InvariantCultureIgnoreCase) && value.EndsWith("'", StringComparison.InvariantCultureIgnoreCase))
            {
                value = value.Trim('\'');
            }

            return value;
        }
    }

    /// <summary>
    ///     Indicates that the property is to be used as a target for automatic population of values from command line arguments
    ///     when invoking the <see cref="CommandLineArguments.Populate(string, bool, string)"/> method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class CommandLineArgumentAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CommandLineArgumentAttribute"/> class.
        /// </summary>
        /// <param name="shortName">The short name of the argument, represented as a single character.</param>
        /// <param name="longName">The long name of the argument.</param>
        /// <param name="helpText">The help text of the argument.</param>
        public CommandLineArgumentAttribute(char shortName, string longName, string helpText = null)
        {
            ShortName = shortName;
            LongName = longName;
            HelpText = helpText;
        }

        /// <summary>
        ///     Gets or sets the help text of the argument.
        /// </summary>
        public string HelpText { get; set; }

        /// <summary>
        ///     Gets or sets the long name of the argument.
        /// </summary>
        public string LongName { get; set; }

        /// <summary>
        ///     Gets or sets the short name of the argument.
        /// </summary>
        public char ShortName { get; set; }
    }

    /// <summary>
    ///     Encapsulates argument names and help text.
    /// </summary>
    public class ArgumentInfo
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ArgumentInfo"/> class.
        /// </summary>
        /// <param name="shortName">The short name of the argument.</param>
        /// <param name="longName">The long name of the argument.</param>
        /// <param name="helpText">The help text for the argument.</param>
        /// <param name="property">The property with which the argument is associated.</param>
        public ArgumentInfo(char shortName, string longName, string helpText, PropertyInfo property)
        {
            ShortName = shortName;
            LongName = longName;
            HelpText = helpText;
            Property = property;
        }

        /// <summary>
        ///     Gets the help text for the argument.
        /// </summary>
        public string HelpText { get; }

        /// <summary>
        ///     Gets a value indicating whether the argument backing Type is a collection.
        /// </summary>
        public bool IsCollection => Property.PropertyType.IsArray || (Property.PropertyType.IsGenericType && Property.PropertyType.GetGenericTypeDefinition() == typeof(List<>));

        /// <summary>
        ///     Gets the long name of the argument.
        /// </summary>
        public string LongName { get; }

        /// <summary>
        ///     Gets the property with which the argument is associated.
        /// </summary>
        public PropertyInfo Property { get; }

        /// <summary>
        ///     Gets the short name of the argument.
        /// </summary>
        public char ShortName { get; }
    }

    /// <summary>
    ///     Provides static methods used to retrieve the command line arguments and operands with which the application was
    ///     started, as well as a Type to contain them.
    /// </summary>
    public class CommandLineArguments
    {
        /// <summary>
        ///     The regular expression with which to parse the command line string.
        /// </summary>
        private const string ArgumentRegExWithoutForwardSlash = "(?:[-]{1,2})([^=: ]+)[=: ]?([^-'\"]\\S*|\\\"[^\"]*\\\"|\\\'[^']*\\\')?|([^ ([^'\\\"]+|\"[^\\\"]+\"|\\\'[^']+\\\')";

        /// <summary>
        ///     The regular expression with which to parse the command line string, including the ability to specify forward slashes.
        /// </summary>
        private const string ArgumentRegExWithForwardSlash = "(?:[-]{1,2}|\\/)([^=: ]+)[=: ]?([^-'\"\\/]\\S*|\\\"[^\"]*\\\"|\\\'[^']*\\\')?|([^ ([^'\\\"]+|\"[^\\\"]+\"|\\\'[^']+\\\')";

        /// <summary>
        ///     The regular expression with which to parse argument-value groups.
        /// </summary>
        private const string GroupRegEx = "^-[^-]+";

        /// <summary>
        ///     The regular expression with which to parse strings strictly containing operands.
        /// </summary>
        private const string OperandRegEx = "([^ ([^'\\\"]+|\\\"[^\\\"]+\\\"|\\\'[^']+\\\')";

        /// <summary>
        ///     The regular expression with which to split the command line string explicitly among argument/value pairs and
        ///     operands, and strictly operands.
        /// </summary>
        /// <remarks>
        ///     This regular expression effectively splits a string into two parts; the part before the first "--", and the part
        ///     after. Instances of "--" not surrounded by a word boundary and those enclosed in quotes are ignored.
        /// </remarks>
        private const string StrictOperandSplitRegEx = "(.*?[^\\\"\\\'])?(\\B-{2}\\B)[^\\\"\\\']?(.*)";

        private CommandLineArguments(string commandLineString, List<KeyValuePair<string, string>> argumentList, Dictionary<string, object> argumentDictionary, List<string> operandList, Type targetType = null)
        {
            CommandLineString = commandLineString;
            ArgumentList = argumentList;
            ArgumentDictionary = argumentDictionary;
            OperandList = operandList;
            TargetType = targetType;
        }

        /// <summary>
        ///     Gets or sets a value indicating whether forward slashes are processed as argument delimiters.
        /// </summary>
        public static bool EnableForwardSlash { get; set; } = false;

        /// <summary>
        ///     Gets a dictionary containing the arguments and values specified in the command line arguments with which the
        ///     application was started.
        /// </summary>
        /// <remarks>
        ///     This dictionary contains argument key/value pairs compiled from the <see cref="ArgumentList"/> and checked against
        ///     the <see cref="TargetType"/> to combine duplicated pairs into lists where the backing property is a collection, and
        ///     to overwrite where the backing property is not a collection.
        /// </remarks>
        public Dictionary<string, object> ArgumentDictionary { get; }

        /// <summary>
        ///     Gets the list of arguments specified in the command line arguments with which the application was started.
        /// </summary>
        /// <remarks>
        ///     This list contains each argument key/value pair as supplied in the original string, preserving the original order
        ///     and any duplicated pairs.
        /// </remarks>
        public List<KeyValuePair<string, string>> ArgumentList { get; }

        /// <summary>
        ///     Gets the command line string from which the arguments were parsed.
        /// </summary>
        public string CommandLineString { get; }

        /// <summary>
        ///     Gets a list containing the operands specified in the command line arguments with which the application was started.
        /// </summary>
        public List<string> OperandList { get; private set; }

        /// <summary>
        ///     Gets the target Type, if applicable.
        /// </summary>
        public Type TargetType { get; }

        private static string ArgumentRegEx => EnableForwardSlash ? ArgumentRegExWithForwardSlash : ArgumentRegExWithoutForwardSlash;

        /// <summary>
        ///     Gets the argument value corresponding to the specified <paramref name="index"/>.
        /// </summary>
        /// <param name="index">The index for which the value is to be retrieved.</param>
        /// <returns>The argument value corresponding to the specified index.</returns>
        public object this[int index]
        {
            get
            {
                return ArgumentList[index].Value;
            }
        }

        /// <summary>
        ///     Gets the argument value corresponding to the specified <paramref name="key"/> from the
        ///     <see cref="ArgumentDictionary"/> property.
        /// </summary>
        /// <param name="key">The key for which the value is to be retrieved.</param>
        /// <returns>The argument value corresponding to the specified key.</returns>
        public object this[string key]
        {
            get
            {
                return ArgumentDictionary[key];
            }
        }

        /// <summary>
        ///     Retrieves a collection of <see cref="ArgumentInfo"/> gathered from properties in the target <paramref name="type"/>
        ///     marked with the <see cref="CommandLineArgumentAttribute"/><see cref="Attribute"/> along with the short and long names and help text.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> for which the matching properties are to be retrieived.</param>
        /// <param name="caller">Internal parameter used to identify the calling method.</param>
        /// <returns>The retrieved collection of <see cref="ArgumentInfo"/>.</returns>
        public static IEnumerable<ArgumentInfo> GetArgumentInfo(Type type = null, [CallerMemberName] string caller = default)
        {
            type ??= CommandLineArgumentsExtensions.GetCallingType(caller);
            var retVal = new List<ArgumentInfo>();

            foreach (PropertyInfo property in GetArgumentProperties(type).Values.Distinct())
            {
                CustomAttributeData attribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == typeof(CommandLineArgumentAttribute).Name);

                if (attribute != default(CustomAttributeData))
                {
                    retVal.Add(new ArgumentInfo(
                        shortName: (char)attribute.ConstructorArguments[0].Value,
                        longName: (string)attribute.ConstructorArguments[1].Value,
                        helpText: (string)attribute.ConstructorArguments[2].Value,
                        property: property));
                }
            }

            return retVal;
        }

        /// <summary>
        ///     Returns a dictionary containing the values specified in the command line arguments with which the application was
        ///     started, keyed by argument name.
        /// </summary>
        /// <param name="commandLineString">The command line arguments with which the application was started.</param>
        /// <param name="type">The <see cref="Type"/> for which the command line string is to be parsed.</param>
        /// <returns>
        ///     The dictionary containing the arguments and values specified in the command line arguments with which the
        ///     application was started.
        /// </returns>
        public static CommandLineArguments Parse(string commandLineString = default, Type type = null)
        {
            commandLineString = commandLineString == default || string.IsNullOrEmpty(commandLineString) ? Environment.CommandLine : commandLineString;

            List<KeyValuePair<string, string>> argumentList;
            List<string> operandList;

            // use the strict operand regular expression to test for/extract the two halves of the string, if the operator is used.
            MatchCollection matches = Regex.Matches(commandLineString, StrictOperandSplitRegEx);

            // if there is a match, the string contains the strict operand delimiter. parse the first and second matches accordingly.
            if (matches.Count > 0)
            {
                // the first group of the first match will contain everything in the string prior to the strict operand delimiter,
                // so extract the argument key/value pairs and list of operands from that string.
                argumentList = GetArgumentList(matches[0].Groups[1].Value);
                operandList = GetOperandList(matches[0].Groups[1].Value);

                // the first group of the second match will contain everything in the string after the strict operand delimiter, so
                // extract the operands from that string using the strict method.
                if (!string.IsNullOrEmpty(matches[0].Groups[3].Value))
                {
                    List<string> operandListStrict = GetOperandListStrict(matches[0].Groups[3].Value);
                    operandList.AddRange(operandListStrict);
                }
            }
            else
            {
                argumentList = GetArgumentList(commandLineString);
                operandList = GetOperandList(commandLineString);
            }

            var argumentDictionary = GetArgumentDictionary(argumentList, type);
            return new CommandLineArguments(commandLineString, argumentList, argumentDictionary, operandList, type);
        }

        /// <summary>
        ///     Populates the properties in the invoking class marked with the
        ///     <see cref="CommandLineArgumentAttribute"/><see cref="Attribute"/> with the values specified in the list of command line
        ///     arguments, if present.
        /// </summary>
        /// <param name="commandLineString">The command line arguments with which the application was started.</param>
        /// <param name="clearExistingValues">Whether to clear the properties before populating them. Defaults to true.</param>
        /// <param name="caller">Internal parameter used to identify the calling method.</param>
        public static void Populate(string commandLineString = default, bool clearExistingValues = true, [CallerMemberName] string caller = default)
        {
            var type = CommandLineArgumentsExtensions.GetCallingType(caller);
            Populate(type, Parse(commandLineString, type), clearExistingValues);
        }

        /// <summary>
        ///     Populates the properties in the specified Type marked with the
        ///     <see cref="CommandLineArgumentAttribute"/><see cref="Attribute"/> with the values specified in the list of command line
        ///     arguments, if present.
        /// </summary>
        /// <param name="type">
        ///     The Type for which the static properties matching the list of command line arguments are to be populated.
        /// </param>
        /// <param name="commandLineString">The command line arguments with which the application was started.</param>
        /// <param name="clearExistingValues">Whether to clear the properties before populating them. Defaults to true.</param>
        public static void Populate(Type type, string commandLineString = default, bool clearExistingValues = true)
        {
            Populate(type, Parse(commandLineString, type), clearExistingValues);
        }

        /// <summary>
        ///     Populates the properties in the specified Type marked with the
        ///     <see cref="CommandLineArgumentAttribute"/><see cref="Attribute"/> with the values specified in the specified argument
        ///     dictionary, if present. All property values are set to null at the start of the routine.
        /// </summary>
        /// <param name="type">
        ///     The Type for which the static properties matching the list of command line arguments are to be populated.
        /// </param>
        /// <param name="arguments">
        ///     The Arguments object containing the dictionary containing the argument-value pairs with which the destination
        ///     properties should be populated and the list of operands.
        /// </param>
        /// <param name="clearExistingValues">Whether to clear the properties before populating them. Defaults to true.</param>
        public static void Populate(Type type, CommandLineArguments arguments, bool clearExistingValues = true)
        {
            // fetch any properties in the specified type marked with the ArgumentAttribute attribute and clear them
            Dictionary<string, PropertyInfo> properties = GetArgumentProperties(type);

            if (clearExistingValues)
            {
                ClearProperties(properties);
            }

            foreach (string propertyName in properties.Keys)
            {
                // if the argument dictionary contains a matching argument
                if (arguments.ArgumentDictionary.ContainsKey(propertyName))
                {
                    // retrieve the property and type
                    PropertyInfo property = properties[propertyName];
                    Type propertyType = property.PropertyType;

                    // retrieve the value from the argument dictionary
                    object value = arguments.ArgumentDictionary[propertyName];

                    object convertedValue;

                    // if the type of the property is bool and the argument value is empty set the property value to true,
                    // indicating the argument is present
                    if (propertyType == typeof(bool))
                    {
                        convertedValue = true;

                        // if a value is specified, a bool flag was followed by an operand and the parser interpreted this as key
                        // value pair because it wasn't aware the flag was backed by a bool. remove the argument from the original
                        // string and re-parse operands from it to preserve order.
                        if (!string.IsNullOrEmpty(value.ToString()))
                        {
                            var arg = Regex.Matches(arguments.CommandLineString, "(?:[-]{1,2}|\\/)" + propertyName)[0].Value;
                            arguments.OperandList = GetOperandList(arguments.CommandLineString.Replace(arg, string.Empty));
                        }
                    }
                    else if (propertyType.IsArray || (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>)))
                    {
                        convertedValue = value;

                        // next, create a list with the same type as the target property
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
                        foreach (object v in (List<object>)convertedValue)
                        {
                            valueList.Add(ChangeType(v, propertyName, valueType));
                        }

                        // if the target property is an array, create one and populate it from the list this is surprisingly
                        // difficult here because we created the source list with the Activator and ToArray() won't work easily.
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
                        convertedValue = ChangeType(value, propertyName, propertyType);
                    }

                    // set the target properties' value to the converted value from the argument string
                    property.SetValue(null, convertedValue);
                }
            }

            PropertyInfo operandsProperty = GetOperandsProperty(type);

            // check to ensure the target class has a property marked with the Operands attribute; if not GetOperandsProperty()
            // will return null.
            if (operandsProperty != default)
            {
                if (operandsProperty.PropertyType.IsAssignableFrom(typeof(List<string>)))
                {
                    operandsProperty.SetValue(null, arguments.OperandList);
                }
                else
                {
                    operandsProperty.SetValue(null, arguments.OperandList.ToArray());
                }
            }
        }

        private static object ChangeType(object value, string argument, Type toType)
        {
            try
            {
                if (toType.IsEnum)
                {
                    return Enum.Parse(toType, (string)value, true);
                }

                return Convert.ChangeType(value, toType, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is OverflowException || ex is ArgumentNullException)
            {
                string message = $"Specified value '{value}' for argument '{argument}' (expected type: {toType}).  ";
                message += "See inner exception for details.";

                throw new ArgumentException(message, ex);
            }
        }

        private static void ClearProperties(Dictionary<string, PropertyInfo> properties)
        {
            foreach (string key in properties.Keys)
            {
                properties[key].SetValue(null, null);
            }
        }

        private static Dictionary<string, object> GetArgumentDictionary(List<KeyValuePair<string, string>> argumentList, Type targetType = null)
        {
            var dict = new ConcurrentDictionary<string, object>();
            var argumentInfo = targetType == null ? new List<ArgumentInfo>() : GetArgumentInfo(targetType);

            foreach (var arg in argumentList)
            {
                var info = argumentInfo.SingleOrDefault(i => i.ShortName.ToString(CultureInfo.InvariantCulture) == arg.Key || i.LongName == arg.Key);

                if (info != default(ArgumentInfo))
                {
                    bool added = false;

                    foreach (var k in new[] { info.ShortName.ToString(CultureInfo.InvariantCulture), info.LongName })
                    {
                        if (dict.ContainsKey(k))
                        {
                            dict.AddOrUpdate(k, arg.Value, (key, existingValue) => info.IsCollection ? ((List<object>)existingValue).Concat(new[] { arg.Value }).ToList() : (object)arg.Value);
                            added = true;
                            break;
                        }
                    }

                    if (!added)
                    {
                        dict.TryAdd(arg.Key, info.IsCollection ? new List<object>(new[] { arg.Value }) : (object)arg.Value);
                    }
                }
                else
                {
                    dict.AddOrUpdate(arg.Key, arg.Value, (key, existingValue) => arg.Value);
                }
            }

            return dict.ToDictionary(a => a.Key, a => a.Value);
        }

        private static List<KeyValuePair<string, string>> GetArgumentList(string commandLineString)
        {
            var argumentList = new List<KeyValuePair<string, string>>();

            foreach (Match match in Regex.Matches(commandLineString, ArgumentRegEx))
            {
                // the first match of the regular expression used to parse the string will contain the argument name, if one was matched.
                if (match.Groups[1].Value == default || string.IsNullOrEmpty(match.Groups[1].Value))
                {
                    continue;
                }

                string fullMatch = match.Groups[0].Value;
                string argument = match.Groups[1].Value;
                string value = match.Groups[2].Value;

                value = value.TrimOuterQuotes();

                // check to see if the argument uses a single dash. if so, split the argument name into a char array and add each
                // to the list. if a value is specified, it belongs to the final character.
                if (Regex.IsMatch(fullMatch, GroupRegEx))
                {
                    char[] charArray = argument.ToCharArray();

                    // iterate over the characters backwards to more easily assign the value
                    for (int i = 0; i < charArray.Length; i++)
                    {
                        argumentList.Add(new KeyValuePair<string, string>(charArray[i].ToString(CultureInfo.InvariantCulture), i == charArray.Length - 1 ? value : string.Empty));
                    }
                }
                else
                {
                    argumentList.Add(new KeyValuePair<string, string>(argument, value));
                }
            }

            return argumentList;
        }

        private static Dictionary<string, PropertyInfo> GetArgumentProperties(Type type)
        {
            Dictionary<string, PropertyInfo> properties = new Dictionary<string, PropertyInfo>();

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static))
            {
                // attempt to fetch the ArgumentAttribute of the property
                CustomAttributeData attribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == typeof(CommandLineArgumentAttribute).Name);

                // if found, extract the Name property and add it to the dictionary
                if (attribute != default(CustomAttributeData))
                {
                    char shortName = (char)attribute.ConstructorArguments[0].Value;
                    string longName = (string)attribute.ConstructorArguments[1].Value;

                    if (!properties.ContainsKey(shortName.ToString(CultureInfo.InvariantCulture)) && !properties.ContainsKey(longName))
                    {
                        properties.Add(shortName.ToString(CultureInfo.InvariantCulture), property);
                        properties.Add(longName, property);
                    }
                }
            }

            return properties;
        }

        private static List<string> GetOperandList(string commandLineString)
        {
            List<string> operands = new List<string>();

            foreach (Match match in Regex.Matches(commandLineString, ArgumentRegEx))
            {
                // the 3rd match of the regular expression used to parse the string will contain the operand, if one was matched.
                if (match.Groups[3].Value == default || string.IsNullOrEmpty(match.Groups[3].Value))
                {
                    continue;
                }

                string operand = match.Groups[3].Value;

                operands.Add(operand.TrimOuterQuotes());
            }

            return operands;
        }

        private static List<string> GetOperandListStrict(string operandListString)
        {
            List<string> operands = new List<string>();

            foreach (Match match in Regex.Matches(operandListString, OperandRegEx))
            {
                operands.Add(match.Groups[0].Value);
            }

            return operands;
        }

        private static PropertyInfo GetOperandsProperty(Type type)
        {
            PropertyInfo property = type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(p => p.CustomAttributes
                    .Any(a => a.AttributeType.Name == typeof(OperandsAttribute).Name));

            if (property != default && property.PropertyType != typeof(string[]) && property.PropertyType != typeof(List<string>))
            {
                throw new InvalidCastException("The target for the Operands attribute must be of string[] or List<string>.");
            }

            return property;
        }
    }

    /// <summary>
    ///     Indicates that the property is to be used as the target for automatic population of command line operands when invoking
    ///     the <see cref="CommandLineArguments.Populate(string, bool, string)"/> method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class OperandsAttribute : Attribute
    {
    }
}