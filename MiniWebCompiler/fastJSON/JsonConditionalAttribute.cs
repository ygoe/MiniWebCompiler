using System;
using System.Collections;

namespace fastJSON
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class JsonConditionalAttribute : Attribute
    {
        /// <summary>
        /// Determines whether the property value shall be serialized.
        /// </summary>
        /// <param name="value">The current property value.</param>
        /// <returns>true, if the value shall be serialized; otherwise, false.</returns>
        public bool TestCondition(object value)
        {
            if (value == null)
                return false;   // null (is always default value)
            if (value.Equals(GetDefault(value.GetType())))
                return false;   // Default value
            if (typeof(IEnumerable).IsAssignableFrom(value.GetType()) &&
                !((IEnumerable)value).GetEnumerator().MoveNext())
                return false;   // Empty collection
            return true;
        }

        /// <summary>
        /// Gets the default value for the specified type. Note for value types: This value will be
        /// boxed and needs to be compared with the Equals method instead of the equality operator.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static object GetDefault(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }
    }
}
