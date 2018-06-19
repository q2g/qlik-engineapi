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
    #endregion

    #region  Helper Classes
    public enum EngineType
    {
        ENUM,
        CLASS
    }

    public interface IEngineObject
    {
        string Name { get; set; }
        EngineType EngType { get; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineBase
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                    NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineAdvanced : EngineBase
    {
        public string Type { get; set; }
        public string Format { get; set; }
        public bool Required { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineMethod : EngineBase
    {
        [JsonIgnore]
        public List<EngineParameter> Parameter { get; set; } = new List<EngineParameter>();
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineResponse : EngineAdvanced { }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineClass : EngineBase, IEngineObject
    {
        public string Type { get; set; }

        [JsonIgnore]
        public List<EngineProperty> Properties { get; set; } = new List<EngineProperty>();

        [JsonIgnore]
        public List<EngineMethod> Methods { get; set; } = new List<EngineMethod>();

        [JsonIgnore]
        public EngineType EngType { get => EngineType.CLASS; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                    NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineEnum : EngineBase, IEngineObject
    {
        public EngineType EngType { get => EngineType.ENUM; }
        public List<EngineEnumValue> Values { get; set; } = new List<EngineEnumValue>();
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                    NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineEnumValue : EngineBase
    {
        public int? Value { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineParameter : EngineAdvanced { }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineProperty : EngineAdvanced
    {
        public string Default { get; set; }
        public string DefaultValueFromDescription { get; set; }
        public List<string> Enum { get; set; }
        public bool IsEnumType { get; set; }
        public string Ref { get; set; }

        public string GetRefType()
        {
            return Ref?.Split('/')?.LastOrDefault() ?? null;
        }

        public List<EngineEnum> GetConvertedEnums()
        {
            var results = new List<EngineEnum>();
            foreach (var item in Enum)
                results.Add(new EngineEnum() { Name = item });
            return results;
        }

        public string GenerateEnumType()
        {
            return $"{Name}_Enum";
        }
    }
    #endregion
}