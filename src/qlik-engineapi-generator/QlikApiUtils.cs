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
            switch (type)
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
                default:
                    return type;
            }
        }
    }
}