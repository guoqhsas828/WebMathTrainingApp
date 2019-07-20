/*
 * CmdLineParser.cs -
 *
 * Copyright (c) WebMathTraining 2008. All rights reserved.
 *
 * $Id: CmdLineParser.cs,v 1.30 2006/11/16 22:55:27 rnagamalla Exp $
 *
 * TODO:
 *   o Add support for grouped single char options (e.g. -bc => -b -c)
 *   o Add support for keyword substitutions in usage text (e.g. for default values)
 *   o Check for conflicts in AddOption()
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.IO;
using System.Text;


namespace BaseEntity.Configuration
{
  /// <summary>
  ///   Delegate used to report errors or usage
  /// </summary>
  public delegate void ErrorReporter(string message);

  /// <summary>
  ///   This class provides a standard way for handling command-line
  ///   parameters.
  /// </summary>
  /// 
  /// <example>
  /// <para>The following example demonstrates using parsing
  /// command-line arguments</para>
  /// <code language="C#">
  ///   static int Main(string[] args)
  ///   {
  ///     // Initialise
  ///     RiskConfigurator.Configure();
  ///
  ///     // Set up command line arguments
  ///     CmdLineParser parser = new CmdLineParser();
  ///     parser.AddDateTimeOption( "-d|--date", "date", "As-of date" );
  ///     parser.AddStringOption( "-s|--server", "url", "https://www.markit.com/export.jsp", "URL of Markit download page. [default: %default]" );
  ///     parser.AddInt32Option( "-v|--report-version", "version", "Version of report" );
  ///
  ///     // Initialise and parse args
  ///     if ( !parser.ParseArgs() )
  ///       return 1;
  /// 
  ///     // ...
  ///   }
  /// </code>
  /// </example>
  /// 
  public class CmdLineParser
  {
    #   region Constructors

    /// <summary>
    ///  Construct a default command line parser
    /// </summary>
    public CmdLineParser()
      : this(Console.Error.WriteLine)
    {
    }

    /// <summary>
    ///  Construct a command line parser with the specified error reporter
    /// </summary>
    /// 
    /// <param name="reporter">Error reporting function</param>
    /// 
    public CmdLineParser(ErrorReporter reporter)
    {
      reporter_ = reporter;
      optionList_ = new List<CmdLineOption>();
      optionMap_ = new Dictionary<string, CmdLineOption>();
      optArgs_ = new Dictionary<string, object>();
      posArgs_ = new List<string>();

      // Add default arguments
      if (standardOptionList_ != null)
      {
        foreach (CmdLineOption opt in standardOptionList_)
          AddOption(opt);
      }
    }

    #   endregion Constructors

    #   region Methods

    /// <summary>
    ///   Set the standard options for any command line parser. Typically
    ///   this is set as part of an application initialisation.
    /// </summary>
    /// 
    /// <param name="options">List of standard options</param>
    /// 
    public static void SetStandardOptions(List<CmdLineOption> options)
    {
      standardOptionList_ = options;
    }

    /// <summary>
    ///   Get the standard options for any command line parser. Typically
    ///   this is set as part of an application initialisation.
    /// </summary>
    /// 
    /// 
    public static List<CmdLineOption> GetStandardOptions()
    {
      return standardOptionList_;
    }

    /// <summary>
    ///   Add boolean option with default value of true
    /// </summary>
    ///
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Name used to identify option</param>
    /// <param name="helpText">Description for argument for help</param>
    /// 
    public void AddBooleanOption(string names,
                                 string valueName,
                                 string helpText)
    {
      this.AddOption(new BooleanCmdLineOption(names, valueName, true, helpText));
    }


    /// <summary>
    ///   Add boolean option
    /// </summary>
    /// 
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Name used to identify option</param>
    /// <param name="defaultValue">Default value for option</param>
    /// <param name="helpText">Description for argument for help</param>
    /// 
    public void AddBooleanOption(string names,
                                 string valueName,
                                 bool defaultValue,
                                 string helpText)
    {
      helpText = helpText.Replace("%default", defaultValue.ToString());
      this.AddOption(new BooleanCmdLineOption(names, valueName, true, helpText));
      SetValue(valueName, defaultValue);
    }


    /// <summary>
    ///   Add boolean option
    /// </summary>
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Name used to identify option</param>
    /// <param name="defaultValue">Default value for option</param>
    /// <param name="valueIfSet">Value if option set</param>
    /// <param name="helpText">Description for argument for help</param>
    ///
    public void AddBooleanOption(string names,
                                 string valueName,
                                 bool defaultValue,
                                 bool valueIfSet,
                                 string helpText)
    {
      helpText = helpText.Replace("%default", defaultValue.ToString());
      this.AddOption(new BooleanCmdLineOption(names, valueName, valueIfSet, helpText));
      SetValue(valueName, defaultValue);
    }


    /// <summary>
    ///   Add enum option
    /// </summary>
    /// 
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Name used to identify option</param>
    /// <param name="helpText">Description for argument for help</param>
    /// 
    public void AddEnumOption<T>(string names,
                                 string valueName,
                                 string helpText)
    {
      //Appending all the enum values in place of the '%values' keyword in the help text.
      if (helpText.Contains("%values"))
      {
        Array values = Enum.GetValues(typeof(T));
        string expectedValues = "";
        for (int cnt = 0; cnt < values.Length; cnt++)
        {
          expectedValues += values.GetValue(cnt) + ", ";
        }
        expectedValues = expectedValues.Trim().TrimEnd(new char[] { ',' });
        helpText = helpText.Replace("%values", expectedValues);
      }
      AddOption(new EnumCmdLineOption<T>(names, valueName, helpText));
    }


    /// <summary>
    ///   Add enum option
    /// </summary>
    ///
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Name used to identify option</param>
    /// <param name="defaultValue">Default value for option</param>
    /// <param name="helpText">Description for argument for help</param>
    /// 
    public void AddEnumOption<T>(string names,
                                 string valueName,
                                 T defaultValue,
                                 string helpText)
    {
      if (defaultValue != null)
        helpText = helpText.Replace("%default", defaultValue.ToString());

      AddEnumOption<T>(names, valueName, helpText);
      SetValue(valueName, defaultValue);
    }


    /// <summary>
    ///   Add string option
    /// </summary>
    /// 
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Name used to identify option</param>
    /// <param name="helpText">Description for argument for help</param>
    /// 
    public void AddStringOption(string names,
                                string valueName,
                                string helpText)
    {
      AddOption(new StringCmdLineOption(names, valueName, helpText));
    }


    /// <summary>
    ///   Add string option
    /// </summary>
    /// 
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Name used to identify option</param>
    /// <param name="defaultValue">Default value for option</param>
    /// <param name="helpText">Description for argument for help</param>
    /// 
    public void AddStringOption(string names,
                                string valueName,
                                string defaultValue,
                                string helpText)
    {
      if (defaultValue != null)
        helpText = helpText.Replace("%default", defaultValue);

      AddStringOption(names, valueName, helpText);
      SetValue(valueName, defaultValue);
    }


    /// <summary>
    ///   Add DateTime option
    /// </summary>
    /// 
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Name used to identify option</param>
    /// <param name="helpText">Description for argument for help</param>
    /// 
    public void AddDateTimeOption(string names,
                                  string valueName,
                                  string helpText)
    {
      AddOption(new DateTimeCmdLineOption(names, valueName, helpText));
    }


    /// <summary>
    ///   Add DateTime option
    /// </summary>
    /// 
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Name used to identify option</param>
    /// <param name="defaultValue">Default value for option</param>
    /// <param name="helpText">Description for argument for help</param>
    /// 
    public void AddDateTimeOption(string names,
                                  string valueName,
                                  DateTime defaultValue,
                                  string helpText)
    {
      AddDateTimeOption(names, valueName, helpText);
      SetValue(valueName, defaultValue);
    }


    /// <summary>
    ///   Add Int32 option
    /// </summary>
    /// 
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Name used to identify option</param>
    /// <param name="helpText">Description for argument for help</param>
    /// 
    public void AddInt32Option(string names,
                               string valueName,
                               string helpText)
    {
      AddOption(new Int32CmdLineOption(names, valueName, helpText));
    }


    /// <summary>
    ///   Add Int32 option
    /// </summary>
    /// 
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Name used to identify option</param>
    /// <param name="defaultValue">Default value for option</param>
    /// <param name="helpText">Description for argument for help</param>
    /// 
    public void AddInt32Option(string names,
                               string valueName,
                               int defaultValue,
                               string helpText)
    {
      helpText = helpText.Replace("%default", defaultValue.ToString());
      AddInt32Option(names, valueName, helpText);
      SetValue(valueName, defaultValue);
    }


    /// <summary>
    ///   Add Int64 option
    /// </summary>
    /// 
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Name used to identify option</param>
    /// <param name="helpText">Description for argument for help</param>
    /// 
    public void AddInt64Option(string names,
                               string valueName,
                               string helpText)
    {
      AddOption(new Int64CmdLineOption(names, valueName, helpText));
    }


    /// <summary>
    ///   Add integer option
    /// </summary>
    /// 
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Name used to identify option</param>
    /// <param name="defaultValue">Default value for option</param>
    /// <param name="helpText">Description for argument for help</param>
    /// 
    public void AddInt64Option(string names,
                               string valueName,
                               long defaultValue,
                               string helpText)
    {
      helpText = helpText.Replace("%default", defaultValue.ToString());
      AddInt64Option(names, valueName, helpText);
      SetValue(valueName, defaultValue);
    }


    /// <summary>
    ///   Add double option
    /// </summary>
    /// 
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Name used to identify option</param>
    /// <param name="helpText">Description for argument for help</param>
    /// 
    public void AddDoubleOption(string names,
                                string valueName,
                                string helpText)
    {
      AddOption(new DoubleCmdLineOption(names, valueName, helpText));
    }


    /// <summary>
    ///   Add double option
    /// </summary>
    /// 
    /// <param name="names">Sequence of pipe-separated options flags</param>
    /// <param name="valueName">Name used to identify option</param>
    /// <param name="defaultValue">Default value for option</param>
    /// <param name="helpText">Description for argument for help</param>
    /// 
    public void AddDoubleOption(string names,
                                string valueName,
                                double defaultValue,
                                string helpText)
    {
      helpText = helpText.Replace("%default", defaultValue.ToString());
      AddDoubleOption(names, valueName, helpText);
      SetValue(valueName, defaultValue);
    }


    /// <summary>
    ///   Add option
    /// </summary>
    /// 
    /// <param name="opt"></param>
    /// 
    public void AddOption(CmdLineOption opt)
    {
      string[] names = opt.Names.Split(new char[] { '|' });
      foreach (string name in names)
      {
        if (!IsValidName(name))
          throw new ArgumentException(String.Format("'{0}' is not a valid short or long option name", name));
      }

      // TODO: check for conflicts

      optionList_.Add(opt);

      foreach (string name in names)
      {
        optionMap_[name] = opt;
      }
    }

    private string[] SplitArgs(string[] args)
    {
      List<string> argList = new List<string>();

      for (int i = 0; i < args.Length; i++)
      {
        string arg = args[i];

        if (arg.StartsWith("--"))
        {
          int idx = arg.IndexOf('=');
          if (idx > -1)
          {
            argList.Add(arg.Substring(0,idx));
            argList.Add(arg.Substring(idx + 1));
          }
          else
          {
            argList.Add(arg);
          }
        }
        else if (arg.StartsWith("-"))
        {
          char[] opts = arg.ToCharArray(1, arg.Length - 1);
          foreach (char opt in opts)
          {
            argList.Add(String.Format("-{0}", opt));
          }
        }
        else
        {
          argList.Add(arg);
        }
      }

      return argList.ToArray();
    }


    /// <summary>
    ///  Parse arguments to current process
    /// </summary>
    /// 
    /// <returns>False if some error</returns>
    /// 
    public bool ParseArgs()
    {
      string[] cmdLineArgs = Environment.GetCommandLineArgs();

      int count = cmdLineArgs.Length - 1;
      string[] args = new string[count];
      Array.Copy(cmdLineArgs, 1, args, 0, count);

      return ParseArgs(args);
    }


    /// <summary>
    ///   Parse specified arguments
    /// </summary>
    /// 
    /// <param name="args"></param>
    /// 
    /// <returns>False if some error</returns>
    /// 
    public bool ParseArgs(ArrayList args)
    {
      return ParseArgs((string[])args.ToArray(typeof(string)));
    }


    /// <summary>
    ///   Parse specified arguments
    /// </summary>
    ///
    /// <param name="args"></param>
    /// 
    /// <returns>False if some error</returns>
    public bool ParseArgs(string[] args)
    {
      //Rama: Modified to accept multiple options to be merged together. ex: -b -a can be merged as -ba
      string[] validArgs = SplitArgs(args);
      int startIdx = 0;

      for (int i = 0; i < validArgs.Length; i++)
      {
        string arg = validArgs[i];

        if (IsValidName(arg))
        {
          if (!optionMap_.ContainsKey(arg))
          {
            reporter_(String.Format("Invalid command line option: {0}", arg));
            DisplayUsage();
            return false;
          }
          else
          {
            CmdLineOption opt = (CmdLineOption)optionMap_[arg];

            string[] optArgs;
            if (opt.NumArgs == 0)
              optArgs = null;
            else
            {
              //getting the index of the arguments to the options   
              //If the option starts with a -- then its argument if supplied will be the next immediate element in the array.
              //If the option starts with a - it can a single option or multiple aoptions merged together.
              if (arg.StartsWith("--"))
                startIdx = i + 1;
              else
              {
                for (int cnt = startIdx; cnt < validArgs.Length; cnt++)
                {
                  if (!validArgs[cnt].StartsWith("-"))
                  {
                    startIdx = cnt;
                    break;
                  }
                }
              }

              optArgs = new string[opt.NumArgs];
              try
              {
                for (int j = 0; j < optArgs.Length; j++)
                {
                  if (!validArgs[j + startIdx].StartsWith("-"))
                    optArgs[j] = validArgs[j + startIdx];
                  else
                    throw new CmdLineParserException(String.Format("Option '{0}' has invalid number of arguments.", validArgs[i]));
                }
              }
              catch (CmdLineParserException ex) // if for a particular option, the number of arguments supplied is less than the actual number required.
              {
                reporter_(ex.ToString());
                DisplayUsage();
                return false;
              }
              catch (IndexOutOfRangeException) // when an argument is missed for the last option
              {
                reporter_(String.Format("Missing argument for '{0}' option", validArgs[i]));
                return false;
              }
              startIdx += opt.NumArgs;
            }

            try
            {
              bool result = opt.Parse(this, optArgs);
              if (!result)
              {
                // Do not process any more options
                return false;
              }
            }
            catch (CmdLineParserException /*ex*/ )
            {
              DisplayUsage();
              return false;
            }
          }
        }
        else
        {
          // Treat as positional argument
          if (i >= startIdx)
          {
            posArgs_.Add(arg.Trim());
            startIdx += 1;
          }
        }
      }

      return true;
    }


    /// <summary>
    ///   Set the current value for an option
    /// </summary>
    /// 
    /// <param name="valueName"></param>
    /// <param name="value"></param>
    /// 
    public void SetValue(string valueName, object value)
    {
      optArgs_[valueName] = value;
    }


    /// <summary>
    ///   Test if an option has a value
    /// </summary>
    /// 
    /// <param name="valueName">Name of option to test</param>
    /// 
    /// <returns>True if option has a value</returns>
    /// 
    public bool HasValue(string valueName)
    {
      return optArgs_.ContainsKey(valueName);
    }


    /// <summary>
    ///   Get the value of an option
    /// </summary>
    /// 
    /// <param name="valueName">Name of option to retrieve</param>
    /// 
    /// <returns>Value of specified option</returns>
    /// 
    public object GetValue(string valueName)
    {
      if (HasValue(valueName))
        return optArgs_[valueName];

      throw new CmdLineParserException("Invalid name: " + valueName);
    }


    /// <summary>
    ///   Get positional arguments
    /// </summary>
    /// 
    /// <returns>List of arguments</returns>
    /// 
    public string[] GetArguments()
    {
      return posArgs_.ToArray();
    }


    /// <summary>
    ///   Return true if this is a valid short name or long name
    /// </summary>
    /// 
    /// <param name="optName">Option flag</param>
    /// 
    /// <returns>True if this is a valid option flag</returns>
    /// 
    private bool IsValidName(string optName)
    {
      if (optName.StartsWith("--"))
      {
        return (optName.Length == 2) ?
        false : true;
      }
      else if (optName.StartsWith("-"))
      {
        return (optName.Length != 2) ?
        false : true;
      }

      return false;
    }


    /// <summary>
    ///   Display usage for this argument list
    /// </summary>
    ///
    /// <returns>1</returns>
    /// 
    public int DisplayUsage()
    {
      StringBuilder sb = new StringBuilder();

      sb.Append(Environment.NewLine);

      if (this.SampleUsage != null)
      {
        sb.Append(String.Format("Usage: {0}{1}", this.SampleUsage, Environment.NewLine));
      }
      else
      {
        string fileName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
        sb.Append(String.Format("Usage: {0} [options]{1}", fileName, Environment.NewLine));
      }



      List<string> optList = new List<string>();

      foreach (CmdLineOption opt in optionList_)
      {
        string[] names = opt.Names.Split(new char[] { '|' });
        StringBuilder temp = new StringBuilder();

        for (int i = 0; i < names.Length; i++)
        {
          if (i > 0)
            temp.Append(", ");
          string name = names[i];
          temp.Append(name);
          if (opt.NumArgs > 0)
          {
            if (name.StartsWith("--"))
              temp.Append("=");
            else
              temp.Append(" ");
            temp.Append(opt.ValueName.ToUpper());
          }
        }
        optList.Add(temp.ToString());
      }

      int colWidth = 0;
      foreach (string buf in optList)
      {
        colWidth = Math.Max(buf.Length, colWidth);
      }
      colWidth = Math.Max(8, colWidth);
      int descripColWidth = 80 - colWidth - 2;

      //Description for all the options.
      StringBuilder optDescripStr = new StringBuilder();

      int maxLength = 1;

      for (int i = 0; i < optionList_.Count; i++)
      {
        CmdLineOption opt = optionList_[i];
        StringBuilder temp = new StringBuilder();

        temp.Append(optList[i].PadRight(colWidth));
        if (opt.HelpText != null)
        {
          //Splitting the Helptext according to the descripColWidth value and appending them to sb
          int length = descripColWidth;
          string[] words = GetArray(opt.HelpText, descripColWidth);

          string line = "";
          bool first = true;
          foreach (String wrd in words)
          {
            line += " " + wrd;
            if (line.Length > length)
            {
              line = line.Substring(0, line.Length - wrd.Length - 1);

              if (first)
              {
                temp.Append(String.Format(" {0}{1}", line, Environment.NewLine));
                first = false;
              }
              else
                temp.AppendLine(line.PadLeft(colWidth + line.Length + 2));
              line = wrd;
            }
            //Caculating the maximum length of the decription for all the options.
            //Setting maxLength to be max of either the longest description or console width.
            maxLength = System.Math.Max(maxLength, line.Length);
          }
          if (line.Length > 0)
          {
            if (first)
              temp.Append(String.Format(" {0}{1}", line, Environment.NewLine));
            else
              temp.AppendLine(line.PadLeft(colWidth + line.Length + 2));
          }
          maxLength = System.Math.Max(maxLength, line.Length);
        }
        optDescripStr.Append(temp.ToString());
      }

      sb.Append(String.Format("{0}{1}{0}", Environment.NewLine, "-".PadLeft(colWidth + maxLength + 1, '-')));
      sb.Append(String.Format("{0}{1}", "OPTIONS".PadRight(colWidth), "DESCRIPTION".PadLeft(13)));
      sb.Append(String.Format("{0}{1}{0}", Environment.NewLine, "-".PadLeft(colWidth + maxLength + 1, '-')));
      sb.Append(optDescripStr.ToString());

      if (remarks_ != null)
      {
        sb.Append(Environment.NewLine);
        sb.Append(remarks_);
      }

      reporter_(sb.ToString());

      return 1;
    }

    /// <summary>
    ///    Splits the given text into an array of strings using " " as split character.
    ///    If any of the conatining words is bigger than the length parameter, it breaks the words 
    ///    into multiple words.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    private string[] GetArray(string text, int length)
    {
      string[] temp = text.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
      IList wordsList = new ArrayList();
      for (int cnt = 0; cnt < temp.Length; cnt++)
      {
        if (temp[cnt].Length <= length)
          wordsList.Add(temp[cnt]);
        else
        {
          bool stop = false;
          int startIndex = 0;
          while (!stop)
          {
            wordsList.Add(temp[cnt].Substring(startIndex, length - 1));
            startIndex += length;
            if (temp[cnt].Substring(startIndex).Length < length)
              stop = true;
          }
          wordsList.Add(temp[cnt].Substring(startIndex));
        }
      }
      string[] list = new string[wordsList.Count];
      wordsList.CopyTo(list, 0);
      return (list);
    }

    #   endregion Methods

    #   region Properties

    /// <summary>
    ///   Sample command line (displayed as part of usage text)
    /// </summary>
    public string SampleUsage
    {
      get { return sampleUsage_; }
      set { sampleUsage_ = value; }
    }


    /// <summary>
    ///   Remarks for this argument list
    /// </summary>
    public string Remarks
    {
      get { return remarks_; }
      set { remarks_ = value; }
    }


    /// <summary>
    ///   Delegate used to display error/usage text
    /// </summary>
    public ErrorReporter Reporter
    {
      get { return reporter_; }
    }

    #   endregion Properties

    #   region Data

    private string remarks_;
    private string sampleUsage_;
    private ErrorReporter reporter_;
    private List<CmdLineOption> optionList_;
    private Dictionary<string, CmdLineOption> optionMap_;
    private Dictionary<string, object> optArgs_;
    private List<string> posArgs_;
    private static List<CmdLineOption> standardOptionList_;

    #   endregion Data
  }

  /// <summary>
  ///  Base Exception class for the command line parser.
  /// </summary>
  [Serializable]
  public class CmdLineParserException : Exception
  {
    /// <summary>
    ///   Create default instance
    /// </summary>
    public CmdLineParserException()
    {
    }


    /// <summary>
    ///   Create new instance with the given message
    /// </summary>
    public CmdLineParserException(string message)
      : base(message)
    {
    }


    /// <summary>
    ///   Create new instance with given message and inner exception
    /// </summary>
    /// <remarks>
    ///   This is used by the AnalyticService to wrap exceptions
    ///   thrown by the Toolkit.
    /// </remarks>
    public CmdLineParserException(string message, Exception inner)
      : base(message, inner)
    {
    }


    /// <summary>
    ///   Required for serialization support
    /// </summary>
    public CmdLineParserException(SerializationInfo info, StreamingContext context)
      : base(info, context)
    {
    }
  }
}
