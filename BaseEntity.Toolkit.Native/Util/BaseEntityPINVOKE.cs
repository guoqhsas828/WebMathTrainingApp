using System;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;
using System.Xml;

public static class NativeConfigurator
{
  private static bool _initialized = false;

  public static void Initialize()
  {
    if (_initialized) return;
    CheckUpdate();
    _initialized = true;
  }

  public static void Validate()
  {
    // Make sure ToolkitConfigurator.Init() is called
    if (!_initialized)
    {
      throw new InvalidOperationException(
        "ToolkitConfigurator.Init() must be called first.");
    }
  }

  [DllImport("MagnoliaCppWrapper", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
  private static extern int GetMACAddressList(StringBuilder MACAddresses, ref int maxSize);

  [DllImport("MagnoliaCppWrapper", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
  private static extern int GetHostID(string macAddr, StringBuilder hostID, StringBuilder messages, ref int maxSizeMsg);

  private static void CheckUpdate()
  {
    try
    {

      var sb = new StringBuilder();

      #region Build Profile

      using (var stream = new StringWriter(sb))
      using (var writer = new XmlTextWriter(stream))
      {
        writer.Formatting = Formatting.Indented;
        writer.WriteStartDocument();

        writer.WriteStartElement("UpdateRequest");

        //0. Write a version number
        writer.WriteElementString("UpdateRequestVersion", "1");

        //1. User Name
        writer.WriteElementString("UserName", Environment.UserName);

        //2. HostIDs
        int bufLen = 1000;
        var hostid = new StringBuilder();
        var macList = new StringBuilder(bufLen);
        GetMACAddressList(macList, ref bufLen);
        string[] subMacLists = macList.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string mac in subMacLists)
        {
          string[] subMac = mac.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
          var thisHostid = new StringBuilder(bufLen);
          var errormessage = new StringBuilder(bufLen);
          GetHostID(subMac[0], thisHostid, errormessage, ref bufLen);
          hostid.AppendFormat("{0}|", thisHostid);
        }
        writer.WriteElementString("HostIDs", hostid.ToString());

        //3. Computer Name
        writer.WriteElementString("ComputerName", Environment.MachineName);

        //4. Software and its version
        string commandline = Environment.CommandLine;
        char splitor = commandline.StartsWith("\"") ? '"' : ' ';
        string[] subcommandline = commandline.Split(new[] { splitor }, StringSplitOptions.RemoveEmptyEntries);
        string application = Path.GetFileNameWithoutExtension(subcommandline[0]);
        writer.WriteElementString("Software", application);

        writer.WriteElementString("Version", Assembly.GetExecutingAssembly().GetName().Version.ToString());

        //5. Here as we don't perform license check here
        writer.WriteElementString("LicenseStatus", "0");
        writer.WriteElementString("LicenseMessage", "");

        //6. OS information
        writer.WriteElementString("OS", Environment.OSVersion.ToString());
        writer.WriteElementString("CommandLine", Environment.CommandLine);

        writer.WriteEndElement();
        writer.WriteEndDocument();
      }

      #endregion

      var plainProfile = sb.ToString();

      //here to encrypt the text
      byte[] data = Encoding.ASCII.GetBytes(plainProfile);
      var cryptic = new DESCryptoServiceProvider { Key = Assembly.GetExecutingAssembly().GetName().GetPublicKeyToken(), IV = Encoding.ASCII.GetBytes("MAGNOLIAIG") };

      using (var stream = new MemoryStream())
      {
        using (var crStream = new CryptoStream(stream, cryptic.CreateEncryptor(), CryptoStreamMode.Write))
        {
          crStream.Write(data, 0, data.Length);
        }

        //should only access stream after crStream is closed (otherwise we may get partial data)
        byte[] encryptedData = stream.ToArray();
        string profile = Convert.ToBase64String(encryptedData); //convert from byte array to string
       
      }
    }
    catch (Exception)
    {
    }
  }
}

