namespace QlikApiParser
{
    #region Usings
    using System;
    using System.IO;
    using System.Linq;
    using System.Collections.Generic;
    using System.Text;
    using NLog;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    #endregion

    public enum AsyncMode
    {
        NONE,
        SHOW
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore,
                NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class QlikApiConfig
    {
        #region  Properties
        [JsonProperty(Required = Required.Always)]
        public string SourceFile { get; set; }
        [JsonProperty(Required = Required.Always)]
        public string OutputFolder { get; set; }
        [JsonProperty(Required = Required.Always)]
        public string TypeScriptFolder { get; set; }
        [JsonProperty(Required = Required.Always)]
        public string NamespaceName { get; set; }
        [JsonProperty]
        public bool UseQlikResponseLogic { get; set; } = true;
        [JsonProperty]
        public bool UseDescription { get; set; } = true;
        [JsonProperty]
        public AsyncMode AsyncMode { get; set; } = AsyncMode.SHOW;
        [JsonProperty]
        public bool GenerateCancelationToken { get; set; } = true;

        [JsonIgnore]
        public string BaseObjectInterfaceClassName { get; } = "ObjectInterface";
        [JsonIgnore]
        public string BaseObjectInterfaceName
        {
            get { return $"I{BaseObjectInterfaceClassName }"; }
        }
        #endregion

        public string GetChangeFile()
        {
            var name = Path.GetFileNameWithoutExtension(SourceFile);
            return $"{name}_change.json";
        }
    }
}