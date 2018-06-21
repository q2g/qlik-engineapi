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

        private string GetParentName(JProperty token)
        {
            var parent = token?.Parent?.Parent as JProperty;
            if (parent == null)
                return null;
            return parent.Name;
        }

        private string GetEnumDefault(string type, string defaultValue)
        {
            var enumObject = EngineObjects.FirstOrDefault(e => e.EngType == EngineType.ENUM && e.Name == type) as EngineEnum;
            if (enumObject == null)
                enumObject = EngineObjects.FirstOrDefault(e => e.EngType == EngineType.ENUM && type.StartsWith(e.Name)) as EngineEnum;

            foreach (var value in enumObject.Values)
            {
                if (defaultValue.EndsWith(value.Name))
                    return $"{enumObject.Name}.{value.Name}";
            }
            return $"{type}.{defaultValue}";
        }

        private string GetFormatedEnumBlock(EngineEnum enumObject)
        {
            enumObject.RenameValues();

            var builder = new StringBuilder();
            builder.Append(QlikApiUtils.Indented($"public enum {enumObject.Name}\r\n", 1));
            builder.AppendLine(QlikApiUtils.Indented("{", 1));
            foreach (var enumValue in enumObject.Values)
            {
                var eValue = enumValue.Name;
                if (enumValue.Value.HasValue)
                    eValue += $" = {enumValue.Value}";
                builder.AppendLine(QlikApiUtils.Indented($"{eValue},", 2));
            }
            builder.AppendLine(QlikApiUtils.Indented("}", 1));
            return builder.ToString().TrimEnd(',').TrimEnd();
        }

        private EngineEnum EnumExists(EngineEnum engineEnum)
        {
            var results = EngineObjects.Where(o => o.EngType == engineEnum.EngType &&
                                                   o.Name.StartsWith(engineEnum.Name)).ToList();
            if (results.Count == 0)
                return null;

            var hitCount = 0;
            EngineEnum currentEnum = null;
            foreach (EngineEnum item in results)
            {
                currentEnum = item;
                foreach (var enumValue in engineEnum.Values)
                {
                    var hit = item.Values.FirstOrDefault(v => v.Name == enumValue.Name);
                    if (hit != null)
                        hitCount++;
                }
            }

            if (hitCount == engineEnum.Values.Count)
                return currentEnum;
            return null;
        }

        public string GenerateEnumType(string name)
        {
            var exitingEnum = EngineObjects.Where(e => e.Name.StartsWith(name) && e.EngType == EngineType.ENUM).ToList();
            if (exitingEnum.Count == 0)
                return $"{name}";
            return $"{name}_{exitingEnum.Count}";
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
                        engineProperty.Type = GenerateEnumType(engineProperty.Name);
                        engineProperty.IsEnumType = true;
                        var engineEnum = new EngineEnum();
                        engineEnum.Name = engineProperty.Name;
                        foreach (var enumValue in engineProperty.Enum)
                            engineEnum.Values.Add(new EngineEnumValue() { Name = enumValue });
                        var enumResult = EnumExists(engineEnum);
                        if (enumResult == null)
                        {
                            EngineObjects.Add(engineEnum);
                            engineEnum.Name = engineProperty.Type;
                        }
                        else 
                           engineProperty.Type = enumResult.Name;
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
            return $" : List<{QlikApiUtils.GetDotNetType(arrayType)}>";
        }

        private string GetFormatedMethod(EngineMethod method)
        {
            var response = method.Responses.FirstOrDefault() ?? null;
            var descBuilder = new DescritpionBuilder()
            {
                Summary = method.Description,
                SeeAlso = method.SeeAlso,
                Param = method.Parameters,
            };

            if (response != null)
                descBuilder.Return = response.Description;
            var description = descBuilder.Generate(2);
            var returnType = "void";
            if (response != null)
            {
                returnType = QlikApiUtils.GetDotNetType(response.GetRealType());
                if (method?.Responses?.Count > 1)
                {
                    logger.Debug($"The method {method?.Name} has {method?.Responses?.Count} responses.");
                    returnType = "JObject";
                }
            }
            var parameter = new StringBuilder();
            if (method.Parameters.Count > 0)
            {
                foreach (var para in method.Parameters)
                    parameter.Append($"{QlikApiUtils.GetDotNetType(para.Type)} {para.Name}, ");
            }
            var methodBuilder = new StringBuilder();
            methodBuilder.AppendLine(description);
            methodBuilder.AppendLine(QlikApiUtils.Indented($"{returnType} {method.Name}({parameter.ToString().TrimEnd().TrimEnd(',')});", 2));
            return methodBuilder.ToString();
        }

        private void AddDefinitions(JObject mergeObject)
        {
            try
            {
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
                                var properties = ReadProperties(jObject, "properties");
                                engineClass.Properties.AddRange(properties);
                                EngineObjects.Add(engineClass);
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
                                if (EnumExists(engineEnum) == null)
                                    EngineObjects.Add(engineEnum);
                                break;
                            default:
                                logger.Error($"Unknown object type {objectType}");
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The definitions could not be added.");
            }
        }

        private void AddMethods(JObject mergeObject)
        {
            try
            {
                var classes = mergeObject["services"] as JObject;
                foreach (var child in classes.Children())
                {
                    var jProperty = child as JProperty;
                    foreach (var subChild in child.Children())
                    {
                        logger.Debug($"Class name: {jProperty.Name}");
                        dynamic jObject = subChild as JObject;
                        var export = jObject?.export?.ToObject<bool>() ?? true;
                        if (!export)
                            continue;

                        var engineInterface = jObject.ToObject<EngineInterface>();
                        engineInterface.Name = jProperty.Name;
                        EngineObjects.Add(engineInterface);
                        IEnumerable<JToken> methods = jObject?.methods?.Children() ?? null;
                        if (methods != null)
                        {
                            foreach (var method in methods)
                            {
                                var methodProp = method as JProperty;
                                logger.Debug($"Method name: {methodProp.Name}");
                                var engineMethod = method.First.ToObject<EngineMethod>();
                                engineMethod.Name = methodProp.Name;
                                var seeAlsoObject = method.First as JObject;
                                if (seeAlsoObject != null)
                                    engineMethod.SeeAlso = GetValueFromProperty<List<string>>(seeAlsoObject, "x-qlik-see-also");
                                foreach (var para in engineMethod.Parameters)
                                {
                                    para.Type = para.GetRealType();
                                    var enumList = para.GetEnums();
                                    foreach (var item in enumList)
                                    {
                                        var enumValue = EnumExists(item);
                                        if (enumValue == null)
                                            EngineObjects.Add(item);
                                        else
                                            para.Type = enumValue.Name;
                                    }
                                }
                                engineInterface.Methods.Add(engineMethod);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The methods could not be added.");
            }
        }
        #endregion

        #region public methods
        public void ReadJson(JObject mergeObject)
        {
            try
            {
                EngineObjects.Clear();
                AddDefinitions(mergeObject);
                AddMethods(mergeObject);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The json could not parse.");
            }
        }

        public void SaveToCSharp(string workDir, string name)
        {
            try
            {
                var enumList = new List<string>();
                var fileContent = new StringBuilder();
                fileContent.Append($"namespace {name}");
                fileContent.AppendLine();
                fileContent.AppendLine("{");
                fileContent.AppendLine(QlikApiUtils.Indented("#region Usings", 1));
                fileContent.AppendLine(QlikApiUtils.Indented("using System;", 1));
                fileContent.AppendLine(QlikApiUtils.Indented("using System.ComponentModel;", 1));
                fileContent.AppendLine(QlikApiUtils.Indented("using System.Collections.Generic;", 1));
                fileContent.AppendLine(QlikApiUtils.Indented("using Newtonsoft.Json;", 1));
                fileContent.AppendLine(QlikApiUtils.Indented("using Newtonsoft.Json.Linq;", 1));
                fileContent.AppendLine(QlikApiUtils.Indented("#endregion", 1));
                fileContent.AppendLine();
                fileContent.AppendLine(QlikApiUtils.Indented("#region Enums", 1));
                var enumObjects = EngineObjects.Where(d => d.EngType == EngineType.ENUM).ToList();
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
                fileContent.AppendLine(QlikApiUtils.Indented("#endregion", 1));
                fileContent.AppendLine();
                var interfaceObjects = EngineObjects.Where(d => d.EngType == EngineType.INTERFACE).ToList();
                logger.Debug($"Write Interfaces {interfaceObjects.Count}");
                fileContent.AppendLine(QlikApiUtils.Indented("#region Interfaces", 1));
                lineCounter = 0;
                foreach (EngineInterface interfaceObject in interfaceObjects)
                {
                    lineCounter++;
                    fileContent.AppendLine(QlikApiUtils.Indented($"public interface {interfaceObject.Name}", 1));
                    fileContent.AppendLine(QlikApiUtils.Indented("{", 1));
                    foreach (var methodObject in interfaceObject.Methods)
                        fileContent.AppendLine(GetFormatedMethod(methodObject));
                    fileContent.AppendLine(QlikApiUtils.Indented("}", 1));
                    if (lineCounter < interfaceObjects.Count)
                        fileContent.AppendLine();
                }
                fileContent.AppendLine(QlikApiUtils.Indented("#endregion", 2));
                fileContent.AppendLine();
                fileContent.AppendLine(QlikApiUtils.Indented("#region Classes", 1));
                var classObjects = EngineObjects.Where(d => d.EngType == EngineType.CLASS).ToList();
                logger.Debug($"Write Classes {classObjects.Count}");
                lineCounter = 0;
                foreach (EngineClass classObject in classObjects)
                {
                    lineCounter++;
                    fileContent.AppendLine(QlikApiUtils.Indented($"public class {classObject.Name}<###implements###>", 1));
                    fileContent.AppendLine(QlikApiUtils.Indented("{", 1));
                    if (classObject.Properties.Count > 0)
                    {
                        fileContent.AppendLine(QlikApiUtils.Indented("#region Properties", 2));
                        var propertyCount = 0;
                        foreach (var property in classObject.Properties)
                        {
                            propertyCount++;
                            if (!String.IsNullOrEmpty(property.Description))
                            {
                                var builder = new DescritpionBuilder()
                                {
                                    Summary = property.Description,
                                };
                                fileContent.AppendLine(builder.Generate(2));
                            }

                            var dValue = String.Empty;
                            if (property.Default != null)
                            {
                                dValue = property.Default.ToLowerInvariant();
                                if (property.IsEnumType)
                                    dValue = GetEnumDefault(property.Type, property.Default);
                                fileContent.AppendLine(QlikApiUtils.Indented($"[DefaultValue({dValue})]", 2));
                            }

                            if (classObject.Type == "array")
                            {
                                var implements = GetImplemention(property);
                                fileContent.Replace("<###implements###>", implements);
                            }
                            else if (property.Type == "array")
                            {
                                var refType = property.GetRefType();
                                fileContent.AppendLine(QlikApiUtils.Indented($"public List<{QlikApiUtils.GetDotNetType(refType)}> {property.Name} {{ get; set; }}", 2));
                            }
                            else
                            {
                                var propertyText = QlikApiUtils.Indented($"public {QlikApiUtils.GetDotNetType(property.Type)} {property.Name} {{ get; set; }}", 2);
                                if (property.Default != null)
                                    propertyText += $" = {dValue};";
                                fileContent.AppendLine(propertyText);
                            }

                            if (propertyCount < classObject.Properties.Count)
                                fileContent.AppendLine();
                        }
                        fileContent.AppendLine(QlikApiUtils.Indented("#endregion", 2));
                    }
                    fileContent.AppendLine(QlikApiUtils.Indented("}", 1));
                    if (lineCounter < classObjects.Count)
                        fileContent.AppendLine();
                }
                fileContent.Replace("<###implements###>", "");
                fileContent.AppendLine(QlikApiUtils.Indented("#endregion", 1));
                fileContent.AppendLine("}");
                var savePath = Path.Combine(workDir, $"{name}.cs");
                var content = fileContent.ToString().Trim();
                File.WriteAllText(savePath, content);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The .NET file could not create.");
            }
        }
        #endregion
    }
}