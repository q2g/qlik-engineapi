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

    public enum AsyncMode
    {
        NONE,
        SHOW
    }

    public class QlikApiConfig
    {
        #region  Properties
        public string SourceFile { get; set; }
        public string OutputFolder { get; set; }
        public string NamespaceName { get; set; }
        public bool UseQlikResponseLogic { get; set; } = true;
        public bool UseDescription { get; set; } = true;
        public AsyncMode AsyncMode { get; set; }
        public bool GenerateCancelationToken { get; set; } = true;

        public string BaseObjectInterfaceClassName { get; } = "ObjectInterface";
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