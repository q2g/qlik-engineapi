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
        public List<EngineParameter> Parameters { get; set; } = new List<EngineParameter>();
        public List<EngineResponse> Responses { get; set; } = new List<EngineResponse>();
        public List<string> SeeAlso { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineResponse : EngineAdvanced
    {

    }

    public class EngineInterface : EngineBase, IEngineObject
    {
        [JsonIgnore]
        public List<EngineMethod> Methods { get; set; } = new List<EngineMethod>();

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
                var chars = firstEnum.Name.ToCharArray();
                foreach (var charValue in chars)
                {
                    startText += charValue;
                    if (!StartWithText(startText))
                    {
                        startText = startText.TrimEnd(charValue);
                        break;
                    }
                }

                if (!String.IsNullOrEmpty(startText))
                {
                    foreach (var item in Values)
                        item.Name = item.Name.Remove(0, startText.Length);
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
    public class EngineParameter : EngineAdvanced
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
            return result?.Split('/')?.LastOrDefault() ?? null;
        }

        public string GetRealType()
        {
            var result = GetArrayType();
            if(result == Type)
               result = GetSchemaType();
            return result;
        }
    }

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