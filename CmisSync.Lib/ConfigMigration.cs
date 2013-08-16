﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace CmisSync.Lib.Sync
{
    /// <summary>
    /// Migrate config.xml from past versions.
    /// </summary>
    public static class ConfigMigration
    {
        /// <summary>
        /// Migrate from the config.xml format of CmisSync 0.3.9 to the current format, if necessary.
        /// </summary>
        public static void Migrate()
        {
            // Replace XML root element from <sparkleshare> to <CmisSync>
            ReplaceXMLRootElement();
        }


        /// <summary>
        /// Replace XML root element name from sparkleshare to CmisSync
        /// </summary>
        private static void ReplaceXMLRootElement()
        {
            // If file does not exist yet, no need for migration.
            if( ! File.Exists(ConfigManager.CurrentConfigFile))
                return;

            var fileContents = System.IO.File.ReadAllText(ConfigManager.CurrentConfigFile);
            if ( fileContents.Contains("<notifications>True</notifications>") ) {
                fileContents = fileContents.Replace("<notifications>True</notifications>", "<notifications>true</notifications>");
                System.IO.File.WriteAllText(ConfigManager.CurrentConfigFile, fileContents);
            }
            try
            {
                // If log4net element is found, it means that the root element is already correct.
                XmlElement element = (XmlElement)ConfigManager.CurrentConfig.GetLog4NetConfig();
                if (element != null)
                    return;
            }
            catch (Exception)
            {
                // Replace root XML element from <sparkleshare> to <CmisSync>

                fileContents = fileContents.Replace("<sparkleshare>", "<CmisSync>");
                fileContents = fileContents.Replace("</sparkleshare>", "</CmisSync>");

                System.IO.File.WriteAllText(ConfigManager.CurrentConfigFile, fileContents);
                ReplaceXMLRootElement();
            }
        }
    }
}
