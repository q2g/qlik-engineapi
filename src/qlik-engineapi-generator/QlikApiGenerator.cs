namespace QlikApiParser
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;
    using NLog;
    #endregion

    public class QlikApiGenerator
    {
        #region Logger
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region  Properties && Variables
        private List<IEngineObject> EngineObjects = new List<IEngineObject>();
        private QlikApiConfig Config;
        #endregion

        public QlikApiGenerator(QlikApiConfig config)
        {
            Config = config;
        }

        #region  private methods
        private T GetValueFromProperty<T>(JObject jObject, string name)
        {
            if (jObject == null)
                return default(T);

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
            if (!(token?.Parent?.Parent is JProperty parent))
                return null;
            return parent.Name;
        }

        private string GetEnumDefault(string type, string defaultValue)
        {
            if (!(EngineObjects.FirstOrDefault(e => e.EngType == EngineType.ENUM && e.Name == type) is EngineEnum enumObject))
                enumObject = EngineObjects.FirstOrDefault(e => e.EngType == EngineType.ENUM && type.StartsWith(e.Name)) as EngineEnum;
            foreach (var value in enumObject.Values)
            {
                var enumName = value.Title;
                if (!String.IsNullOrEmpty(value.Description))
                    enumName = value.Description;
                if (defaultValue.EndsWith(enumName))
                    return $"{enumObject.Name}.{enumName}";
            }
            return null;
        }

        private string GetFormatedEnumBlock(EngineEnum enumObject)
        {
            var builder = new StringBuilder();
            builder.Append(QlikApiUtils.Indented($"public enum {enumObject.Name}\r\n", 1));
            builder.AppendLine(QlikApiUtils.Indented("{", 1));
            foreach (var enumValue in enumObject.Values)
            {
                if (!String.IsNullOrEmpty(enumValue.Description))
                {
                    if (enumValue.XQlikConst != null)
                        builder.AppendLine(QlikApiUtils.Indented($"{enumValue.Description} = {enumValue.XQlikConst.Value},", 2));
                    else
                        builder.AppendLine(QlikApiUtils.Indented($"{enumValue.Description},", 2));
                    builder.AppendLine(QlikApiUtils.Indented($"{enumValue.Title} = {enumValue.Description},", 2));
                }
                else
                    builder.AppendLine(QlikApiUtils.Indented($"{enumValue.Title},", 2));

                if (!String.IsNullOrEmpty(enumValue.ShotValue))
                {
                    var shotValue = $"{enumValue.ShotValue} = ";
                    builder.AppendLine(QlikApiUtils.Indented($"{shotValue}{enumValue.Name},", 2));
                }
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
                    {
                        hitCount++;
                        if (!String.IsNullOrEmpty(enumValue.ShotValue))
                            hit.ShotValue = enumValue.ShotValue;
                    }
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

        private List<EngineProperty> ReadProperties(JObject jObject, string tokenName, string className)
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
                        engineProperty.EnumShot = (propObject as JObject)["enumShort"] as JToken ?? null;
                    }
                    engineProperty.Name = jprop.Name;

                    if (engineProperty.Default == null)
                    {
                        switch (engineProperty.Type)
                        {
                            case "boolean":
                                engineProperty.Default = "false";
                                break;
                            case "integer":
                                engineProperty.Default = "0";
                                break;
                            case "double":
                                engineProperty.Default = "0";
                                break;
                            case "object":
                                engineProperty.Default = "null";
                                break;
                            default:
                                break;
                        }
                    }

                    if (engineProperty.Description != null && engineProperty.Description.Contains("The default value is"))
                    {
                        if (!String.IsNullOrEmpty(engineProperty.Default))
                            engineProperty.DefaultValueFromDescription = engineProperty.Default;
                        else
                            logger.Warn($"The default value was not found for the property: \"{engineProperty.Name}\" class: \"{className}\"");
                    }

                    var refValue = GetValueFromProperty<string>(propObject, "$ref");
                    if (!String.IsNullOrEmpty(refValue))
                        engineProperty.Ref = refValue;

                    if (jprop.Name == "$ref")
                    {
                        var refLink = jprop?.Value?.ToObject<string>() ?? null;
                        logger.Debug($"Items Ref: {refLink}");
                        engineProperty.Ref = refLink;
                    }

                    if (engineProperty.Type == "array")
                    {
                        refValue = GetValueFromProperty<string>(propObject.items, "$ref");
                        if (String.IsNullOrEmpty(refValue))
                            refValue = propObject.items.type.ToObject<string>();
                        engineProperty.Ref = refValue;
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

        private List<EngineEnumValue> GetEnumValues(JArray items)
        {
            var results = new List<EngineEnumValue>();
            try
            {
                foreach (var item in items)
                {
                    EngineEnumValue enumValue = item.ToObject<EngineEnumValue>() ?? null;
                    if (enumValue != null)
                        results.Add(enumValue);
                    else
                        throw new Exception("The enum value is null.");
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
            var descBuilder = new DescritpionBuilder(Config.UseDescription)
            {
                Summary = method.Description,
                SeeAlso = method.SeeAlso,
                Param = method.Parameters,
            };

            if (Config.GenerateCancelationToken)
                descBuilder.Param.Add(new EngineParameter() { Type = "CancellationToken?", Name = "token", Description = "Propagates notification that operations should be canceled." });

            if (response != null)
                descBuilder.Return = response.Description;
            var description = descBuilder.Generate(2);
            var returnType = "Task";
            if (response != null)
            {
                returnType = $"Task<{QlikApiUtils.GetDotNetType(response.GetRealType())}>";
                var serviceType = response.GetServiceType();
                if (serviceType != null)
                    returnType = $"Task<{serviceType}>";
                if (method?.Responses?.Count > 1 || !Config.UseQlikResponseLogic)
                {
                    logger.Debug($"The method {method?.Name} has {method?.Responses?.Count} responses.");
                    var resultClass = method.GetMultipleClass();
                    EngineObjects.Add(resultClass);
                    returnType = $"Task<{resultClass.Name}>";
                }
            }
            if (method.UseGeneric)
                returnType = "Task<T>";
            var methodBuilder = new StringBuilder();
            if (!String.IsNullOrEmpty(description))
                methodBuilder.AppendLine(description);
            var asyncValue = String.Empty;
            switch (Config.AsyncMode)
            {
                case AsyncMode.NONE:
                    asyncValue = String.Empty;
                    break;
                case AsyncMode.SHOW:
                    asyncValue = "Async";
                    break;
                default:
                    asyncValue = String.Empty;
                    break;
            }
            var cancellationToken = String.Empty;
            var parameter = new StringBuilder();
            if (method.Parameters.Count > 0)
            {
                //Sort parameters by required
                var parameters = method.Parameters.OrderBy(p => p.Required == false);
                foreach (var para in parameters)
                {
                    var defaultValue = String.Empty;
                    if (!para.Required)
                        defaultValue = $" = {QlikApiUtils.GetDefaultValue(para.Type, para.Default)}";

                    var type = para.Type;
                    if (para.Items != null)
                        type = para.GetEnumType();
                    parameter.Append($"{QlikApiUtils.GetDotNetType(type)} {para.Name}{defaultValue}, ");
                }
            }
            var parameterValue = parameter.ToString().TrimEnd().TrimEnd(',');
            if (method.Deprecated)
            {
                var obDesc = String.Empty;
                if (!String.IsNullOrEmpty(method.XQlikDeprecationDescription))
                    obDesc = $"(\"{method.XQlikDeprecationDescription}\")";
                methodBuilder.AppendLine(QlikApiUtils.Indented($"[ObsoleteAttribute{obDesc}]", 2));
            }
            var tvalue = String.Empty;
            if (method.UseGeneric)
                tvalue = "<T>";
            methodBuilder.AppendLine(QlikApiUtils.Indented($"{returnType} {method.Name}{asyncValue}{tvalue}({parameterValue}{cancellationToken});", 2));
            return methodBuilder.ToString();
        }

        private void AddDefinitions(JObject mergeObject)
        {
            try
            {
                var definitions = mergeObject.SelectToken("components.schemas") as JObject;
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

                                //special case for .NET JObject - JsonObject is ignored
                                if (engineClass.Name == "JsonObject")
                                {
                                    logger.Info("The class \"JsonObject\" is ignored because \"JObject\" already exists in the namespace Newtonsoft.");
                                    continue;
                                }

                                engineClass.SeeAlso = GetValueFromProperty<List<string>>(jObject, "x-qlik-see-also");
                                var properties = ReadProperties(jObject, "properties", engineClass.Name);
                                if (properties.Count == 0)
                                    logger.Info($"The Class \"{engineClass.Name}\" has no properties.");
                                engineClass.Properties.AddRange(properties);
                                EngineObjects.Add(engineClass);

                                //Special for ObjectInterface => Add IObjectInterface
                                if (engineClass.Name == Config.BaseObjectInterfaceClassName)
                                {
                                    var baseInterface = new EngineInterface()
                                    {
                                        Name = Config.BaseObjectInterfaceName,
                                        Description = "Generated Interface",
                                    };

                                    baseInterface.Properties.AddRange(engineClass.Properties);
                                    EngineObjects.Add(baseInterface);
                                }
                                break;
                            case "array":
                                engineClass = jObject.ToObject<EngineClass>();
                                engineClass.Name = jProperty.Name;
                                engineClass.SeeAlso = GetValueFromProperty<List<string>>(jObject, "x-qlik-see-also");
                                var arrays = ReadProperties(jObject, "items", engineClass.Name);
                                engineClass.Properties.AddRange(arrays);
                                EngineObjects.Add(engineClass);
                                break;
                            case "string":
                                EngineEnum engineEnum = jObject.ToObject<EngineEnum>();
                                engineEnum.Name = jProperty.Name;
                                var enums = GetEnumValues(jObject["oneOf"] as JArray);
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
                var classes = mergeObject.SelectToken("x-qlik-services") as JObject;
                foreach (var child in classes.Children())
                {
                    var jProperty = child as JProperty;
                    foreach (var subChild in child.Children())
                    {
                        logger.Debug($"Interface name: {jProperty.Name}");
                        dynamic jObject = subChild as JObject;
                        var export = jObject?.export?.ToObject<bool>() ?? true;
                        if (!export)
                            continue;

                        var engineInterface = jObject.ToObject<EngineInterface>();
                        engineInterface.Name = $"I{jProperty.Name}";
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
                                if (method.First is JObject seeAlsoObject)
                                    engineMethod.SeeAlso = GetValueFromProperty<List<string>>(seeAlsoObject, "x-qlik-see-also");
                                foreach (var para in engineMethod.Parameters)
                                    para.Type = para.GetRealType();
                                engineInterface.Methods.Add(engineMethod);

                                var deletePropertys = engineMethod.Responses.Where(i => i.Delete == true).ToList();
                                for (int i = deletePropertys.Count - 1; i >= 0; i--)
                                    engineMethod.Responses.Remove(deletePropertys[i]);

                                //Check reponse for the presence of property "x-qlik-service"
                                foreach (var response in engineMethod.Responses)
                                {
                                    var objectInterface = response?.Schema?.ToString()?.EndsWith("/ObjectInterface") ?? false;
                                    if (objectInterface && response.XQlikService == null)
                                        logger.Warn($"The interface {engineInterface.Name} has a method {methodProp.Name} which has no x-qlik-service as the return value.");
                                }

                                //T version from original
                                var jsonMethod = CreateMethodClone(engineMethod);
                                jsonMethod.UseGeneric = true;
                                engineInterface.Methods.Add(jsonMethod);

                                if (engineMethod.Parameters.Count > 0)
                                {
                                    // Add a JObject version as parameter
                                    jsonMethod = CreateMethodClone(engineMethod);
                                    jsonMethod.Parameters.Clear();
                                    jsonMethod.Parameters.Add(new EngineParameter()
                                    {
                                        Name = "param",
                                        Type = "JObject",
                                        Required = true,
                                        Description = "Qlik Parameter as JSON object.",
                                    });
                                    engineInterface.Methods.Add(jsonMethod);

                                    //T version from JObejct
                                    jsonMethod = CreateMethodClone(jsonMethod);
                                    jsonMethod.UseGeneric = true;
                                    engineInterface.Methods.Add(jsonMethod);
                                }
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

        private EngineMethod CreateMethodClone(EngineMethod currentMethod)
        {
            var json = JsonConvert.SerializeObject(currentMethod);
            return JsonConvert.DeserializeObject<EngineMethod>(json);
        }

        private void LinkEnumTypes()
        {
            foreach (var listObject in EngineObjects)
            {
                if (listObject is EngineClass engineClass)
                {
                    foreach (var property in engineClass.Properties)
                    {
                        if (!String.IsNullOrEmpty(property.Ref))
                        {
                            var propertyType = EngineObjects.FirstOrDefault(c => c.Name == property.GetRefType());
                            if (propertyType?.EngType == EngineType.ENUM)
                                property.IsEnumType = true;
                        }
                    }
                }
            }
        }
        #endregion

        #region public methods
        public List<IEngineObject> ReadJson(JObject mergeObject)
        {
            try
            {
                EngineObjects.Clear();
                AddDefinitions(mergeObject);
                LinkEnumTypes();
                AddMethods(mergeObject);
                return EngineObjects;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The json could not parse.");
                return null;
            }
        }

        public void SaveToCSharp(QlikApiConfig config, List<IEngineObject> engineObjects, string savePath, List<string> injectPragmas = null)
        {
            try
            {
                var enumList = new List<string>();
                var fileContent = new StringBuilder();
                fileContent.Append($"namespace {config.NamespaceName}");
                fileContent.AppendLine();
                fileContent.AppendLine("{");
                fileContent.AppendLine(QlikApiUtils.Indented("#region Usings", 1));
                fileContent.AppendLine(QlikApiUtils.Indented("using System;", 1));
                fileContent.AppendLine(QlikApiUtils.Indented("using System.ComponentModel;", 1));
                fileContent.AppendLine(QlikApiUtils.Indented("using System.Collections.Generic;", 1));
                fileContent.AppendLine(QlikApiUtils.Indented("using Newtonsoft.Json;", 1));
                fileContent.AppendLine(QlikApiUtils.Indented("using Newtonsoft.Json.Linq;", 1));
                fileContent.AppendLine(QlikApiUtils.Indented("using System.Threading.Tasks;", 1));
                fileContent.AppendLine(QlikApiUtils.Indented("using System.Threading;", 1));
                fileContent.AppendLine(QlikApiUtils.Indented("#endregion", 1));
                fileContent.AppendLine();

                if (injectPragmas != null)
                    foreach (var pragma in injectPragmas)
                        fileContent.AppendLine(QlikApiUtils.Indented(pragma, 1));

                var lineCounter = 0;
                var enumObjects = engineObjects.Where(d => d.EngType == EngineType.ENUM).ToList();
                if (enumObjects.Count > 0)
                {
                    fileContent.AppendLine(QlikApiUtils.Indented("#region Enums", 1));
                    logger.Debug($"Write Enums {enumObjects.Count}");
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
                }
                var interfaceObjects = engineObjects.Where(d => d.EngType == EngineType.INTERFACE).ToList();
                if (interfaceObjects.Count > 0)
                {
                    logger.Debug($"Write Interfaces {interfaceObjects.Count}");
                    fileContent.AppendLine(QlikApiUtils.Indented("#region Interfaces", 1));
                    lineCounter = 0;
                    var descBuilder = new DescritpionBuilder(Config.UseDescription);
                    foreach (EngineInterface interfaceObject in interfaceObjects)
                    {
                        lineCounter++;
                        descBuilder.Summary = interfaceObject.Description;
                        var desc = descBuilder.Generate(1);
                        if (!String.IsNullOrEmpty(desc))
                            fileContent.AppendLine(desc);

                        //Special for ObjectInterface => Add IObjectInterface
                        var implInterface = String.Empty;
                        if (Config.BaseObjectInterfaceName != interfaceObject.Name)
                            implInterface = $" : {Config.BaseObjectInterfaceName}";
                        fileContent.AppendLine(QlikApiUtils.Indented($"public interface {interfaceObject.Name}{implInterface}", 1));
                        fileContent.AppendLine(QlikApiUtils.Indented("{", 1));
                        var properties = interfaceObject.Properties;
                        foreach (var property in properties)
                        {
                            if (!String.IsNullOrEmpty(property.Description))
                            {
                                var propDescBuilder = new DescritpionBuilder(Config.UseDescription)
                                {
                                    Summary = property.Description,
                                };
                                var description = propDescBuilder.Generate(2);
                                if (!String.IsNullOrEmpty(description))
                                    fileContent.AppendLine(description);
                            }
                            fileContent.AppendLine(QlikApiUtils.Indented($"{QlikApiUtils.GetDotNetType(property.Type)} {property.Name} {{ get; set; }}", 2));
                        }
                        foreach (var methodObject in interfaceObject.Methods)
                            fileContent.AppendLine(GetFormatedMethod(methodObject));

                        var builder = new DescritpionBuilder(Config.UseDescription);
                        if (Config.BaseObjectInterfaceName == interfaceObject.Name)
                        {
                            builder.Summary = "This event fires when to notify subscribers that a change has occured.";
                            desc = builder.Generate(2);
                            fileContent.AppendLine(desc);
                            fileContent.AppendLine(QlikApiUtils.Indented("event EventHandler Changed;", 2));
                            builder.Summary = "This event fires when the Qlik Sense entity has been removed or deleted.";
                            desc = builder.Generate(2);
                            fileContent.AppendLine(desc);
                            fileContent.AppendLine(QlikApiUtils.Indented("event EventHandler Closed;", 2));
                            builder.Summary = "This event fires when to notify subscribers that a change has occured.";
                            desc = builder.Generate(2);
                            fileContent.AppendLine(desc);
                            fileContent.AppendLine(QlikApiUtils.Indented("void OnChanged();", 2));
                        }

                        fileContent.AppendLine(QlikApiUtils.Indented("}", 1));
                        if (lineCounter < interfaceObjects.Count)
                            fileContent.AppendLine();
                    }
                    fileContent.AppendLine(QlikApiUtils.Indented("#endregion", 2));
                    fileContent.AppendLine();
                }
                var classObjects = engineObjects.Where(d => d.EngType == EngineType.CLASS).ToList();
                if (classObjects.Count > 0)
                {
                    fileContent.AppendLine(QlikApiUtils.Indented("#region Classes", 1));
                    logger.Debug($"Write Classes {classObjects.Count}");
                    lineCounter = 0;
                    var descBuilder = new DescritpionBuilder(Config.UseDescription);
                    foreach (EngineClass classObject in classObjects)
                    {
                        lineCounter++;
                        descBuilder.Summary = classObject.Description;
                        descBuilder.SeeAlso = classObject.SeeAlso;
                        var desc = descBuilder.Generate(1);
                        if (!String.IsNullOrEmpty(desc))
                            fileContent.AppendLine(desc);
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
                                    var builder = new DescritpionBuilder(Config.UseDescription)
                                    {
                                        Summary = property.Description,
                                    };
                                    var description = builder.Generate(2);
                                    if (!String.IsNullOrEmpty(description))
                                        fileContent.AppendLine(description);
                                }

                                var dValue = String.Empty;
                                if (property.Default != null)
                                {
                                    dValue = property.Default.ToLowerInvariant();
                                    if (property.IsEnumType)
                                        dValue = GetEnumDefault(property.GetRefType(), property.Default);
                                    fileContent.AppendLine(QlikApiUtils.Indented($"[DefaultValue({dValue})]", 2));
                                }

                                var implements = String.Empty;
                                var refType = property.GetRefType();
                                if (classObject.Type == "array")
                                {
                                    implements = GetImplemention(property);
                                    fileContent.Replace("<###implements###>", implements);
                                }
                                else if (property.Type == "array")
                                {
                                    fileContent.AppendLine(QlikApiUtils.Indented($"public List<{QlikApiUtils.GetDotNetType(refType)}> {property.Name} {{ get; set; }}", 2));
                                }
                                else
                                {
                                    var resultType = QlikApiUtils.GetDotNetType(property.Type);
                                    if (!String.IsNullOrEmpty(refType))
                                        resultType = refType;
                                    var propertyText = QlikApiUtils.Indented($"public {resultType} {property.Name} {{ get; set; }}", 2);
                                    if (property.Default != null)
                                        propertyText += $" = {dValue};";
                                    fileContent.AppendLine(propertyText);
                                }

                                if (propertyCount < classObject.Properties.Count)
                                    fileContent.AppendLine();
                            }
                            fileContent.AppendLine(QlikApiUtils.Indented("#endregion", 2));
                        }
                        fileContent.Replace("<###implements###>", "");
                        fileContent.AppendLine(QlikApiUtils.Indented("}", 1));
                        if (lineCounter < classObjects.Count)
                            fileContent.AppendLine();
                    }
                    fileContent.AppendLine(QlikApiUtils.Indented("#endregion", 1));
                }
                fileContent.AppendLine("}");
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