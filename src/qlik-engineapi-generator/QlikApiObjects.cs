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
    using System.Text.RegularExpressions;
    #endregion

    #region  Helper Classes
    public enum EngineType
    {
        ENUM,
        CLASS,
        INTERFACE
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
        public List<string> SeeAlso { get; set; }
        public bool Deprecated { get; set; }

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
        public string Default { get; set; }
        public string DefaultValueFromDescription { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineMethod : EngineBase
    {
        public List<EngineParameter> Parameters { get; set; } = new List<EngineParameter>();
        public List<EngineResponse> Responses { get; set; } = new List<EngineResponse>();
        public bool UseGeneric { get; set; } = false;

        public EngineClass GetMultipleClass()
        {
            try
            {
                var className = $"{Name}Response";
                var result = new EngineClass()
                {
                    Name = className,
                    Type = "object",
                };
                foreach (var response in Responses)
                {
                    var engineProprty = new EngineProperty()
                    {
                        Name = response.Name,
                        Description = response.Description,
                        Type = QlikApiUtils.GetDotNetType(response.GetRealType()),
                        Required = response.Required,
                        Format = response.Format,
                    };

                    var serviceType = response.GetServiceType();
                    if (serviceType != null)
                        engineProprty.Type = serviceType;
                    result.Properties.Add(engineProprty);
                }
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("The method \"GetMultipleClass\" was failed.", ex);
            }
        }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                    NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineAdvancedTypes : EngineAdvanced
    {
        public JObject Schema { get; set; }
        public JObject Items { get; set; }

        private string GetArrayType()
        {
            if (Items == null)
                return Type;
            var itemType = Items["type"]?.ToObject<string>() ?? null;
            if (itemType == null)
                return Type;
            return $"List<{QlikApiUtils.GetDotNetType(itemType)}>";
        }

        private string GetSchemaType()
        {
            if (Schema == null)
                return Type;
            var result = Schema["$ref"]?.ToObject<string>() ?? null;
            if (result == null)
                return Type;
            result = result?.Split('/')?.LastOrDefault() ?? null;
            if (Type == "array")
                return $"List<{QlikApiUtils.GetDotNetType(result)}>";
            return result;
        }

        public string GetServiceType()
        {
            if (Schema == null)
                return null;

            var service = Schema["$service"]?.ToObject<string>() ?? null;
            if (service != null)
            {
                service = service?.Split('/')?.LastOrDefault() ?? null;
                return $"I{service}";
            }
            return null;
        }

        public string GetRealType()
        {
            var result = GetArrayType();
            if (result == Type)
                result = GetSchemaType();
            return result;
        }

        public List<EngineEnum> GetEnums()
        {
            var results = new List<EngineEnum>();
            if (Items != null)
            {
                var enumObj = Items["enum"] ?? null;
                if (enumObj != null)
                {
                    var engineEnum = new EngineEnum();
                    engineEnum.Name = Name;
                    Type = Name;
                    var childen = enumObj.Children();
                    foreach (var child in childen)
                    {
                        var name = child.ToObject<string>();
                        engineEnum.Values.Add(new EngineEnumValue() { Name = name });
                    }
                    results.Add(engineEnum);
                }
            }
            return results;
        }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineResponse : EngineAdvancedTypes { }

    public class EngineInterface : EngineBase, IEngineObject
    {
        [JsonIgnore]
        public List<EngineMethod> Methods { get; set; } = new List<EngineMethod>();

        [JsonIgnore]
        public List<EngineProperty> Properties { get; set; } = new List<EngineProperty>();

        [JsonIgnore]
        public EngineType EngType { get => EngineType.INTERFACE; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineClass : EngineBase, IEngineObject
    {
        public string Type { get; set; }

        [JsonIgnore]
        public List<EngineProperty> Properties { get; set; } = new List<EngineProperty>();

        [JsonIgnore]
        public EngineType EngType { get => EngineType.CLASS; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                    NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineEnum : EngineBase, IEngineObject
    {
        public EngineType EngType { get => EngineType.ENUM; }
        public List<EngineEnumValue> Values { get; set; } = new List<EngineEnumValue>();

        private bool StartWithText(string value)
        {
            var items = Values.Skip(1);
            foreach (var item in items)
            {
                if (!item.Name.StartsWith(value))
                    return false;
            }
            return true;
        }

        public void RenameValues()
        {
            try
            {
                var firstEnum = Values?.FirstOrDefault() ?? null;
                if (firstEnum == null)
                    return;
                var startText = String.Empty;
                var blocks = firstEnum.Name.Split('_');
                if (blocks.Length == 1)
                    return;
                var tempText = String.Empty;
                foreach (var block in blocks)
                {
                    startText += $"{block}_";
                    if (!StartWithText(startText))
                        break;
                    tempText = startText;
                }

                if (!String.IsNullOrEmpty(startText))
                {
                    var hitNumber = false;
                    foreach (var item in Values)
                    {
                        var testValue = item.Name.Remove(0, tempText.Length);
                        if (Regex.IsMatch(testValue, "^[0-9]+"))
                        {
                            hitNumber = true;
                            break;
                        }
                    }
                    if (!hitNumber)
                        foreach (var item in Values)
                            item.Name = item.Name.Remove(0, tempText.Length);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Rename Values was failed.", ex);
            }
        }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                    NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineEnumValue : EngineBase
    {
        public int? Value { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineParameter : EngineAdvancedTypes { }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineProperty : EngineAdvanced
    {
        public List<string> Enum { get; set; }
        public bool IsEnumType { get; set; }
        public string Ref { get; set; }

        public string GetRefType()
        {
            var result = Ref?.Split('/')?.LastOrDefault() ?? null;
            if(result == "JsonObject")
              return "JObject";
            return result;
        }

        public List<EngineEnum> GetConvertedEnums()
        {
            var results = new List<EngineEnum>();
            foreach (var item in Enum)
                results.Add(new EngineEnum() { Name = item });
            return results;
        }
    }

    public class DescriptionSection
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class DescritpionBuilder
    {
        public string Summary { get; set; }
        public List<string> SeeAlso { get; set; } = new List<string>();
        public List<EngineParameter> Param { get; set; } = new List<EngineParameter>();
        public string Return { get; set; }
        private bool UseDescription { get; set; }

        public DescritpionBuilder(bool useDescription)
        {
            UseDescription = useDescription;
        }

        private string GetName(string name, Tuple<string, string> args = null)
        {
            var builder = new StringBuilder();
            if (args != null)
                builder.AppendLine($" {args.Item1}=\"{args.Item2}\"");
            return $"{name}{builder.ToString().TrimEnd()}";
        }

        private string GetFormatedList(List<string> values, string name, int layer, Tuple<string, string> args = null)
        {
            var builder = new StringBuilder();
            builder.AppendLine(QlikApiUtils.Indented($"/// <{GetName(name, args).Trim()}>", layer));
            foreach (var item in values)
            {
                var val = item.Replace("\r", "");
                builder.AppendLine(QlikApiUtils.Indented($"/// {val.Trim()}", layer));
            }
            builder.AppendLine(QlikApiUtils.Indented($"/// </{name}>", layer));
            return builder.ToString().TrimEnd();
        }

        private string GetFormatedText(string value)
        {
            value = value.Replace("&lt;", "<");
            value = value.Replace("&gt;", ">");
            return value;
        }

        public string Generate(int layer)
        {
            try
            {
                if (UseDescription == false)
                    return null;

                var builder = new StringBuilder();
                if (!String.IsNullOrEmpty(Summary))
                    builder.AppendLine(GetFormatedList(Summary.Split('\n').ToList(), "summary", layer));
                if (Param != null && Param.Count > 0)
                {
                    foreach (var item in Param)
                    {
                        if (!String.IsNullOrEmpty(item.Description))
                        {
                            var values = item.Description.Split('\n').ToList();
                            var parmText = GetFormatedList(values, "param", layer, new Tuple<string, string>("name", item.Name));
                            builder.AppendLine(parmText);
                        }
                    }
                }
                if (!String.IsNullOrEmpty(Return))
                    builder.AppendLine(GetFormatedList(Return.Split('\n').ToList(), "return", layer));
                if (SeeAlso != null && SeeAlso.Count > 0)
                {
                    builder.AppendLine(GetFormatedList(SeeAlso, "seealso", layer));
                }
                return GetFormatedText(builder.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                throw new Exception("The description could not be generated.", ex);
            }
        }
    }
    #endregion
}