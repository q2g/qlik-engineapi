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
                        definitions.Add(jProperty.Name, engineObject);
                        var properties = GetValueFromProperty<JToken>(jObject, "properties");
                        if (properties == null)
                            properties = GetValueFromProperty<JToken>(jObject, "items");

                        if (properties == null)
                            logger.Debug("No properties found. EMPTY");
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
                                        parameter.DefaultValueFromDescription = parameter.DefaultValue;

                                    if (parameter.Enum != null)
                                        parameter.Type = parameter.GenerateEnumType();
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
                var fileContent = new StringBuilder();
                fileContent.Append($"namespace {name}\r\n{{");
                fileContent.AppendLine("#region Usings");
                fileContent.AppendLine("using System;");
                fileContent.AppendLine("#endregion");
                
                foreach (var objects in definitions)
                {
                    foreach (var parameter in objects.Value.Parameters)
                    {
                        
                    }
                }

                fileContent.AppendLine("}}");
                var savePath = Path.Combine(workDir, $"{name}.cs");
                File.WriteAllText(savePath, fileContent.ToString());
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
        public List<EngineParameter> Parameters { get; set; } = new List<EngineParameter>();
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineParameter
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Format { get; set; }
        public string Type { get; set; }
        public string DefaultValue { get; set; }
        public bool Required { get; set; }
        public List<string> Enum { get; set; }

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