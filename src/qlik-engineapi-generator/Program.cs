namespace QlikApiParser
{
    #region Usings
    using System;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using Microsoft.Extensions.PlatformAbstractions;
    using Newtonsoft.Json;
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
                SetLoggerSettings();
                var workingFile = args?.FirstOrDefault() ?? null;
                if (workingFile == null)
                {
                    logger.Warn("No working file found.");
                    return;
                }
                logger.Info("Start parsing...");
                var qlikApiReader = new QlikApiReader();
                qlikApiReader.Parse(workingFile);
                logger.Info("Finish");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "APP ERROR");
            }

            Console.WriteLine("Hello World!");
        }

        #region Private Methods
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
