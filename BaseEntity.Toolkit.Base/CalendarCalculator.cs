using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Dow = System.DayOfWeek;

namespace BaseEntity.Toolkit.Base.Details
{
  /// <summary>
  ///  Calendar calculation helpers class, pure managed implementation.
  /// </summary>
  public static class CalendarCalculator
  {
    #region Public methods
    /// <summary>
    ///  Get the name of the specified calendar.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <returns>System.String.</returns>
    public static string CalendarName(Calendar calendar)
    {
      return Calendars.GetEntryName(calendar.Id);
    }

    /// <summary/>
    public static Func<string[]>  GetValidCalendars { get; private set; }

    /// <summary/>
    public static Func<string, string> GetCalendarDescription { get; private set; }

    /// <summary>
    /// Gets the calendar from associated with the specified name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>Calendar.</returns>
    public static Calendar GetCalendar(string name)
    {
      return new Calendar(Calendars.GetEntryId(name));
    }

    /// <summary>
    /// Determines whether the specified calendar is valid.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <returns><c>true</c> if the specified calendar is valid; otherwise, <c>false</c>.</returns>
    public static bool IsValidCalendar(Calendar calendar)
    {
      return Calendars.IsValidEntry(calendar.Id);
    }

    /// <summary>
    /// Determines whether the specified date is a valid settlement (business) date] []
    /// according to the specified calendar.
    /// </summary>
    /// <param name="cal">The calendar.</param>
    /// <param name="day">The day.</param>
    /// <param name="month">The month.</param>
    /// <param name="year">The year.</param>
    /// <returns><c>true</c> if the date is a valid settlement (business) date; otherwise, <c>false</c>.</returns>
    public static bool IsValidSettlement(Calendar cal,
      int day, int month, int year)
    {
      return IsValidSettlement(cal.Id, day, month, year);
    }
    #endregion

    #region Initialization

    private static Func<string, int[]> LoadCalendar;

    /// <summary/>
    public static void Init(Func<string, int[]> loadCalendar, Func<string[]> getAllCalendars, Func<string,string> getCalendarDescription)
    {
      LoadCalendar = loadCalendar;
      GetValidCalendars = getAllCalendars;
      GetCalendarDescription = getCalendarDescription;
    }

    internal static void InitializeCalendarCollection()
    {
      // Global lock
      lock (typeof(CalendarCalculator))
      {
        _calendars = CalendarCollection.Create();
      }
    }

    #endregion

    #region Data and property

    private static readonly log4net.ILog logger = 
      log4net.LogManager.GetLogger(typeof(CalendarCalculator));

    private static CalendarCollection _calendars;

    private static CalendarCollection Calendars
    {
      get
      {
        if (_calendars == null) InitializeCalendarCollection();
        return _calendars;
      }
    }
    #endregion

    #region Check business days

    private static bool IsValidSettlement(int id, int day, int month, int year)
    {
      var entry = Calendars.Lookup(id);

      if (entry.Status == Status.Invalid)
      {
        throw new ArgumentException(String.Format("Invalid calendar {0} ({1})", entry.Name, id));
      }

      // Check for weekend
      var dow = GetDayOfWeek(day, month, year);

      Dow[] myWeekends = entry.Weekends;
      if (myWeekends == null || myWeekends.Length == 0)
      {
        // Assume Sat/Sun as default
        if (dow == Dow.Saturday || dow == Dow.Sunday)
          return false;
      }
      else if (IsWeekend(myWeekends, dow))
      {
        return false;
      }

      // Check for holiday
      int[] myHols = entry.Holidays;
      if (myHols != null && myHols.Length > 0)
      {
        // Schedule exists for this calendar
        int tdate = ((year * 100 + month) * 100 + day);
        if (Array.BinarySearch(myHols, tdate) >= 0)
        {
          // In our list of holidays
          return false;
        }
      }

      int[] baseCals = entry.BaseCalendars;
      // if is composite
      if (baseCals != null && baseCals.Length > 0)
      {
        // iterate through base cals
        for (int i = 0; i < baseCals.Length; i++)
        {
          if (!IsValidSettlement(baseCals[i], day, month, year))
            return false;
        }
      }

      return true;
    }


    static Dow GetDayOfWeek(int day, int month, int year)
    {
      return new DateTime(year, month, day).DayOfWeek;
    }

    static bool IsWeekend(Dow[] myWeekends, Dow dow)
    {
      return myWeekends.Contains(dow);
    }
    #endregion

    #region Nested type: Status
    internal enum Status
    {
      Uninitialized,
      Valid,
      Invalid
    };
    #endregion

    #region Nested type: Entry
    internal class Entry
    {
      private const int BuiltInFlag = 0x10;
      private const int HasDataFlag = 0x20;
      private const int FlagMask = 0xF0;
      private const int StatusMask = 0x0F;
      private int _state;

      public int Id;
      public string Name;
      public int[] BaseCalendars;
      public int[] Holidays;
      public Dow[] Weekends;

      public Status Status
      {
        get { return (Status)(_state & StatusMask); }
        set { _state = (_state & FlagMask) | (((int)value) & StatusMask); }
      }

      public bool HasData
      {
        get { return (_state & HasDataFlag) != 0; }
        set
        {
          if (value) _state |= HasDataFlag;
          else _state &= ~HasDataFlag;
        }
      }

      public bool IsBuiltIn
      {
        get { return (_state & BuiltInFlag) != 0; }
        set
        {
          if (value) _state |= BuiltInFlag;
          else _state &= ~BuiltInFlag;
        }
      }
    }
    #endregion

    #region Nested type: CalendarCollection
    internal class CalendarCollection
    {
      #region Data
      // Used to parse composite calendar identifiers
      private const int MaxCalendars = 1024;
      private static readonly char[] Delimiters = new[] { ',', '+' };

      private int _nextId;
      private readonly Entry[] _entries;
      private readonly Dictionary<string, int> _map;
      #endregion

      #region Properties and query methods
      internal IEnumerable<Entry> Entries
      {
        get { return _entries.Where((e, i) => i < _nextId); }
      }

      internal bool IsValidEntry(int id)
      {
        Entry entry;
        if (id < 0 || id >= _nextId || (entry = _entries[id]) == null)
        {
          return false;
        }
        if (entry.Status != Status.Uninitialized)
        {
          return entry.Status == Status.Valid;
        }
        if (String.IsNullOrEmpty(entry.Name))
          return false;

        try
        {
          entry = Lookup(id);
        }
        catch (IOException)
        {
          // swallow it. 
        }
        return entry.Status == Status.Valid;
      }

      internal int GetEntryId(string name)
      {
        // copy and convert string to upper case
        name = name.ToUpper();

        int id;
        if (_map.TryGetValue(name, out id))
          return id;

        lock (this)
        {
          return GetCalendarEntry(name).Id;
        }
      }

      internal string GetEntryName(int id)
      {
        Entry entry;
        if (id < 0 || id >= _nextId || (entry = _entries[id]) == null)
        {
          throw new ArgumentException(String.Format("Invalid calendar id {0}", id));
        }
        return entry.Name;
      }
      #endregion

      #region Look up and load calendar entry

      internal Entry Lookup(int id)
      {
        Entry entry;
        if (id < 0 || id >= _nextId || (entry = _entries[id]) == null)
        {
          throw new ArgumentException(String.Format("Invalid calendar id {0}", id));
        }
        if (entry.Status != Status.Uninitialized)
        {
          return entry;
        }
        lock (entry)
        {
          // Double check.
          if (entry.Status == Status.Uninitialized)
            LoadCalendarEntry(entry);

          return entry;
        }
      }

      private void LoadCalendarEntry(Entry entry)
      {
        var myHols = new HashSet<int>();
        var myWeekends = new HashSet<Dow>();
        var baseCals = entry.BaseCalendars;
        if (baseCals != null)
        {
          if (!LoadCompositeCalendar(baseCals, myHols, myWeekends))
          {
            entry.Status = Status.Invalid;
            return;
          }
        }
        else
        {
          if (!entry.HasData)
          {
            // if no .dat file, then mark as Invalid
            entry.Status = Status.Invalid;
            return;
          }

          string name = entry.Name;
          if (String.IsNullOrEmpty(name))
          {
            throw new ArgumentException(String.Format("Attempting to load invalid calendar: {0}", entry.Id));// should never happen
          }

          if (!LoadBaseCalendar(name, myHols, myWeekends))
          {
            entry.Status = Status.Invalid;
            return;
          }
        }

        entry.Holidays = myHols.OrderBy(d => d).ToArray();
        entry.Weekends = myWeekends.Count != 0
          ? myWeekends.OrderBy(d => d).ToArray()
          : null;
        entry.Status = Status.Valid;
      }

      private static bool LoadBaseCalendar(string name, ISet<int> hols, ISet<Dow> weekends)
      {
        var dates = LoadCalendar(name);
        if (dates.Length > 0)
        {
          foreach (var date in dates)
          {
            if (date < 8)
            {
              weekends.Add((Dow)date);
            }
            else
            {
              if (!Dt.IsValid(date))
              {
                logger.ErrorFormat("Invalid date {0}", date);
                return false;
              }
              hols.Add(date);
            }
          }
        }
        return true;
      }

      private bool LoadCompositeCalendar(int[] baseCals, ISet<int> hols, ISet<Dow> weekends)
      {
        for (int i = 0; i < baseCals.Length; i++)
        {
          var entry = Lookup(baseCals[i]);

          // If we find an invalid base calendar,
          // then the composite calendar is invalid.
          if (entry.Status == Status.Invalid)
            return false;

          // Merge holidays and weekends
          if (entry.Holidays != null) hols.UnionWith(entry.Holidays);
          if (entry.Weekends != null) weekends.UnionWith(entry.Weekends);
        }
        return true;
      }
      #endregion

      #region Initialize calendar entries
      public static CalendarCollection Create()
      {
        var cc = new CalendarCollection();
        cc.CreateKnownCalendars();

        return cc;
      }

      private CalendarCollection()
      {
        _nextId = 0;
        _entries = new Entry[MaxCalendars];
        _map = new Dictionary<string, int>();
      }

      private void CreateKnownCalendars()
      {
        const BindingFlags bf = BindingFlags.Public |
          BindingFlags.Static | BindingFlags.DeclaredOnly;
        int max = 0;
        foreach (var fi in typeof(Calendar).GetFields(bf)
          .Where(f => f.FieldType == typeof(Calendar)))
        {
          var calendar = (Calendar)fi.GetValue(null);
          var id = calendar.Id;
          CreateCalendarEntry(id, fi.Name.ToUpper(), null);
          if (id >= max) max = id + 1;
        }
        _nextId = max;
      }

      Entry CreateCalendarEntry(int id, string name, int[] baseCals)
      {
        if (id >= MaxCalendars)
        {
          throw new InvalidOperationException(String.Format("Calendar limit {0} Exceeded", MaxCalendars));
        }
        if (id < 0)
        {
          throw new InvalidOperationException(String.Format("Calendar id is negative {0}", id));
        }
        var entry = _entries[id] = new Entry
        {
          Id = id,
          Name = name,
          Status = Status.Uninitialized,
          BaseCalendars = baseCals
        };
        _map.Add(name, id);
        return entry;
      }

      Entry GetCalendarEntry(string name)
      {
        Debug.Assert(name == name.ToUpper());

        int id;
        if (_map.TryGetValue(name, out id))
          return _entries[id];

        var pos = name.IndexOfAny(Delimiters);
        if (pos < 0)
        {
          // Simple calendar
          return AddCalendarEntry(name, null);
        }

        // Composite calendar
        int[] baseCals = ParseBaseCals(name);
        if (baseCals == null)
        {
          throw new ArgumentException(String.Format("Invalid composite calendar: {0}", name));
        }

        return AddCalendarEntry(name, baseCals);
      }

      Entry AddCalendarEntry(string name, int[] baseCals)
      {
        return CreateCalendarEntry(_nextId++, name, baseCals);
      }

      // Utility function to parse base calendars from a composite calendar string (e.g. NYB+TGT)
      // NOTE: Must be called within critical section!
      int[] ParseBaseCals(string name)
      {
        var baseNames = name.Split(Delimiters);
        IList<int> baseCals = new List<int>();
        for (int i = 0; i < baseNames.Length; ++i)
        {
          var baseCalName = baseNames[i];
          if (string.IsNullOrEmpty(baseCalName)) continue;
          var entry = GetCalendarEntry(baseCalName);
          baseCals.Add(entry.Id);
        }
        return baseCals.ToArray();
      }
      #endregion
    }
    #endregion
  }
}
