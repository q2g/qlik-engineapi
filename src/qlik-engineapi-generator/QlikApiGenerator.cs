namespace QlikApiParser
{
    #region Usings
    using System;
    using System.IO;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NLog;
    using System.Linq;
    using System.Collections.Generic;
    using Newtonsoft.Json.Serialization;
    using System.Text;
    using System.ComponentModel;
    #endregion

    public class QlikApiGenerator
    {
        #region Logger
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region  private methods
        private T GetValueFromProperty<T>(JObject jObject, string name)
        {
            var children = jObject.Children();
            foreach (var child in children)
            {
                var jProperty = child as JProperty;
                if (jProperty.Name == name)
                    return jProperty.First.Value<T>();
            }

            return default(T);
        }

        private string IndentedText(string value, int layer)
        {
            if (layer <= 0)
                return value;
            var def = "    ";
            var indent = String.Empty;
            for (int i = 0; i < layer; i++)
                indent += def;
            return $"{indent}{value}";
        }

        private string GetParentName(JProperty token)
        {
            var parent = token?.Parent?.Parent as JProperty;
            if (parent == null)
                return null;
            return parent.Name;
        }

        private Dictionary<string, int> GetEnumValues(JObject token)
        {
            var children = token.Children();
            var results = new Dictionary<string, int>();
            foreach (var child in children)
            {
                var jProperty = child as JProperty;
                if (jProperty.Name != "type")
                    results.Add(jProperty.Name, jProperty.First["value"].Value<int>());
            }

            return results;
        }

        private string GetFormatedDescription(string description, int layer)
        {
            if (String.IsNullOrEmpty(description))
                return null;

            var desc = description;
            desc = desc.Replace("&lt;", "<");
            desc = desc.Replace("&gt;", ">");
            desc = desc.Replace("\r\n", $"\r\n{IndentedText("/// ", layer)}");
            desc = desc.Replace("\n", $"\r\n{IndentedText("/// ", layer)}");
            return desc;
        }

        private string GetFormatedEnumBlock(string name, Dictionary<string, int> values)
        {
            var builder = new StringBuilder();
            builder.Append(IndentedText($"public enum {name}\r\n", 1));
            builder.AppendLine(IndentedText("{", 1));
            foreach (var enumValue in values)
            {
                var eValue = enumValue.Value.ToString();
                if (eValue == "-999999")
                    eValue = null;
                else
                    eValue = $" = {eValue}";

                builder.AppendLine(IndentedText($"{enumValue.Key}{eValue},", 2));
            }
            builder.AppendLine(IndentedText("}", 1));
            return builder.ToString().TrimEnd(',');
        }

        private string GetDotNetType(string type)
        {
            switch (type)
            {
                case "integer":
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
        #endregion

        #region public methods
        public Dictionary<string, EngineObject> Parse(JObject mergeObject)
        {
            try
            {
                var definitionsToken = mergeObject["definitions"] as JObject;
                var definitions = new Dictionary<string, EngineObject>();
                foreach (var child in definitionsToken.Children())
                {
                    var jProperty = child as JProperty;
                    foreach (var subChild in child.Children())
                    {
                        logger.Debug($"Object name: {jProperty.Name}");
                        var jObject = subChild as JObject;
                        var engineObject = jObject.ToObject<EngineObject>();

                        if (engineObject.Export == false)
                            continue;

                        definitions.Add(jProperty.Name, engineObject);
                        var properties = GetValueFromProperty<JToken>(jObject, "properties");
                        if (properties == null)
                            properties = GetValueFromProperty<JToken>(jObject, "items");

                        if (properties == null)
                            if (engineObject.Type == "enum")
                            {
                                logger.Debug("No properties, but enum found.");
                                engineObject.Enums = GetEnumValues(jObject);
                            }
                            else
                            {
                                logger.Debug("No properties found. EMPTY");
                            }
                        else
                        {
                            foreach (var prop in properties)
                            {
                                var jPropProperty = prop as JProperty;
                                var propName = jPropProperty.Name;
                                logger.Debug($"Parameter name: {propName}");
                                var parameter = new EngineParameter();
                                if (propName == "$ref")
                                {
                                    parameter.Name = GetParentName(jPropProperty);
                                    parameter.Ref = jPropProperty.First.Value<string>();
                                    logger.Debug($"REF: {parameter.Ref}");
                                }

                                var jSubObject = prop.First as JObject;
                                if (jSubObject != null)
                                {
                                    parameter = jSubObject.ToObject<EngineParameter>();
                                    parameter.Name = propName;
                                    parameter.Ref = GetValueFromProperty<string>(jSubObject, "$ref");
                                    if (parameter.Description != null && parameter.Description.Contains("The default value is"))
                                        parameter.DefaultValueFromDescription = parameter.Default;

                                    if (parameter.Enum != null)
                                        parameter.Type = parameter.GenerateEnumType();

                                    if (parameter.Type == "array")
                                    {
                                        var arrayType = prop.First["items"]?.First?.First?.Value<string>() ?? null;
                                        if (arrayType.StartsWith("#"))
                                            arrayType = arrayType?.Split('/')?.LastOrDefault() ?? null;
                                        parameter.ArrayType = arrayType;
                                    }
                                }

                                engineObject.Parameters.Add(parameter);
                            }
                        }
                    }
                }

                return definitions;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The json could not parse.");
                return null;
            }
        }

        public void SaveToCSharp(Dictionary<string, EngineObject> definitions, string workDir, string name)
        {
            try
            {
                var enumList = new List<string>();
                var fileContent = new StringBuilder();
                fileContent.Append($"namespace {name}");
                fileContent.AppendLine();
                fileContent.AppendLine("{");
                fileContent.AppendLine(IndentedText("#region Usings", 1));
                fileContent.AppendLine(IndentedText("using System;", 1));
                fileContent.AppendLine(IndentedText("using System.ComponentModel;", 1));
                fileContent.AppendLine(IndentedText("using System.Collections.Generic;", 1));
                fileContent.AppendLine(IndentedText("using Newtonsoft.Json;", 1));
                fileContent.AppendLine(IndentedText("using Newtonsoft.Json.Linq;", 1));
                fileContent.AppendLine(IndentedText("#endregion", 1));
                fileContent.AppendLine();
                fileContent.AppendLine(IndentedText("#region Enums", 1));
                fileContent.AppendLine("<###ENUMS###>");
                fileContent.AppendLine(IndentedText("#endregion", 1));
                fileContent.AppendLine();

                var classCount = 0;
                foreach (var defObject in definitions)
                {
                    if (defObject.Value.Export == false)
                        continue;

                    var oType = defObject.Value.Type;
                    switch (oType)
                    {
                        case "object":
                        case "array":
                            classCount++;
                            fileContent.AppendLine(IndentedText($"public class {defObject.Key}<###implements###>", 1));
                            fileContent.AppendLine(IndentedText("{<###classopen###>", 1));
                            fileContent.AppendLine(IndentedText("<###region Properties###>", 2));
                            var paraCount = 0;
                            var implements = false;
                            foreach (var parameter in defObject.Value.Parameters)
                            {
                                paraCount++;
                                if (!String.IsNullOrEmpty(parameter.Description))
                                {
                                    fileContent.AppendLine(IndentedText("/// <summary>", 2));
                                    fileContent.AppendLine(IndentedText($"/// {GetFormatedDescription(parameter.Description, 2)}", 2));
                                    fileContent.AppendLine(IndentedText("/// </summary>", 2));
                                }

                                var dValue = String.Empty;
                                if (parameter.Default != null)
                                {
                                    dValue = parameter.Default.ToLowerInvariant();
                                    if (parameter.Type.EndsWith("_ENUM"))
                                        dValue = parameter.Default.ToUpperInvariant();
                                    fileContent.AppendLine(IndentedText($"[DefaultValue({dValue})]", 2));
                                }

                                if (oType == "array")
                                {
                                    var arrayType = parameter?.Ref?.Split('/')?.LastOrDefault() ?? null;
                                    fileContent.Replace("<###implements###>", $" : List<{GetDotNetType(arrayType)}> {{");
                                    fileContent.Replace("<###region Properties###>", "");
                                    fileContent.Replace("{<###classopen###>", "");
                                    implements = true;
                                }
                                else if (parameter.Type == "array")
                                {
                                    fileContent.AppendLine(IndentedText($"public List<{GetDotNetType(parameter.ArrayType)}> {parameter.Name} {{ get; set; }}", 2));
                                }
                                else
                                {
                                    var propertyText = IndentedText($"public {GetDotNetType(parameter.Type)} {parameter.Name} {{ get; set; }}", 2);
                                    if (parameter.Default != null)
                                        propertyText += $" = {dValue};";
                                    fileContent.AppendLine(propertyText);
                                }

                                fileContent.Replace("<###implements###>", "");
                                fileContent.Replace("<###classopen###>", "");
                                fileContent.Replace("<###region Properties###>", "#region Properties");

                                if (parameter.Enum != null)
                                {
                                    var dict = parameter.Enum.ToDictionary(x => x, x => -999999);
                                    var block = GetFormatedEnumBlock(parameter.Type, dict);
                                    fileContent.Replace("<###ENUMS###>", $"<###ENUMS###>{block}\r\n");
                                }

                                if (paraCount < defObject.Value.Parameters.Count)
                                    fileContent.AppendLine();
                            }

                            if (!implements)
                            {
                                fileContent.AppendLine(IndentedText("#endregion", 2));
                            }
                            fileContent.AppendLine(IndentedText("}", 1));

                            if (classCount < definitions.Count)
                                fileContent.AppendLine();
                            break;
                        case "enum":
                            var enumBlock = GetFormatedEnumBlock(defObject.Key, defObject.Value.Enums);
                            fileContent.Replace("<###ENUMS###>", $"<###ENUMS###>{enumBlock}\r\n");
                            break;
                        default:
                            logger.Error($"Unknown type {oType}");
                            break;
                    }
                }

                fileContent.Replace("<###ENUMS###>", "");
                fileContent.AppendLine("}");
                var savePath = Path.Combine(workDir, $"{name}.cs");
                var content = fileContent.ToString().Trim();
                content = content.Replace("\r\n    \r\n        \r\n\r\n", "\r\n\r\n");
                content = content.Replace("\n    \n        \n\n", "\n\n");
                File.WriteAllText(savePath, content);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        public void SaveToTypeScript()
        {

        }
        #endregion
    }

    #region  Helper Classes
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineObject
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public bool Export { get; set; } = true;
        public List<EngineParameter> Parameters { get; set; } = new List<EngineParameter>();
        public Dictionary<string, int> Enums { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineParameter
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Format { get; set; }
        public string Type { get; set; }
        public string Default { get; set; }
        public bool Required { get; set; }
        public List<string> Enum { get; set; }
        public string ArrayType { get; set; }

        public string DefaultValueFromDescription { get; set; }
        public string Ref { get; set; }

        public string GenerateEnumType()
        {
            return $"{Name.ToUpperInvariant()}_ENUM";
        }

        public override string ToString()
        {
            return Name;
        }
    }
    #endregion
}