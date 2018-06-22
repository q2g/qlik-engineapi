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
                logger.Info("qlik-engineapi-generator");
                logger.Info("Load log options...");
                SetLoggerSettings();

                if (args == null || args.Length < 5)
                    throw new Exception("5 Parameter erwartet.");

                var config = GetConfig(args);
                if (config == null)
                    return;

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
                    qlikApiGenerator.SaveToCSharp(config, new List<IEngineObject>() {classResult}, savePath);
                }

                logger.Info("Finish");
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                logger.Error(ex, "The Application has an Error.");
            }
        }

        #region Private Methods
        private static QlikApiConfig GetConfig(string[] args)
        {
            try
            {
                var config = new QlikApiConfig()
                {
                    SourceFile = args[0],
                    OutputFolder = args[1],
                    UseDescription = Convert.ToBoolean(args[2]),
                    UseQlikResponseLogic = Convert.ToBoolean(args[3]),
                    NamespaceName = args[4],
                };
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
                var appPath = PlatformServices.Default.Application.ApplicationBasePath;
                var files = Directory.GetFiles(appPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => f.ToLowerInvariant().EndsWith("\\app.config") ||
                       f.ToLowerInvariant().EndsWith("\\app.json")).ToList();
                if (files != null && files.Count > 0)
                {
                    if (files.Count > 1)
                        throw new Exception("Too many logger configs found.");

                    path = files.FirstOrDefault();
                    var extention = Path.GetExtension(path);
                    switch (extention)
                    {
                        case ".json":
                            logger.Factory.Configuration = new XmlLoggingConfiguration(GetXmlReader(path), Path.GetFileName(path));
                            break;
                        case ".config":
                            logger.Factory.Configuration = new XmlLoggingConfiguration(path);
                            break;
                        default:
                            throw new Exception($"unknown log format {extention}.");
                    }
                }
                else
                {
                    throw new Exception("No logger config loaded.");
                }
            }
            catch
            {
                Console.WriteLine($"The logger setting are invalid!!!\nPlease check the {path} in the app folder.");
            }
        }
        #endregion
    }
}