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

        #region  Properties && Variables
        private List<IEngineObject> EngineObjects = new List<IEngineObject>();
        #endregion

        #region  private methods
        private T GetValueFromProperty<T>(JObject jObject, string name)
        {
            var children = jObject.Children();
            foreach (var child in children)
            {
                var jProperty = child as JProperty;
                if (jProperty.Name == name)
                    return jProperty.First.ToObject<T>();
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

        private string GetFormatedEnumBlock(EngineEnum enumObject)
        {
            var builder = new StringBuilder();
            builder.Append(IndentedText($"public enum {enumObject.Name}\r\n", 1));
            builder.AppendLine(IndentedText("{", 1));
            foreach (var enumValue in enumObject.Values)
            {
                var eValue = enumValue.Name;
                if (enumValue.Value.HasValue)
                    eValue += $" = {eValue}";
                builder.AppendLine(IndentedText($"{eValue},", 2));
            }
            builder.AppendLine(IndentedText("}", 1));
            return builder.ToString().TrimEnd(',').TrimEnd();
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

        private List<EngineProperty> ReadProperties(JObject jObject, string tokenName)
        {
            var results = new List<EngineProperty>();
            try
            {
                var properties = GetValueFromProperty<JToken>(jObject, tokenName);
                if (properties == null)
                    return results;
                foreach (var property in properties)
                {
                    var jprop = property as JProperty;
                    logger.Debug($"Property name: {jprop.Name}");
                    var engineProperty = new EngineProperty();
                    dynamic propObject = null;
                    if (property.First.Type == JTokenType.Object)
                    {
                        propObject = property.First as JObject;
                        engineProperty = propObject.ToObject<EngineProperty>();
                    }
                    engineProperty.Name = jprop.Name;
                    if (engineProperty.Description != null && engineProperty.Description.Contains("The default value is"))
                        engineProperty.DefaultValueFromDescription = engineProperty.Default;
                    if (jprop.Name == "$ref")
                    {
                        var refLink = jprop?.Value?.ToObject<string>() ?? null;
                        logger.Debug($"Items Ref: {refLink}");
                        engineProperty.Ref = refLink;
                    }

                    if (engineProperty.Type == "array")
                    {
                        var refValue = GetValueFromProperty<string>(propObject.items, "$ref");
                        if (String.IsNullOrEmpty(refValue))
                            refValue = propObject.items.type.ToObject<string>();
                        engineProperty.Ref = refValue;
                    }

                    if (engineProperty.Enum != null)
                    {
                        engineProperty.Type = engineProperty.GenerateEnumType();
                        engineProperty.IsEnumType = true;
                        var engineEnum = new EngineEnum();
                        engineEnum.Name = engineProperty.Type;
                        foreach (var enumValue in engineProperty.Enum)
                            engineEnum.Values.Add(new EngineEnumValue() { Name = enumValue });
                        EngineObjects.Add(engineEnum);
                    }

                    results.Add(engineProperty);
                }
                return results;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method {nameof(ReadProperties)} failed.");
                return results;
            }
        }

        private List<EngineEnumValue> GetEnumValues(JObject token)
        {
            var results = new List<EngineEnumValue>();
            try
            {
                var children = token.Children();
                foreach (var child in children)
                {
                    dynamic jProperty = child as JProperty;
                    if (jProperty.Name != "type")
                    {
                        EngineEnumValue enumValue = jProperty?.Value?.ToObject<EngineEnumValue>() ?? null;
                        enumValue.Name = jProperty.Name;
                        results.Add(enumValue);
                    }
                }
                return results;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method {nameof(GetEnumValues)} failed.");
                return results;
            }
        }

        private string GetImplemention(EngineProperty property)
        {
            var result = new StringBuilder();
            var arrayType = property?.Ref?.Split('/')?.LastOrDefault() ?? null;    
            return  $" : List<{GetDotNetType(arrayType)}>";
        }
        #endregion

        #region public methods
        public List<IEngineObject> Parse(JObject mergeObject)
        {
            try
            {
                EngineObjects.Clear();
                var definitions = mergeObject["definitions"] as JObject;
                foreach (var child in definitions.Children())
                {
                    var jProperty = child as JProperty;
                    foreach (var subChild in child.Children())
                    {
                        logger.Debug($"Object name: {jProperty.Name}");
                        dynamic jObject = subChild as JObject;
                        var export = jObject?.export?.ToObject<bool>() ?? true;
                        if (!export)
                            continue;
                        var objectType = jObject?.type?.ToString() ?? null;
                        EngineClass engineClass = null;
                        switch (objectType)
                        {
                            case "object":
                                engineClass = jObject.ToObject<EngineClass>();
                                engineClass.Name = jProperty.Name;
                                EngineObjects.Add(engineClass);
                                var properties = ReadProperties(jObject, "properties");
                                engineClass.Properties.AddRange(properties);
                                break;
                            case "array":
                                engineClass = jObject.ToObject<EngineClass>();
                                engineClass.Name = jProperty.Name;
                                var arrays = ReadProperties(jObject, "items");
                                engineClass.Properties.AddRange(arrays);
                                EngineObjects.Add(engineClass);
                                break;
                            case "enum":
                                EngineEnum engineEnum = jObject.ToObject<EngineEnum>();
                                engineEnum.Name = jProperty.Name;
                                var enums = GetEnumValues(jObject);
                                engineEnum.Values = enums;
                                EngineObjects.Add(engineEnum);
                                break;
                            default:
                                logger.Error($"Unknown object type {objectType}");
                                break;
                        }
                    }
                }

                return EngineObjects;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The json could not parse.");
                return null;
            }
        }

        public void SaveToCSharp(List<IEngineObject> definitions, string workDir, string name)
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
                var enumObjects = definitions.Where(d => d.EngType == EngineType.ENUM).ToList();
                logger.Debug($"Write Enums {enumObjects.Count}");
                var lineCounter = 0;
                foreach (EngineEnum enumValue in enumObjects)
                {
                    lineCounter++;
                    var enumResult = GetFormatedEnumBlock(enumValue);
                    fileContent.AppendLine(enumResult);
                    if (lineCounter < enumObjects.Count)
                        fileContent.AppendLine();
                }
                fileContent.AppendLine(IndentedText("#endregion", 1));
                fileContent.AppendLine();
                fileContent.AppendLine(IndentedText("#region Classes", 1));
                var classObjects = definitions.Where(d => d.EngType == EngineType.CLASS).ToList();
                logger.Debug($"Write Classes {classObjects.Count}");
                foreach (EngineClass classObject in classObjects)
                {
                    if (classObject.Name == "NxMetaDef")
                        logger.Debug(",mm,m,");

                    fileContent.AppendLine(IndentedText($"public class {classObject.Name}<###implements###>", 1));
                    fileContent.AppendLine(IndentedText("{", 1));
                    fileContent.AppendLine(IndentedText("#region Properties", 2));
                    var propertyCount = 0;
                    foreach (var property in classObject.Properties)
                    {
                        propertyCount++;
                        if (!String.IsNullOrEmpty(property.Description))
                        {
                            fileContent.AppendLine(IndentedText("/// <summary>", 2));
                            fileContent.AppendLine(IndentedText($"/// {GetFormatedDescription(property.Description, 2)}", 2));
                            fileContent.AppendLine(IndentedText("/// </summary>", 2));
                        }

                        var dValue = String.Empty;
                        if (property.Default != null)
                        {
                            dValue = property.Default.ToLowerInvariant();
                            if (property.IsEnumType)
                                dValue = property.Default.ToUpperInvariant();
                            fileContent.AppendLine(IndentedText($"[DefaultValue({dValue})]", 2));
                        }

                        if (classObject.Type == "array")
                        {
                            var implements = GetImplemention(property);
                            fileContent.Replace("<###implements###>", implements);
                        }

                        if (property.Type == "array")
                        {
                            var refType = property.GetRefType();
                            fileContent.AppendLine(IndentedText($"public List<{GetDotNetType(refType)}> {property.Name} {{ get; set; }}", 2));
                        }
                        else
                        {
                            var propertyText = IndentedText($"public {GetDotNetType(property.Type)} {property.Name} {{ get; set; }}", 2);
                            if (property.Default != null)
                                propertyText += $" = {dValue};";
                            fileContent.AppendLine(propertyText);
                        }

                        fileContent.Replace("<###implements###>", "");
                        if (propertyCount < classObject.Properties.Count)
                            fileContent.AppendLine();
                    }

                    fileContent.AppendLine(IndentedText("#endregion", 2));
                    fileContent.AppendLine(IndentedText("}", 1));
                    fileContent.AppendLine();
                }

                fileContent.AppendLine("}");
                fileContent.AppendLine(IndentedText("#endregion", 1));
                var savePath = Path.Combine(workDir, $"{name}.cs");
                var content = fileContent.ToString().Trim();
                content = content.Replace("\r\n    \r\n        \r\n\r\n", "\r\n\r\n");
                content = content.Replace("\n    \n        \n\n", "\n\n");
                File.WriteAllText(savePath, content);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The .NET file could not create.");
            }
        }

        public void SaveToTypeScript()
        {

        }
        #endregion
    }
}