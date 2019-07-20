/*
 * HelpPortal.cs
 *
 * Copyright (c) WebMathTraining 2002-2011. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Globalization;
using System.Security.Cryptography;
using System.Web;
using BaseEntity.Configuration;

namespace BaseEntity.Configuration
{
  /// <summary>
  /// Help Portal exception
  /// </summary>
  public class HelpPortalException : Exception
  {
    
  }

  /// <summary>
  /// Identifier for online help category
  /// </summary>
  /// <remarks>
  /// NOTE: this should be same as help structure in the web site, also should match LicenseFeature class
  /// </remarks>
  public enum ProductHelpCategory
	{
    /// <summary>
    /// 
    /// </summary>
		None,

    /// <summary>
    /// XL
    /// </summary>
		XL,

    /// <summary>
    /// TOOLKIT SDK
    /// </summary>
	  TOOLKITSDK,

    /// <summary>
    /// RISK
    /// </summary>
		RISK,

    /// <summary>
    /// RISKSDK
    /// </summary>
		RISKSDK,

    /// <summary>
    /// AS
    /// </summary>
		AS,

    /// <summary>
    /// WATERFALL
    /// </summary>
		WATERFALL,

    /// <summary>
    /// INTEX
    /// </summary>
		INTEX,

    /// <summary>
    /// CVA
    /// </summary>
		CVA,

    /// <summary>
    /// CCR
    /// </summary>
		CCR
	}

  /// <summary>
  /// Help portal helper class
  /// </summary>
  public class HelpPortal
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(HelpPortal));

    #region Methods

    /// <summary>
    /// View online help for subject in browser
    /// </summary>
    /// <param name="helpCategory">Help category</param>
    public static void ViewHelp(ProductHelpCategory helpCategory)
    {
      ViewHelp(helpCategory, null);
    }

    /// <summary>
    /// View online help for a subject and sub-subject in browser
    /// This method will check the local WebMathTraining.xml for possible override settings such as localhelp or server name
    /// </summary>
    /// <param name="helpCategory">Help category</param>
    /// <param name="subject">Subject url</param>
    public static void ViewHelp(ProductHelpCategory helpCategory, string subject)
    {
      Assembly assembly = Assembly.GetExecutingAssembly();
      Version ver = assembly.GetName().Version;
      string version = string.Format("{0}.{1}", ver.Major, ver.Minor);
      bool isSecureServer = false; //default value
      string server = "portal.WebMathTrainingsolutions.com"; //default value
      bool ieBrowser = true;
      bool islocalFileHelp = false;
      var localFileHelpMap = new Dictionary<string, string>();

      //get the server setting from WebMathTraining.xml
      XmlElement configXml = Configurator.GetConfigXml("ClientPortal", null);
      if (configXml != null)
      {
        isSecureServer = (bool)Configurator.GetAttributeValue(configXml, "Secure", typeof(bool), isSecureServer);
        server = (string)Configurator.GetAttributeValue(configXml, "Server", typeof(string), server);
        ieBrowser = (bool)Configurator.GetAttributeValue(configXml, "IEBrowser", typeof(bool), ieBrowser);

        if (server == "localhost")
        {
          foreach (XmlNode localDocNode in configXml.ChildNodes)
          {
            islocalFileHelp = true;
            string product = localDocNode.Attributes["product"].Value;
            string path = localDocNode.Attributes["path"].Value;
            if (!Path.IsPathRooted(path))
            {
              path = Path.Combine(SystemContext.InstallDir, path);
              path = Path.GetFullPath(path);
            }
            localFileHelpMap[product] = path;
          }
        }
      }

      ViewOnlineHelp(helpCategory, subject, version, isSecureServer, server, islocalFileHelp, localFileHelpMap);
    }

    #endregion Methods

    #region Launcher Methods

    /// <summary>
    /// View online help given complete set of parameters.
    /// </summary>
    /// <remarks>
    /// NOTE: Called dynamically from Launcher. Do not remove or change without updating Launcher
    /// </remarks>
    /// <param name="helpCategory">Identifies help to display eg TOOLKITSDK</param>
    /// <param name="subject">Subject within category</param>
    /// <param name="version">Identifies WebMathTraining version of help to display</param>
    /// <param name="isSecureServer"></param>
    /// <param name="server"></param>
    /// <param name="ieBrowser"></param>
    /// <param name="islocalFileHelp"></param>
    /// <param name="localFileHelpMap"></param>
    public static void ViewHelp(ProductHelpCategory helpCategory,
      string subject,
      string version,
      bool isSecureServer,
      string server,
      bool ieBrowser,
      bool islocalFileHelp,
      Dictionary<string, string> localFileHelpMap)
    {
      ViewOnlineHelp(helpCategory, subject, version, isSecureServer, server, islocalFileHelp, localFileHelpMap);
    }

    #endregion Launcher Methods

    #region Private Methods

    /// <summary>
    /// Low level view help method
    /// </summary>
    private static void ViewOnlineHelp(ProductHelpCategory helpCategory,
      string subUrl,
      string version,
      bool isSecureServer,
      string server,
      bool islocalFileHelp,
      Dictionary<string, string> localFileHelpMap)
    {
      try
      {
        if (islocalFileHelp)
        {
          if (localFileHelpMap.ContainsKey(helpCategory.ToString()))
          {
            string path = "";
            try
            {
              path = localFileHelpMap[helpCategory.ToString()];
              logger.DebugFormat("Starting local help for {0} from {1}", helpCategory, path);
              Process.Start(path);
            }
            catch (Exception ex)
            {
              logger.ErrorFormat("Starting local help for {0} from {1}. {2}", helpCategory, path, ex);
              throw new Exception(String.Format("Could not start local help for [{0}] from [{1}].\n{2}", helpCategory, path, ex.Message));
            }
            return;
          }
        }

        //generate the request string
        string helpUrl;
        if (string.IsNullOrEmpty(subUrl))
          helpUrl = string.Format("/Support/Help/{0}/{1}/Index.aspx", helpCategory, version);
        else if (!subUrl.Contains("."))
          helpUrl = string.Format("/Support/Help/{0}/{1}/{2}/{2}.htm", helpCategory, version, subUrl);
        else
          helpUrl = string.Format("/Support/Help/{0}/{1}/{2}", helpCategory, version, subUrl);
        DateTime timeStamp = DateTime.UtcNow; //converted to UTC

        string hostid = GetUserIDString();

        //NOTE: Here we should make sure that en-US culture is used, such that DateTime string can be correctly parsed by server
        CultureInfo uscultrue = CultureInfo.GetCultureInfo("en-US");
        string sid = string.Format(uscultrue, "?url={0}&timestamp={1:G}&hostid={2}", helpUrl, timeStamp, hostid);
        string ssid = EncryptString(sid);

        string urlssid = HttpUtility.UrlEncode(ssid);
        string loginbaseurl = string.Format("{0}://{1}/Support/login.aspx?SSID={2}", isSecureServer ? "https" : "http", server, urlssid);

        Process.Start(loginbaseurl);
      }
      catch (Exception ex)
      {
        string msg = string.Format("Failed to start web browser to view online help for {0}.\r\n{1}", helpCategory, ex.Message);
        logger.Error(msg, ex);
        throw;
      }
    }

    /// <summary>
    /// Encript string
    /// </summary>
    private static string EncryptString(string input)
    {
      byte[] data = ASCIIEncoding.ASCII.GetBytes(input);
      DESCryptoServiceProvider cryptic = new DESCryptoServiceProvider();
      cryptic.Key = Assembly.GetExecutingAssembly().GetName().GetPublicKeyToken(); //public key token are same if sign using the same key
      cryptic.IV = ASCIIEncoding.ASCII.GetBytes("WebMathTraining");

      using (MemoryStream stream = new MemoryStream())
      {
        using (CryptoStream crStream = new CryptoStream(stream, cryptic.CreateEncryptor(), CryptoStreamMode.Write))
        {
          crStream.Write(data, 0, data.Length);
        }
        //NOTE: should only access stream after crStream is closed (otherwise we may get partial data)
        byte[] encryptedData = stream.ToArray();
        return Convert.ToBase64String(encryptedData); //convert from byte array to string
      }
    }

    #endregion Private Methods

    #region Utility Methods

    [DllImport("WebMathTrainingUtils", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int GetMACAddressList(StringBuilder MACAddresses, ref int maxSize);

    [DllImport("WebMathTrainingUtils", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int GetHostID(string macAddr, StringBuilder hostID, StringBuilder messages, ref int maxSizeMsg);

    /// <summary>
    /// Get user id
    /// </summary>
    private static string GetUserIDString()
    {
      string userName = Environment.UserName.ToLower();

      //sample case
      //"jclemens@F8A9FA89-7888D8F7-3000C03C|<other-hostid>"
      int bufLen = 1000;
      StringBuilder macList = new StringBuilder(bufLen);
      try
      {
        GetMACAddressList(macList, ref bufLen);
      }
      catch (Exception ex)
      {
        string msg = "Cannot find MAC Addresses on this machine";
        logger.Error(msg, ex);
        throw new Exception(msg, ex);
      }
      string[] subMacLists = macList.ToString().Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

      StringBuilder userid = new StringBuilder();
      userid.AppendFormat("{0}@", userName);

      foreach (string mac in subMacLists)
      {
        string[] subMac = mac.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        StringBuilder hostid = new StringBuilder(bufLen);
        StringBuilder errormessage = new StringBuilder(bufLen);
        try
        {
          GetHostID(subMac[0], hostid, errormessage, ref bufLen);
        }
        catch (Exception ex)
        {
          string msg = string.Format("Cannot get Host id from given MAC Addresses '{0}'. Error message: {1} ", mac, errormessage);
          logger.Error(msg, ex);
          throw new Exception(msg, ex);
        }
        userid.AppendFormat("{0}|", hostid);
      }

      return userid.ToString();
    }

    #endregion Utility Methods
  }
}
