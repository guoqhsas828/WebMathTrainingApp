using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Layout.Pattern;
using log4net.Util;

namespace BaseEntity.Configuration
{
  #region ObjectAppender

  /// <summary>
  /// A custom log4net Appender for storing binary blobs to store program state
  /// </summary>
  public class ObjectAppender : RollingFileAppender
  {
    #region Constructors

    /// <summary>
    /// Default Constructor
    /// </summary>
    public ObjectAppender()
      : base()
    {
    }

    #endregion

    #region methods

    /// <summary>
    /// Overriding Append to allow for a custom Binary Log FileName to be injected into the text writer 
    /// </summary>
    /// <param name="loggingEvent"></param>
    protected override void Append(LoggingEvent loggingEvent)
    {
        base.AdjustFileBeforeAppend();
        ((ObjectAppenderQuietTextWriter)QuietWriter).ObjectLogPath = ObjectLogPath;
        ((ObjectAppenderQuietTextWriter)QuietWriter).ParameterisedObjectLogFile = ObjectLogFile;

        base.Append(loggingEvent);
    }

    /// <summary>
    /// Overriding SetQWForFiles to allow for a custom TextWriter to be created for the ObjectAppender. 
    /// This is in line with the RollingFileAppender and FileAppender in the log4net library.
    /// </summary>
    /// <param name="writer"></param>
    protected override void SetQWForFiles(TextWriter writer)
    {
      QuietWriter = new ObjectAppenderQuietTextWriter(writer, ErrorHandler);
      ((ObjectAppenderQuietTextWriter)QuietWriter).ObjectLogPath = ObjectLogPath;
      ((ObjectAppenderQuietTextWriter)QuietWriter).ParameterisedObjectLogFile = ObjectLogFile;
    }

    #endregion

    #region Properties

    /// <summary>
    /// This needs to be a property to allow for injection from the log4net config
    /// </summary>
    public string ObjectLogPath { get; set; }

    /// <summary>
    /// This needs to be a property to allow for injection from the log4net config
    /// </summary>
    public string FallBackLogFileName { get; set; }

    /// <summary>
    /// This needs to be a property to allow for injection from the log4net config
    /// </summary>
    public log4net.Util.PatternString ObjectLogFile
    {
      get { return _parameterisedObjectLogFile; }
      set
      {
        _parameterisedObjectLogFile = value;
        _parameterisedObjectLogFile.AddConverter(new ConverterInfo() { Name = "namespace", Type = typeof(NamespacePatternConvertor) });
        _parameterisedObjectLogFile.AddConverter(new ConverterInfo() { Name = "class", Type = typeof(ClassPatternConvertor) });
        _parameterisedObjectLogFile.AddConverter(new ConverterInfo() { Name = "tag", Type = typeof(TagPatternConvertor) });
        _parameterisedObjectLogFile.AddConverter(new ConverterInfo() { Name = "guid", Type = typeof(GuidPatternConvertor) });
        _parameterisedObjectLogFile.ActivateOptions();
      }
    }

    private log4net.Util.PatternString _parameterisedObjectLogFile;

    #endregion
  }

  #endregion

  #region ObjectAppenderQuietTextWriter

  /// <summary>
  /// A Custom text writer to track file udpates
  /// </summary>
  public class ObjectAppenderQuietTextWriter : CountingQuietTextWriter
  {

    #region constructors

    /// <summary>
    /// Default Constructor
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="errorHandler"></param>
    public ObjectAppenderQuietTextWriter(TextWriter writer, IErrorHandler errorHandler)
      : base(writer, errorHandler)
    {

    }

    #endregion

    #region Properties
/*
    /// <summary>
    /// The binary log file name, is updated each call as each binary blob saved is saved to a unique file
    /// </summary>
    public string BinaryLogFileName { get; set; }*/

    /// <summary>
    /// The object log file location, is updated each call as each binary blob saved is saved to a unique file, allows for custom file names to be applied
    /// </summary>
    public string ObjectLogPath { get; set; }

    /// <summary>
    /// The a parameterised log file name. Currently this supports the namespace, class, unique identifier (tag), and a guid
    /// </summary>
    public log4net.Util.PatternString ParameterisedObjectLogFile { get; set; }

    #endregion
  }

  #endregion

  #region BinaryConverter

  /// <summary>
  /// Binary Converter used to save binary blobs
  /// </summary>
  public class BinaryConverter : PatternConverter
  {
    /// <summary>
    /// Fall back file name is a custom name is not provided / available
    /// </summary>
    protected string FallBackLogFileName
    {
      get
      {
        return Properties.Contains("FallbackLogFileName") ? Properties["FallbackLogFileName"].ToString() : null;
      }
    }

    /// <summary>
    /// Saves binary blob to unique file and records this file to the base log file
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="state"></param>
    protected override void Convert(System.IO.TextWriter writer, object state)
    {
      var loggingEvent = state as LoggingEvent;

      if (loggingEvent == null)
      {
        writer.Write("Unable to serialize object, log entry was empty");
        return;
      }

      var tuple = loggingEvent.MessageObject as Tuple<ObjectLogFileKey, Dictionary<string, object>>;

      if (tuple != null)
      {
        ConvertAndSaveToProvidedFileName(writer, tuple);
        return;
      }

      var dictionary = loggingEvent.MessageObject as Dictionary<string, object>;

      if (dictionary != null)
      {
        ConvertAndSaveToAutomaticallyGeneratedFileName(writer, dictionary);
        return;
      }

      writer.Write(string.Format("Unable to record binary instance of an object of type: {0}.", state));
    }

    private void ConvertAndSaveToProvidedFileName(System.IO.TextWriter writer, Tuple<ObjectLogFileKey, Dictionary<string, object>> state)
    {
      var path = ((ObjectAppenderQuietTextWriter)writer).ObjectLogPath;

      var filename = LoadFileName(state.Item1, writer);

      var absoluteFilename = GetNextFileName(path, filename);
      ConvertAndSave(writer, absoluteFilename, state.Item2);
    }

    private void ConvertAndSaveToAutomaticallyGeneratedFileName(System.IO.TextWriter writer, Dictionary<string, object> state)
    {
      var path = ((ObjectAppenderQuietTextWriter)writer).ObjectLogPath;
      if (FallBackLogFileName == null)
      {
        writer.Write("No Custom or Fallback Log File Defined");
        return;
      }

      var logFileName = FallBackLogFileName;
      var filename = GetNextFileName(path, logFileName);
      ConvertAndSave(writer, filename, state);
    }

    private static string GetNextFileName(string path, string filename)
    {
      var sb = new StringBuilder();
      sb.Append(path);
      sb.Append(filename);
      return sb.ToString();
    }

    private static void ConvertAndSave(System.IO.TextWriter writer, string filename, Dictionary<string, object> state)
    {
      var msg = filename;

      // Ensure that the directory structure exists
      var directoryFullName = Path.GetDirectoryName(filename);

      if (directoryFullName == null)
      {
        return;
      }

      if (!Directory.Exists(directoryFullName))
      {
        Directory.CreateDirectory(directoryFullName);
      }

      using (var fileStream = System.IO.File.Open(filename, FileMode.OpenOrCreate))
      {
        try
        {
          var formatter = new BinaryFormatter();
          formatter.Serialize(fileStream, state);
        }
        catch (OutOfMemoryException e)
        {
          msg = string.Format("Unable to serialize Binary File {0}, successfully due to OutOfMemoryException: {1}", filename, e.Message);
        }
        catch (ArgumentException e)
        {
          msg = string.Format("Unable to serialize Binary File {0}, successfully to ArgumentException: {1}", filename, e.Message);
        }
        finally
        {
          fileStream.Close();      
        }
      }
      writer.Write(msg);
    }

    private static string LoadFileName(ObjectLogFileKey objectLogFileKey, System.IO.TextWriter writer)
    {
      InitParameterisedObjectLogFile(objectLogFileKey);
      var parameterisedObjectLogFile = ((ObjectAppenderQuietTextWriter)writer).ParameterisedObjectLogFile;

      var fileNameWriter = new StringWriter();
      parameterisedObjectLogFile.Format(fileNameWriter);

      return fileNameWriter.ToString();
    }

    private static void InitParameterisedObjectLogFile(ObjectLogFileKey objectLogFileKey)
    {
      objectLogFileKey.AssignParameterisedObjectLogFile();
    }

    /// <summary>
    /// This needs to be a property to allow for injection from the log4net config
    /// </summary>
    public string ParameterisedObjectLogFile { get; set; }
  }

  #endregion

  #region NamespacePatternConvertor

  /// <summary>
  /// Loads the namespace for the current object log
  /// </summary>
  public class NamespacePatternConvertor : PatternConverter
  {
    /// <summary>
    /// Loads the namespace for the current object log
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="state"></param>
    protected override void Convert(TextWriter writer, object state)
    {
      var currentNamespace = log4net.ThreadContext.Properties["namespace"] as string;
      if (currentNamespace != null)
      {
        writer.Write(currentNamespace);
      }
    }
  }

  #endregion

  #region ClassPatternConvertor

  /// <summary>
  /// Loads the class for the current object log
  /// </summary>
  public class ClassPatternConvertor : PatternConverter
  {
    /// <summary>
    /// Loads the class for the current object log
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="state"></param>
    protected override void Convert(TextWriter writer, object state)
    {
      var currentClass = log4net.ThreadContext.Properties["class"] as string;
      if (currentClass != null)
      {
        writer.Write(currentClass);
      }
    }
  }

  #endregion

  #region TagPatternConvertor

  /// <summary>
  /// Loads the tag for the current object log
  /// </summary>
  public class TagPatternConvertor : PatternConverter
  {
    /// <summary>
    /// Loads the tag for the current object log
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="state"></param>
    protected override void Convert(TextWriter writer, object state)
    {
      var currentTag = log4net.ThreadContext.Properties["tag"] as string;
      if (currentTag != null)
      {
        writer.Write(currentTag);
      }
    }
  }

  #endregion

  #region GuidPatternConvertor

  /// <summary>
  /// Loads a guid for the current object log
  /// </summary>
  public class GuidPatternConvertor : PatternConverter
  {
    /// <summary>
    /// Loads a guid for the current object log
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="state"></param>
    protected override void Convert(TextWriter writer, object state)
    {
      writer.Write(Guid.NewGuid());
    }
  }

  #endregion
}
