namespace QlikApiParser
{
    #region Usings
    using System.IO;
    using System.Linq;
    using System.Xml;
    using System;
    using Microsoft.Extensions.PlatformAbstractions;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Helper;
    using NLog.Config;
    using NLog;
    using System.Collections.Generic;
    #endregion

    class Program
    {
        #region Logger
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        static void Main(string[] args)
        {
            try
            {
                SetLoggerSettings();
                logger.Info("qlik-engineapi-generator");
                logger.Info("Load options...");
                var config = GetConfig();
                if (!File.Exists(config.SourceFile))
                {
                    logger.Warn("No source file found. Please check the args.");
                    return;
                }

                var origJsonObject = GetJsonObject(config.SourceFile);
                var workDir = Path.GetDirectoryName(config.SourceFile);
                if (string.IsNullOrEmpty(config.OutputFolder))
                    config.OutputFolder = workDir;
                Directory.CreateDirectory(config.OutputFolder);

                var files = Directory.GetFiles(workDir, "*.json", SearchOption.TopDirectoryOnly);
                var changeJsonFile = config.GetChangeFile();
                if (File.Exists(changeJsonFile))
                {
                    var changeJsonObject = GetJsonObject(changeJsonFile);
                    origJsonObject.Merge(changeJsonObject);
                    var keyNames = new List<string>() {"qExportState", "qMatchingFieldMode", "qGroup"};
                    var parameters = origJsonObject.SelectTokens("$...parameters").ToList();
                    var jArray = new JArray();
                    for (int i = 0; i < parameters.Count; i++)
                    {
                         jArray = parameters[i].ToObject<JArray>();
                         parameters[i].Replace(origJsonObject.MergeArray(jArray, keyNames));
                    }
                    keyNames = new List<string>() {"qReturn"};
                    var responses = origJsonObject.SelectTokens("$...responses").ToList();
                    for (int i = 0; i < responses.Count; i++)
                    {
                        jArray = responses[i].ToObject<JArray>();
                        responses[i].Replace(origJsonObject.MergeArray(jArray, keyNames));
                    }
                }
                
                logger.Info("Start parsing...");
                var qlikApiGenerator = new QlikApiGenerator(config);
                var engineObjects = qlikApiGenerator.ReadJson(origJsonObject);

                logger.Info("Write Enums...");
                var objectResults = engineObjects.Where(o => o.EngType == EngineType.ENUM).ToList();
                var savePath = Path.Combine(config.OutputFolder, "Enums.cs");
                qlikApiGenerator.SaveToCSharp(config, objectResults, savePath);

                logger.Info("Write Interfaces...");
                objectResults = engineObjects.Where(o => o.EngType == EngineType.INTERFACE).ToList();
                savePath = Path.Combine(config.OutputFolder, "Interfaces.cs");
                qlikApiGenerator.SaveToCSharp(config, objectResults, savePath);

                logger.Info("Write Classes...");
                objectResults = engineObjects.Where(o => o.EngType == EngineType.CLASS).ToList();
                foreach (var classResult in objectResults)
                {
                    savePath = Path.Combine(config.OutputFolder, $"{classResult.Name}.cs");
                    qlikApiGenerator.SaveToCSharp(config, new List<IEngineObject>() { classResult }, savePath);
                }

                Environment.ExitCode = 0;
                logger.Info("Finish");
                logger.Factory.Flush();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The Application has an Error.");
                logger.Factory.Flush();
                Environment.ExitCode = 1;
            }
        }

        #region Private Methods
        private static QlikApiConfig GetConfig()
        {
            try
            {
                var appConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.json");
                var json = File.ReadAllText(appConfig);
                var config = JsonConvert.DeserializeObject<QlikApiConfig>(json);
                config.SourceFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.SourceFile);
                config.OutputFolder = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{config.OutputFolder.Replace("/", "\\").TrimEnd('\\')}\\"));
                return config;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Could not read cmd parameter.");
                return null;
            }
        }

        private static JObject GetJsonObject(string fullname)
        {
            var json = File.ReadAllText(fullname);
            return JObject.Parse(json);
        }

        private static XmlReader GetXmlReader(string path)
        {
            var jsonContent = File.ReadAllText(path);
            var xdoc = JsonConvert.DeserializeXNode(jsonContent);
            return xdoc.CreateReader();
        }

        private static void SetLoggerSettings()
        {
            var path = String.Empty;

            try
            {
                var logConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.config");
                logger.Factory.Configuration = new XmlLoggingConfiguration(logConfigPath);
                var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "qlikapiparser.log");
                if (File.Exists(logFile))
                    File.Delete(logFile);
            }
            catch
            {
                Console.WriteLine($"The logger setting are invalid!!!\nPlease check the {path} in the app folder.");
            }
        }
        #endregion
    }
}