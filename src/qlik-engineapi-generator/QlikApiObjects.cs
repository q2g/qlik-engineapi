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

        [JsonProperty(PropertyName = "x-qlik-deprecation-description")]
        public string XQlikDeprecationDescription { get; set; }

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
                        Type = QlikApiUtils.GetDotNetType(response.GetRealType(ScriptLanguage.CSharp)),
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

        [JsonProperty(PropertyName = "x-qlik-service")]
        public string XQlikService { get; set; }

        public bool Delete { get; set; }

        private string GetArrayType(ScriptLanguage language)
        {
            if (Items == null)
                return Type;
            var itemType = Items["type"]?.ToObject<string>() ?? null;
            if (itemType == null)
                return Type;
            switch (language)
            {
                case ScriptLanguage.CSharp:
                    return $"List<{QlikApiUtils.GetDotNetType(itemType)}>";
                case ScriptLanguage.TypeScript:
                    return $"{QlikApiUtils.GetTypeScriptType(itemType)}[]";
                default:
                    throw new Exception($"Unknown script language {language.ToString()}");
            }
        }

        private string GetSchemaType(ScriptLanguage language)
        {
            if (Schema == null)
                return Type;
            var result = Schema["$ref"]?.ToObject<string>() ?? null;
            if (result == null)
                return Type;
            result = result?.Split('/')?.LastOrDefault() ?? null;
            if (Type == "array")
            {
                switch (language)
                {
                    case ScriptLanguage.CSharp:
                        return $"List<{QlikApiUtils.GetDotNetType(result)}>";
                    case ScriptLanguage.TypeScript:
                        return $"{QlikApiUtils.GetTypeScriptType(result)}[]";
                    default:
                        throw new Exception($"Unknown script language {language.ToString()}");
                }
            }
            return result;
        }

        public string GetServiceType()
        {
            if (XQlikService == null)
                return null;
            return $"I{XQlikService}";
        }

        public string GetEnumType()
        {
            if (Items == null)
                return Type;
            var enumType = Items["$ref"]?.ToObject<string>() ?? null;
            if (enumType != null)
                return enumType?.Split('/')?.LastOrDefault() ?? null;
            return Type;
        }

        public string GetRealType(ScriptLanguage language)
        {
            var result = GetArrayType(language);
            if (result == Type)
                result = GetSchemaType(language);
            return result;
        }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineResponse : EngineAdvancedTypes { }

    public class EngineProperties : EngineBase
    {
        [JsonIgnore]
        public List<EngineProperty> Properties { get; set; } = new List<EngineProperty>();
    }

    public class EngineInterface : EngineProperties, IEngineObject
    {
        [JsonIgnore]
        public List<EngineMethod> Methods { get; set; } = new List<EngineMethod>();

        [JsonIgnore]
        public EngineType EngType { get => EngineType.INTERFACE; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineClass : EngineProperties, IEngineObject
    {
        public string Type { get; set; }

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
        public string ShotValue { get; set; }

        [JsonProperty(PropertyName = "x-qlik-const")]
        public int? XQlikConst { get; set; }
        public string Title { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineParameter : EngineAdvancedTypes { }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class EngineProperty : EngineAdvanced
    {
        public List<string> Enum { get; set; }
        public JToken EnumShot { get; set; }
        public bool IsEnumType { get; set; }
        public string Ref { get; set; }

        public string GetRefType()
        {
            var result = Ref?.Split('/')?.LastOrDefault() ?? null;
            if (result == "JsonObject")
                return "JObject";
            return result;
        }

        public string GetRefTypeScript()
        {
            var result = Ref?.Split('/')?.LastOrDefault() ?? null;
            if (result == "JsonObject")
                return "JObject";
            if (Ref != null && Ref.StartsWith("#") && !IsEnumType)
                return $"I{result}";
            else
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
        private readonly ScriptLanguage language = ScriptLanguage.CSharp;

        public DescritpionBuilder(bool useDescription, ScriptLanguage lang)
        {
            language = lang;
            UseDescription = useDescription;
        }

        private string GetName(string name, Tuple<string, string> args = null)
        {
            var builder = new StringBuilder();
            if (language == ScriptLanguage.CSharp)
            {
                if (args != null)
                    builder.AppendLine($" {args.Item1}=\"{args.Item2}\"");
            }
            return $"{name}{builder.ToString().TrimEnd()}";
        }

        private string GetFormatedList(List<string> values, string name, int layer, Tuple<string, string> args = null)
        {
            var builder = new StringBuilder();
            builder.AppendLine(QlikApiUtils.Indented($"/// <{GetName(name, args).Trim()}>", layer));
            foreach (var item in values)
            {
                var val = PreFormatedText(item.Replace("\r", ""));
                foreach(var line in val)
                    builder.AppendLine(QlikApiUtils.Indented($"/// {line.Trim()}", layer));
            }
            builder.AppendLine(QlikApiUtils.Indented($"/// </{name}>", layer));
            return builder.ToString().TrimEnd();
        }

        private string GetFormatedTypscriptList(List<string> values, string name, int layer, Tuple<string, string> args = null)
        {
            var builder = new StringBuilder();
            var def = true;
            if (!String.IsNullOrEmpty(name))
            {               
                builder.Append(QlikApiUtils.Indented($" * {GetName(name, args).Trim()}", layer).TrimEnd());
                def = false;
            }
            
            foreach (var item in values)
            {
                var newItem = item;
                if (name == "@return" && item.StartsWith("{"))
                {
                    newItem = "JSON " + item;
                }
                                   
                var vals = PreFormatedText(newItem.Replace("\r", "").Replace((char)160,' ').Replace("  "," "));                
                foreach (var shortLine in vals)
                {
                    if (def)
                        builder.AppendLine(QlikApiUtils.Indented($" * {shortLine.Trim()}", layer, def).TrimEnd());
                    else
                    {
                        builder.AppendLine(QlikApiUtils.Indented($" {shortLine.Trim()}", layer, def).TrimEnd());
                        def = true;
                    }
                }
            }
            return builder.ToString().TrimEnd();
        }

        private List<string> PreFormatedText(string value)
        {
            value = value.Replace("&lt;", "<");
            value = value.Replace("&gt;", ">");
            value = value.Replace("<", "[");
            value = value.Replace(">", "]");
            value = Regex.Replace(value, "\\[div class=([^\\]].*?)\\]", "<note type=\"$1\">", RegexOptions.Singleline);
            value = value.Replace("[/div]", "</note>");
            if (language == ScriptLanguage.CSharp)
                value = value.Replace("</note> <note", "</note>\r\n<note");
            else if (language == ScriptLanguage.TypeScript)
                value = value.Replace("</note> <note", "</note>\r\n<note");
            else
                throw new Exception($"Unknown script language {language.ToString()}");

            if (value.Length > 180)
            {
                if (value.StartsWith("When set to true, generated nodes (based on current selection)"))
                    Console.WriteLine();

                var words = value.Split(" ");
                var lines = new StringBuilder();
                var line = "";
                for (int i = 0; i < words.Length; i++)
                {
                    if ((line.Length + words[i].Length) < 180)
                    {
                        if (line.Length > 0)
                            line += " ";
                        line += words[i];
                    }
                    else
                    {
                        lines.AppendLine(line);
                        line = "";
                    }
                }
                
                value = lines.ToString().TrimEnd();
                //value = value.Replace(". ", ".\r\n");
                //value = value.Replace("[br]", "[br]\r\n");
            }
            return value.Split("\r\n").ToList();
        }

        private string GetFormatedText(string value)
        {
            value = value.Replace("&lt;", "<");
            value = value.Replace("&gt;", ">");
            return value;
        }

        public string Generate(int layer, string deprecated = null)
        {
            try
            {
                if (UseDescription == false)
                    return null;

                var builder = new StringBuilder();

                if (language == ScriptLanguage.CSharp)
                {
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
                }
                else if (language == ScriptLanguage.TypeScript)
                {
                    builder.AppendLine(QlikApiUtils.Indented($"/**", layer));
                    if (!String.IsNullOrEmpty(Summary))
                        builder.AppendLine(GetFormatedTypscriptList(Summary.Split('\n').ToList(), "", layer));
                    else
                    {
                        Summary = "Please, add a Description";
                        builder.AppendLine(GetFormatedTypscriptList(Summary.Split('\n').ToList(), "", layer));
                    }

                    if (Param != null && Param.Count > 0)
                    {
                        foreach (var item in Param)
                        {
                            if (!String.IsNullOrEmpty(item.Description))
                            {
                                var values = item.Description.Replace("  "," ").Split('\n').ToList();
                                var parmText = GetFormatedTypscriptList(values, "@param", layer, new Tuple<string, string>("name", item.Name));
                                builder.AppendLine(parmText);
                            }
                        }
                    }

                    if (!String.IsNullOrEmpty(Return))
                        builder.AppendLine(GetFormatedTypscriptList(Return.Split('\n').ToList(), "@return", layer));

                    if (!String.IsNullOrEmpty(deprecated))
                        builder.AppendLine(QlikApiUtils.Indented($" * @deprecated{ deprecated }", layer));

                    builder.AppendLine(QlikApiUtils.Indented($" */", layer));
                }
                else
                    throw new Exception($"Unknown script language {language.ToString()}");

                var descResult = GetFormatedText(builder.ToString().TrimEnd());
                if (descResult.Contains("[table]"))
                {
                    descResult = descResult.Replace("[/table]", "�[/table]");
                    var match = Regex.Match(descResult, "(\\[table\\][^�]*�\\[/table\\])", RegexOptions.Singleline);
                    if(match.Success)
                    {
                        var tableString = match.Groups[1].Value;
                        tableString = tableString.Replace("[", "<");
                        tableString = tableString.Replace("]", ">");
                        tableString = tableString.Replace("///", "");
                        tableString = tableString.Replace("�", "");
                        tableString = Regex.Replace(tableString, "[ ]{2,}", " ");
                        //Converthtml2markdown
                        //list in table ?
                    }
                }

                return descResult;
            }
            catch (Exception ex)
            {
                throw new Exception("The description could not be generated.", ex);
            }
        }
    }
    #endregion
}