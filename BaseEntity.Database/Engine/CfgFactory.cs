// 
// Copyright (c) WebMathTraining Inc 2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using log4net;
using NHibernate.Cfg;
using BaseEntity.Configuration;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Database.Engine
{
  /// <summary>
  /// 
  /// </summary>
  public class CfgFactory
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(CfgFactory));

    /// <summary>
    /// Gets a hash signature that represents the schema for the database.
    /// </summary>
    /// <value>
    /// The signature (an SHA-1 hash of the NHibernate mapping document)
    /// </value>
    public string Signature { get; private set; }

    private SessionFactoryParams FactoryParams { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="factoryParams"></param>
    internal CfgFactory(SessionFactoryParams factoryParams)
    {
      FactoryParams = factoryParams;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    internal NHibernate.Cfg.Configuration GetConfiguration()
    {
      return CreateConfiguration();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private NHibernate.Cfg.Configuration CreateConfiguration()
    {
      var cfg = new NHibernate.Cfg.Configuration();

      var props = new Dictionary<string, string>();
      if (FactoryParams.Dialect == "MsSql2005")
      {
        props["dialect"] = "NHibernate.Dialect.MsSql2005Dialect";
        props["connection.driver_class"] = "NHibernate.Driver.SqlClientDriver";
        props["connection.provider"] = "NHibernate.Connection.DriverConnectionProvider";
      }
      else if (FactoryParams.Dialect == "MsSql2008")
      {
        props["dialect"] = "NHibernate.Dialect.MsSql2008Dialect";
        props["connection.driver_class"] = "NHibernate.Driver.SqlClientDriver";
        props["connection.provider"] = "NHibernate.Connection.DriverConnectionProvider";
      }
      else
      {
        throw new DatabaseException("Invalid dialect: " + FactoryParams.Dialect);
      }

      if (String.IsNullOrEmpty(FactoryParams.ConnectString))
      {
        throw new DatabaseException("ConnectString must be specified");
      }

      props["command_timeout"] = FactoryParams.CommandTimeout.ToString();
      props["connection.connection_string"] = FactoryParams.ConnectString;

      // This is for Stateless Session batching
      props["hibernate.adonet.batch_size"] = "512";

      if (!String.IsNullOrEmpty(FactoryParams.DefaultSchema))
      {
        props["default_schema"] = FactoryParams.DefaultSchema;
      }

      // If an encrypted login is specified then decrypt it and use it in the connection string
      if (FactoryParams.Password.Length > 0)
      {
        props["connection.connection_string"] = GetDecryptedConnectionString(FactoryParams);
      }

      if (!String.IsNullOrEmpty(FactoryParams.AppRoleName))
      {
        props["connection.application_role_name"] = FactoryParams.AppRoleName;
        props["connection.provider"] = "BaseEntity.Database.Engine.SqlApplicationRoleConnectionProvider, BaseEntity.Database";
      }

      if (!String.IsNullOrEmpty(FactoryParams.AppRolePassword))
      {
        props["connection.application_role_password"] = GetDecryptedApplicationRolePassword(FactoryParams);
      }

      cfg.Properties = props;

      var entities = ClassCache.FindAll().Where(cm => cm.IsEntity).ToList();

      var xmlDoc = new HbmGenerator().Generate(entities.Select(cm => cm.Name).ToList());

      cfg.AddDocument(xmlDoc);

      Signature = GetHash<SHA1CryptoServiceProvider>(xmlDoc.OuterXml);

      foreach (var cm in entities.Where(cm => !cfg.Imports.ContainsKey(cm.Name)))
      {
        cfg.Imports.Add(cm.Name, cm.Type.AssemblyQualifiedName);
      }

      return cfg;
    }

    private static string GetHash<T>(string source) where T : HashAlgorithm, new()
    {
      var cryptoServiceProvider = new T();
      return ComputeHash(source, cryptoServiceProvider);
    }

    private static string ComputeHash<T>(string source, T cryptoServiceProvider) where T : HashAlgorithm, new()
    {
      var bytes = Encoding.ASCII.GetBytes(source);
      cryptoServiceProvider.ComputeHash(bytes);
      var hash = Convert.ToBase64String(cryptoServiceProvider.Hash);
      Logger.InfoFormat("Configuration Hash: {0}", hash);
      return hash;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="factoryParams"></param>
    /// <returns></returns>
    internal static string GetDecryptedApplicationRolePassword(SessionFactoryParams factoryParams)
    {
      var password = "";
      try
      {
        // We can use EncryptLogin as-is to encrypt the application role password provided in WebMathTraining.xml. 
        // We'll always assume that the password is encrypted.
        // However, EncryptLogin produces an encrypted password based on a clear text password that is a 
        // concatenation of the supplied user and the password (ie <user>_<password>)
        // For application roles we'll treat the supplied user as the application role, 
        // so in order to get the password we'll need to strip the first part
        var decryptedAppRoleAndPassword = Decrypt(factoryParams.AppRolePassword);
        var pattern = String.Format("^{0}_", factoryParams.AppRoleName);
        password = Regex.Replace(decryptedAppRoleAndPassword, pattern, "");
      }
      catch (Exception ex)
      {
        Logger.ErrorFormat("Invalid encrypted password. {0}.", ex.Message);
      }
      return password;
    }

    /// <summary>
    /// If WebMathTraining.XML contains an Encrypted Password then this method will return 
    /// a connection string with a decrypted user and pwd that can be used to connect to the database
    /// </summary>
    /// <returns></returns>
    internal static string GetDecryptedConnectionString(SessionFactoryParams factoryParams)
    {
      // Create a new connection string that includes our decrypted user and password
      string newConnectString = "";
      string loginUserName = ""; // We need this to salt the encrypted pwd
      string[] connectTags = factoryParams.ConnectString.Split(new[] {';'});

      foreach (string tag in connectTags)
      {
        string[] namevalue = tag.Split(new[] {'='});

        if (namevalue.Length < 2)
          continue;

        // Ignore password since we will explicitly add the encrypted
        // version after this loop
        if (String.Compare(namevalue[0].Trim(), "password", true) != 0)
          newConnectString += String.Format("{0}={1};", namevalue[0], namevalue[1]);

        if (String.Compare(namevalue[0].Trim(), "user id", true) == 0)
          loginUserName = namevalue[1]; // needed to de-salt the encrypted password
      }

      string loginPassword = "";

      try
      {
        loginPassword = Decrypt(
          factoryParams.Password);
      }
      catch (Exception ex)
      {
        Logger.ErrorFormat("Invalid encrypted password. {0}.", ex.Message);
      }

      // De-Salt the password
      if (loginPassword.Length > loginUserName.Length + 1)
      {
        loginPassword = loginPassword.Substring(loginUserName.Length + 1);
      }
      else
      {
        Logger.Error("Invalid encrypted password. Apparently missing some salt.");
      }

      // Add the decrypted password to the connect string
      newConnectString += String.Format("Password={0}", loginPassword);

      return newConnectString;
    }

    private static readonly HashSet<char> invalidFileNameChars_ = new HashSet<char>(Path.GetInvalidFileNameChars());

    static private readonly byte[] key_ =
    {
      228, 229, 113, 209, 5, 220, 215, 90, 71, 110, 163, 101, 118, 177, 136, 63, 150, 132, 240, 43, 76, 27, 57, 111, 196,
      224, 52, 158, 171, 155, 160, 58
    };

    static private readonly byte[] iv_ = {142, 143, 218, 45, 98, 35, 248, 152, 135, 47, 210, 244, 169, 150, 54, 188};

    /// <summary>
    /// Internal method to decrypt a string
    /// Used to obtain a user/pwd for login to the database
    /// </summary>
    /// <param name="encrypted"></param>
    /// <returns></returns>
    private static string Decrypt(string encrypted)
    {
      // Decode the �encrypted� byte[]
      UTF8Encoding textConverter = new UTF8Encoding();
      RijndaelManaged rij = new RijndaelManaged();
      //Get a decryptor that uses the same key and IV as the encryptor.
      ICryptoTransform decryptor = rij.CreateDecryptor(key_, iv_);

      //Now decrypt the previously encrypted message using the decryptor
      // obtained in the above step.
      MemoryStream msDecrypt = new MemoryStream();
      byte[] baEncrypted = Convert.FromBase64String(encrypted);
      msDecrypt.Write(baEncrypted, 0, baEncrypted.Length);
      msDecrypt.Seek(0, SeekOrigin.Begin);
      CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);

      byte[] fromEncrypt = new byte[encrypted.Length - 4];

      //Read the data out of the crypto stream.
      byte[] length = new byte[4];
      csDecrypt.Read(length, 0, 4);
      csDecrypt.Read(fromEncrypt, 0, fromEncrypt.Length);
      int len = length[0] | (length[1] << 8) | (length[2] << 16) | (length[3] << 24);

      //Convert the byte array back into a string.
      return textConverter.GetString(fromEncrypt).Substring(0, len);
    }

    /// <summary>
    /// Utility method to encrypt a string.
    /// Used to create a user and pwd string to place in WebMathTraining.xml for login
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static string Encrypt(string message)
    {
      // Encode the string �message�
      // ScrambleKey and ScambleIV are randomly generated
      // RC2CryptoServiceProvider rc2 = new RC2CryptoServiceProvider();
      // rc2.GenerateIV();
      // ScrambleIV = rc2.IV;
      // rc2.GenerateKey();
      // ScrambleKey = rc2.Key
      UTF8Encoding textConverter = new UTF8Encoding();

      //Convert the data to a byte array.
      byte[] toEncrypt = textConverter.GetBytes(message);

      //Get an encryptor.
      RijndaelManaged rij = new RijndaelManaged();
      ICryptoTransform encryptor = rij.CreateEncryptor(key_, iv_);

      //Encrypt the data.
      MemoryStream msEncrypt = new MemoryStream();
      CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);

      //Write all data to the crypto stream and flush it.
      // Encode length as first 4 bytes
      byte[] length = new byte[4];
      length[0] = (byte)(message.Length & 0xFF);
      length[1] = (byte)((message.Length >> 8) & 0xFF);
      length[2] = (byte)((message.Length >> 16) & 0xFF);
      length[3] = (byte)((message.Length >> 24) & 0xFF);
      csEncrypt.Write(length, 0, 4);
      csEncrypt.Write(toEncrypt, 0, toEncrypt.Length);
      csEncrypt.FlushFinalBlock();

      //Get encrypted array of bytes.
      byte[] encrypted = msEncrypt.ToArray();

      return Convert.ToBase64String(encrypted);
    }
  }
}