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
    #endregion

    public class QlikApiReader
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
        public Dictionary<string, EngineObject> Parse(string qlikApiFile)
        {
            try
            {
                var content = File.ReadAllText(qlikApiFile);
                var pObject = JObject.Parse(content);
                var definitionsToken = pObject["definitions"] as JObject;
                var definitions = new Dictionary<string, EngineObject>();
                foreach (var child in definitionsToken.Children())
                {
                    var jProperty = child as JProperty;
                    var engineObject = new EngineObject();
                    definitions.Add(jProperty.Name, engineObject);
                    logger.Debug($"Object name: {jProperty.Name}");
                    foreach (var subChild in child.Children())
                    {
                        var jObject = subChild as JObject;
                        engineObject.Type = GetValueFromProperty<string>(jObject, "type");
                        engineObject.Description = GetValueFromProperty<string>(jObject, "description");
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
        #endregion
    }

    #region  Helper Classes
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

        public string DefaultValueFromDescription { get; set; }
        public string Ref { get; set; }
    }
    #endregion
}