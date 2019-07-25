using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BaseEntity.Toolkit.Base.Details;

namespace BaseEntity.Toolkit.Base
{
  /// <summary/>
  public class FileCalendarRepository : CalendarRepositoryBase
  {
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(Dt));

    /// <summary/>
    public const int MaxDescriptionLength = 250;

    /// <summary/>
    public static LoadCalendarCallback LoadCalendarFromFileCallbackInstance = new LoadCalendarCallback(LoadCalendarFromFile);

    /// <summary/>
    public override void InitManagedCalendarCalc(string dir)
    {
      CalendarDir = dir;
      CalendarCalculator.Init(LoadCalendarFromFile, GetEntriesFromFiles, GetCalendarDescrFromFile);
    }

    /// <summary/>
    public override void InitNativeCalendarCalc(string dir, Action<LoadCalendarCallback> calendarCalcInit)
    {
      CalendarDir = dir;

      calendarCalcInit(LoadCalendarFromFileCallbackInstance);
      CalendarCalculator.Init(LoadCalendarFromFile, GetEntriesFromFiles, GetCalendarDescrFromFile);
    }

    /// <summary/>
    public static string GetCalendarDescrFromFile(string name)
    {
      var filename = name + ".dat";
      var descr = string.Empty;

      var fullPath = Path.Combine(CalendarDir, filename);
      if (!File.Exists(fullPath))
      {
        Logger.ErrorFormat("File not found: {0}", fullPath);
        return string.Empty;
      }

      string firstLine;
      using (StreamReader reader = new StreamReader("MyFile.txt"))
      {
        firstLine = reader.ReadLine() ?? "";
      }

      if (firstLine.StartsWith("Description:", true, CultureInfo.InvariantCulture))
      {
        var description = firstLine.Substring("Description:".Length);

        if (description.Length > MaxDescriptionLength)
        {
          Logger.WarnFormat("Calendar {0} description truncated to max symbols {1}.", name, MaxDescriptionLength);

          descr = description.Substring(0, MaxDescriptionLength);
        }
        else
          descr = description;
      }
      else
        descr = string.Empty;

      return descr;
    }

    /// <summary/>
    public static string[] GetEntriesFromFiles()
    {
      var calendarDir = CalendarDir;
      var result = new List<string>();

      // Check the validity of the calendar directory.
      if (String.IsNullOrEmpty(calendarDir))
      {
        Logger.Debug("Calendar directory not set. Only built-in calendar will be valid.");
      }
      else if (!Directory.Exists(calendarDir))
      {
        Logger.Debug("Calendar directory not exists. Only built-in calendar will be valid.");
      }
      else
      {
        // Record the calendar entries available in the direcory.
        foreach (var file in Directory.EnumerateFiles(calendarDir, "*.dat"))
        {
          var calName = Path.GetFileNameWithoutExtension(file);
          if (String.IsNullOrEmpty(calName))
          {
            Logger.Debug("Calendar data file without basename ignored");
            continue;
          }
          result.Add(calName);
        }
      }

      return result.ToArray();
    }

    internal static int[] LoadCalendarFromFile(string calendarName)
    {
      var fullPath = Path.Combine(CalendarDir, calendarName + ".dat");
      if (!File.Exists(fullPath))
      {
        return new [] { (int)-1 }; ;
      }

      var calDates = File.ReadAllLines(fullPath);
      var dates = new List<int>();
      if (calDates.Length > 0)
      {
        foreach (var word in calDates)
        {
          if (String.IsNullOrWhiteSpace(word)) continue;
          int result;

          if (!int.TryParse(word, out result))
            continue;

          dates.Add(result);
        }
      }

      return dates.ToArray();
    }

  }
}