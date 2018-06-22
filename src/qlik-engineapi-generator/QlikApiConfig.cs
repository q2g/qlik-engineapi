namespace QlikApiParser
{
    #region Usings
    using System;
    using System.IO;
    using System.Linq;
    using System.Collections.Generic;
    using System.Text;
    using NLog;
    #endregion

    public class QlikApiConfig
    {
        #region  Properties
        public string SourceFile { get; set; }
        public string OutputFolder { get; set; }
        public string NamespaceName { get; set; }
        public bool UseQlikResponseLogic { get; set; } = true;
        public bool UseDescription { get; set; } = true;
        #endregion

        public string GetChangeFile()
        {
             var name = Path.GetFileNameWithoutExtension(SourceFile);
             return $"{name}_change.json";
        }
    }
}