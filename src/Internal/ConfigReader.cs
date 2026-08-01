using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace NoBrute.Internal
{
    /// <summary>
    /// Null tolerant helper to read values from an <see cref="IConfiguration"/> section.
    /// The built in <c>GetValue</c> extensions throw if a section is not present at all,
    /// which makes optional (and mocked) configuration sections hard to handle.
    /// </summary>
    internal static class ConfigReader
    {
        /// <summary>
        /// Reads a single value from the given section.
        /// </summary>
        /// <typeparam name="T">Type of the value.</typeparam>
        /// <param name="section">The parent section. May be <c>null</c>.</param>
        /// <param name="key">The key to read.</param>
        /// <param name="defaultValue">Value returned if the key is missing or cannot be converted.</param>
        public static T Read<T>(IConfiguration section, string key, T defaultValue)
        {
            IConfigurationSection child = section?.GetSection(key);

            return ReadValue(child?.Value, defaultValue);
        }

        /// <summary>
        /// Reads a list of strings. Supports both array style configuration
        /// (<c>"Headers": [ "A", "B" ]</c>) and comma separated values (<c>"Headers": "A,B"</c>).
        /// </summary>
        /// <param name="section">The parent section. May be <c>null</c>.</param>
        /// <param name="key">The key to read.</param>
        /// <param name="defaultValue">Value returned if the key is missing or empty.</param>
        public static IList<string> ReadStringList(IConfiguration section, string key, IList<string> defaultValue)
        {
            IConfigurationSection child = section?.GetSection(key);

            if (child == null)
            {
                return defaultValue;
            }

            if (!string.IsNullOrWhiteSpace(child.Value))
            {
                List<string> inlineValues = Split(child.Value);

                return inlineValues.Count > 0 ? inlineValues : defaultValue;
            }

            List<string> values = child.GetChildren()?
                .Select(entry => entry?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToList();

            return values != null && values.Count > 0 ? values : defaultValue;
        }

        private static List<string> Split(string value)
        {
            return value
                .Split(',')
                .Select(entry => entry.Trim())
                .Where(entry => entry.Length > 0)
                .ToList();
        }

        private static T ReadValue<T>(string raw, T defaultValue)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            try
            {
                TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));

                if (converter != null && converter.CanConvertFrom(typeof(string)))
                {
                    return (T)converter.ConvertFromInvariantString(raw.Trim());
                }
            }
            catch (Exception)
            {
                // Fall through to the default value: an unparsable entry must never take the application down.
            }

            return defaultValue;
        }
    }
}
