/*
 * CmdLineOption.cs -
 *
 *
 */

using System;
using System.Diagnostics;
using System.Linq;

namespace BaseEntity.Configuration
{
  /// <summary>
  ///   Abstract base class for a single command line option.
  ///   Used by CmdLineParser
  /// </summary>
  public abstract class CmdLineOption
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="helpText">Help text for option</param>
    protected CmdLineOption(string names, string helpText)
    {
      names_ = names;
      helpText_ = helpText;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Sequence of pipe-separated option flags for this option
    ///   (names must conform to short/long name rules)
    /// </summary>
    public string Names
    {
      get { return names_; }
    }


    /// <summary>
    ///   Usage text (optional)
    /// </summary>
    public string HelpText
    {
      get { return helpText_; }
    }


    /// <summary>
    ///   Number of arguments expected by this option (typically 0 or 1)
    /// </summary>
    public abstract int NumArgs { get; }


    /// <summary>
    ///   Indicates if this option is used to set a named value
    /// </summary>
    public bool HasValue
    {
      get { return (ValueType == null) ? false : true; }
    }

    /// <summary>
    ///   Abstract Property to be overriden in sub classes
    /// </summary>
    public abstract Type ValueType { get; }

    /// <summary>
    ///   Abstract Property to be overriden in sub classes
    /// </summary>
    public abstract string ValueName { get; }

    #endregion Properties

    #region Methods

    /// <summary>
    ///    Abstract method to be implemented by all the sub classes
    /// </summary>
    /// <param name="parser"></param>
    /// <param name="optArgs"></param>
    /// <returns></returns>
    public abstract bool Parse(CmdLineParser parser, string[] optArgs);

    /// <summary>
    /// Trim extra characters from cmdline argument - i.e '"', '\'', ' '
    /// </summary>
    /// <param name="optArgs"></param>
    public void Trim(string[] optArgs)
    {
      char[] trimmable = { '\'', '"', ' ' };
      var arg = optArgs[0];
      if (arg.Length <= 2)
        return;
      var first = arg.First();
      var last = arg.Last();
      if (first == last && trimmable.Contains(first))
      {
        optArgs[0] = arg.Trim(first);
      }
    }

    #endregion Methods

    #region Data

    private string names_;
    private string helpText_;

    #endregion Data
  }

  /// <summary>
  ///   Boolean command-line option
  /// </summary>
  public class BooleanCmdLineOption : CmdLineOption
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Value name</param>
    /// <param name="valueIfSet">Value if set</param>
    /// <param name="helpText">Help text for option</param>
    public BooleanCmdLineOption(string names,
                                string valueName,
                                bool valueIfSet,
                                string helpText)
      :
      base(names, helpText)
    {
      valueName_ = valueName;
      valueIfSet_ = valueIfSet;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Indicates that boolean option takes no arguments
    /// </summary>
    public override int NumArgs
    {
      get { return 0; }
    }

    /// <summary>
    ///   Value type
    /// </summary>
    public override Type ValueType
    {
      get { return typeof(bool); }
    }

    /// <summary>
    ///   Value name
    /// </summary>
    public override string ValueName
    {
      get { return valueName_; }
    }

    /// <summary>
    ///   Parse option arguments
    /// </summary>
    /// <param name="parser"></param>
    /// <param name="optArgs"></param>
    /// <returns>bool</returns>
    public override bool Parse(CmdLineParser parser, string[] optArgs)
    {
      parser.SetValue(valueName_, valueIfSet_);

      return true;
    }

    #endregion

    #region Data

    private string valueName_;
    private bool valueIfSet_;

    #endregion
  }


  /// <summary>
  ///   Base class for all standard command line option types
  ///   that take a single value as argument to the option.
  /// </summary>
  public abstract class StandardCmdLineOption : CmdLineOption
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Value name</param>
    /// <param name="helpText">Help text for option</param>
    public StandardCmdLineOption(string names,
                                 string valueName,
                                 string helpText)
      :
      base(names, helpText)
    {
      valueName_ = valueName;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Number of arguments
    /// </summary>
    public override int NumArgs
    {
      get { return 1; }
    }

    /// <summary>
    ///   Value name
    /// </summary>
    public override string ValueName
    {
      get { return valueName_; }
    }

    #endregion Properties

    #region Data

    private string valueName_;

    #endregion Data
  }

  /// <summary>
  ///   String command option
  /// </summary>
  public class StringCmdLineOption : StandardCmdLineOption
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Value name</param>
    /// <param name="helpText">Help text for option</param>
    public StringCmdLineOption(string names,
                               string valueName,
                               string helpText)
      :
      base(names, valueName, helpText)
    {
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Value name
    /// </summary>
    public override Type ValueType
    {
      get { return typeof(string); }
    }

    /// <summary>
    ///   Parse option arguments
    /// </summary>
    /// <param name="parser"></param>
    /// <param name="optArgs"></param>
    /// <returns>bool</returns>
    public override bool Parse(CmdLineParser parser, string[] optArgs)
    {
        Trim(optArgs);
      parser.SetValue(ValueName, optArgs[0]);
      return true;
    }

    #endregion
  }

  /// <summary>
  ///   Enumeration command-line option
  /// </summary>
  public class EnumCmdLineOption<T> : StandardCmdLineOption
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Value name</param>
    /// <param name="helpText">Help text for option</param>
    public EnumCmdLineOption(string names,
                             string valueName,
                             string helpText)
      :
      base(names, valueName, helpText)
    {
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Value type
    /// </summary>
    public override Type ValueType
    {
      get { return typeof(T); }
    }

    #endregion Properties

    #region Methods

    /// <summary>
    ///   Parse option arguments
    /// </summary>
    /// <param name="parser"></param>
    /// <param name="optArgs"></param>
    /// <returns>bool</returns>
    public override bool Parse(CmdLineParser parser, string[] optArgs)
    {
      Trim(optArgs);
      try
      {
        parser.SetValue(ValueName, (T)Enum.Parse(typeof(T), optArgs[0], false));
        return true;
      }
      catch (Exception /*ex*/)
      {
        parser.Reporter(String.Format("Invalid {0} value: {1}", typeof(T).Name, optArgs[0]));
        return false;
      }
    }

    #endregion Methods
  }

  /// <summary>
  ///   DateTime command-line option
  /// </summary>
  public class DateTimeCmdLineOption : StandardCmdLineOption
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName"></param>
    /// <param name="helpText">Help text for option</param>
    public DateTimeCmdLineOption(string names,
                                 string valueName,
                                 string helpText)
      :
      base(names, valueName, helpText)
    {
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Value type
    /// </summary>
    public override Type ValueType
    {
      get { return typeof(DateTime); }
    }

    #endregion Properties

    #region Methods

    /// <summary>
    ///   Parse option arguments
    /// </summary>
    /// <param name="parser"></param>
    /// <param name="optArgs"></param>
    /// <returns>bool</returns>
    public override bool Parse(CmdLineParser parser, string[] optArgs)
    {
      DateTime dt;
      Trim(optArgs);
      bool success = DateTime.TryParse(optArgs[0], out dt);
      if (success)
        parser.SetValue(ValueName, dt);
      else
      {
        parser.Reporter(String.Format(
          "Invalid DateTime value: {0}", optArgs[0]));
      }

      return success;
    }

    #endregion
  }

  /// <summary>
  ///   Double command-line option
  /// </summary>
  public class DoubleCmdLineOption : StandardCmdLineOption
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName"></param>
    /// <param name="helpText">Help text for option</param>
    public DoubleCmdLineOption(string names,
                               string valueName,
                               string helpText)
      :
      base(names, valueName, helpText)
    {
    }

    #endregion

    #region Properties

    /// <summary>
    /// Value type
    /// </summary>
    public override Type ValueType
    {
      get { return typeof(double); }
    }

    #endregion Properties

    #region Methods

    /// <summary>
    ///   Parse option arguments
    /// </summary>
    /// <param name="parser"></param>
    /// <param name="optArgs"></param>
    /// <returns>bool</returns>
    public override bool Parse(CmdLineParser parser, string[] optArgs)
    {
      double dval;
      Trim(optArgs);
      bool success = double.TryParse(optArgs[0], out dval);
      if (success)
        parser.SetValue(ValueName, dval);
      else
      {
        parser.Reporter(String.Format(
          "Invalid Double value: {0}", optArgs[0]));
      }

      return success;
    }

    #endregion
  }

  /// <summary>
  ///   Int32 command-line option
  /// </summary>
  public class Int32CmdLineOption : StandardCmdLineOption
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName"></param>
    /// <param name="helpText">Help text for option</param>
    public Int32CmdLineOption(string names,
                              string valueName,
                              string helpText)
      :
      base(names, valueName, helpText)
    {
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Value type
    /// </summary>
    public override Type ValueType
    {
      get { return typeof(int); }
    }

    /// <summary>
    ///   Parse option arguments
    /// </summary>
    /// <param name="parser"></param>
    /// <param name="optArgs"></param>
    /// <returns>bool</returns>
    public override bool Parse(CmdLineParser parser, string[] optArgs)
    {
      int ival;
      Trim(optArgs);
      bool success = int.TryParse(optArgs[0], out ival);
      if (success)
        parser.SetValue(ValueName, ival);
      else
      {
        parser.Reporter(String.Format(
          "Invalid Int32 value: {0}", optArgs[0]));
      }

      return success;
    }

    #endregion
  }

  /// <summary>
  ///   Int64 command-line option
  /// </summary>
  public class Int64CmdLineOption : StandardCmdLineOption
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName"></param>
    /// <param name="helpText">Help text for option</param>
    public Int64CmdLineOption(string names,
                              string valueName,
                              string helpText)
      :
      base(names, valueName, helpText)
    {
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Value type
    /// </summary>
    public override Type ValueType
    {
      get { return typeof(long); }
    }

    /// <summary>
    ///   Parse option arguments
    /// </summary>
    /// <param name="parser"></param>
    /// <param name="optArgs"></param>
    /// <returns>bool</returns>
    public override bool Parse(CmdLineParser parser, string[] optArgs)
    {
      long value;
      Trim(optArgs);
      bool success = long.TryParse(optArgs[0], out value);
      if (success)
        parser.SetValue(ValueName, value);
      else
      {
        parser.Reporter(String.Format(
          "Invalid Int64 value: {0}", optArgs[0]));
      }

      return success;
    }

    #endregion
  }

  /// <summary>
  ///   This class is used to process standard help options ("-h", "-?", "--help")
  /// </summary>
  public class HelpCmdLineOption : CmdLineOption
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="helpText">Help text for option</param>
    public HelpCmdLineOption(string names, string helpText)
      : base(names, helpText)
    {}

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Number of arguments accepted by this command line option
    /// </summary>
    public override int NumArgs
    {
      get { return 0; }
    }

    /// <summary>
    ///   Value Type
    /// </summary>
    public override Type ValueType
    {
      get { return null; }
    }

    /// <summary>
    ///   Value Name.
    /// </summary>
    public override string ValueName
    {
      get { return null; }
    }

    #endregion Properties

    #region Methods

    /// <summary>
    ///   Parse option arguments
    /// </summary>
    /// <param name="parser"></param>
    /// <param name="optArgs"></param>
    /// <returns>bool</returns>
    public override bool Parse(CmdLineParser parser, string[] optArgs)
    {
      parser.DisplayUsage();

      return false;
    }

    #endregion Methods
  }

  /// <summary>
  ///   Version command-line option
  /// </summary>
  public class VersionCmdLineOption : CmdLineOption
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="helpText">Help text for option</param>
    public VersionCmdLineOption(string names, string helpText)
      : base(names, helpText)
    {}

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Number of arguments accepted by this command line option
    /// </summary>
    public override int NumArgs
    {
      get { return 0; }
    }

    /// <summary>
    /// Value Type
    /// </summary>
    public override Type ValueType
    {
      get { return null; }
    }

    /// <summary>
    /// Value Name
    /// </summary>
    public override string ValueName
    {
      get { return null; }
    }

    #endregion Properties

    #region Methods

    /// <summary>
    ///   Parse option arguments
    /// </summary>
    /// <param name="parser"></param>
    /// <param name="optArgs"></param>
    /// <returns>bool</returns>
    public override bool Parse(CmdLineParser parser, string[] optArgs)
    {
      ProcessModule module = Process.GetCurrentProcess().MainModule;
      FileVersionInfo fvi = module.FileVersionInfo;
      parser.Reporter(fvi.FileVersion);

      return false;
    }

    #endregion Properties
  }
}
