namespace QlikApiParser
{
    #region Usings
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System.Linq;
    using System.Collections.Generic;
    using Newtonsoft.Json.Serialization;
    using System.ComponentModel;
    using System.Text;
    #endregion

    public class QlikApiUtils
    {
        public static string Indented(string value, int layer)
        {
            if (layer <= 0)
                return value;
            var def = "    ";
            var indent = String.Empty;
            for (int i = 0; i < layer; i++)
                indent += def;
            return $"{indent}{value}";
        }

        public static string GetDotNetType(string type)
        {
            switch (type?.ToLowerInvariant())
            {
                case "integer":
                case "int8":
                    return "int";
                case "boolean":
                    return "bool";
                case "number":
                    return "double";
                case "object":
                    return "JObject";
                case "JsonObject":
                    return "JObject";
                case "nan":
                    return "null";
                case "-1e+300":
                    return "double.NaN";

                default:
                    return type;
            }
        }

        public static string GetDefaultValue(string type, string defaultValue)
        {
            if(!String.IsNullOrEmpty(defaultValue))
              return defaultValue;
            
            switch (type?.ToLowerInvariant())
            {
                case "integer":
                case "int8":
                    return "0";
                case "boolean":
                    return "false";
                case "number":
                    return "0.0D";
                case "object":
                    return "null";
                case "nan":
                    return "null";
                case "string":
                    return "null";
                case "-1e+300":
                    return "0.0D";
                default:
                    return "null";
            }
        }
    }
}