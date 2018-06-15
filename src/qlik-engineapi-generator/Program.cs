namespace QlikApiParser
{
    #region Usings
    using System;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using Microsoft.Extensions.PlatformAbstractions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NLog;
    using NLog.Config;
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
                var origJsonFile = args?.FirstOrDefault() ?? null;
                if (!File.Exists(origJsonFile))
                {
                    logger.Warn("No file was found. Please check the args.");
                    return;
                }

                var workDir = Path.GetDirectoryName(origJsonFile);
                var files = Directory.GetFiles(workDir, "*.json", SearchOption.TopDirectoryOnly);
                var name = Path.GetFileNameWithoutExtension(origJsonFile);
                var changeJsonFile = files.FirstOrDefault(f => f.EndsWith($"{name}_change.json"));
                var changeJsonObject = GetJsonObject(changeJsonFile);
                var origJsonObject = GetJsonObject(origJsonFile);
                origJsonObject.Merge(changeJsonObject);

                logger.Info("Start parsing...");
                var qlikApiGenerator = new QlikApiGenerator();
                var definitions = qlikApiGenerator.Parse(origJsonObject);
                qlikApiGenerator.SaveToCSharp(definitions, workDir, name);
                logger.Info("Finish");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The Application has an Error.");
            }
        }

        #region Private Methods
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
