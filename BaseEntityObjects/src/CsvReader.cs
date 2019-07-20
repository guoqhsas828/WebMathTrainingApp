/*
 * CsvReader.cs - Utility class for reading/parsing CSV files
 *
 * This class is part of JHLib (a free .NET tool library by Jouni Heikniemi). Refer to:
 *
 *    http://www.heikniemi.net/jhlib
 *
 * for more details on JHLib.
 *
 */

using System;
using System.Data;
using System.IO;
using System.Collections;
using System.Text;

namespace BaseEntity.Shared
{

  /// <summary>
  /// A data-reader style interface for reading Csv (and otherwise-char-separated) files.
  /// </summary>
  public class CsvReader : IDisposable
  {

    #region Private variables

    private Stream stream;
    private StreamReader reader;
    private char separator;

    #endregion

    #region Static Methods

    /// <summary>
    ///  Parse CSV file
    /// </summary>
    /// <param name="path">Full path of CSV file</param>
    /// <param name="separator"></param>
    public static ArrayList ParseFile(string path, char separator = ',')
    {
      using (CsvReader reader = new CsvReader(path, separator))
      {
        return ParseFile(reader);
      }
    }

    /// <summary>
    ///  Parse CSV stream
    /// </summary>
    /// <param name="stream">CSV stream</param>
    public static ArrayList ParseStream(Stream stream)
    {
      using (CsvReader reader = new CsvReader(stream))
      {
        return ParseFile(reader);
      }
    }

    /// <summary>
    /// Use to parse a file using custom delimiter and therefore a particular constructor of CsvReader
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public static ArrayList ParseFile(CsvReader reader)
    {
      ArrayList rows = new ArrayList();

      string[] fields;
      string[] cols = null;
      int lineCount = 0;
      while ((fields = reader.GetCsvLine()) != null)
      {
        if (fields.Length == 0) continue;

        if (++lineCount == 1)
        {
          // Set up index based on column headers
          cols = new string[fields.Length];
          for (int i = 0; i < fields.Length; i++)
          {
            cols[i] = fields[i].Trim();
          }
        }
        else
        {
          Hashtable row = new Hashtable();
          for (int i = 0; i < fields.Length; i++)
          {
            if (i < cols.Length)
              row[cols[i]] = fields[i].Trim();
          }
          rows.Add(row);
        }
      }

      return rows;
    }

    /// <summary>
    /// </summary>
    public static ArrayList ParseHeader(string path)
    {
      using (CsvReader reader = new CsvReader(path))
      {
        return ParseHeader(reader);
      }
    }

    /// <summary>
    /// </summary>
    public static ArrayList ParseHeader(string path, char separator)
    {
      using (CsvReader reader = new CsvReader(path, null, separator))
      {
        return ParseHeader(reader);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    public static ArrayList ParseHeader(CsvReader reader)
    {
      string[] fields;
      while ((fields = reader.GetCsvLine()) != null)
      {
        if (fields.Length == 0) continue;
        ArrayList cols = new ArrayList();
        for (int i = 0; i < fields.Length; i++)
          cols.Add(fields[i].Trim());
        return cols;
      }

      return null;
    }

    /// <summary>
    ///   Parses the CSV file, reads the data into a data table and returns the data table.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="separator"></param>
    /// <returns></returns>
    public static DataTable TableFromFile(string path, char separator = ',')
    {
      DataTable table = new DataTable();
      foreach (string columnName in ParseHeader(path, separator))
      {
        if (!String.IsNullOrEmpty(columnName))
          table.Columns.Add(columnName, typeof(string));
      }

      foreach (Hashtable line in ParseFile(path, separator))
      {
        DataRow row = table.NewRow();
        foreach (string key in line.Keys)
        {
          if (row.Table.Columns.Contains(key))
            row[key] = line[key];
        }
        table.Rows.Add(row);
      }

      return table;
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new Csv reader for the given stream.
    /// </summary>
    /// <param name="s">The stream to read the CSV from.</param>
    public CsvReader(Stream s) : this(s, null, ',') { }

    /// <summary>
    /// Creates a new reader for the given stream and separator.
    /// </summary>
    /// <param name="s">The stream to read the separator from.</param>
    /// <param name="separator">The field separator character</param>
    public CsvReader(Stream s, char separator) : this(s, null, separator) { }

    /// <summary>
    /// Creates a new Csv reader for the given stream and encoding.
    /// </summary>
    /// <param name="s">The stream to read the CSV from.</param>
    /// <param name="enc">The encoding used.</param>
    public CsvReader(Stream s, Encoding enc) : this(s, enc, ',') { }

    /// <summary>
    /// Creates a new reader for the given stream, encoding and separator character.
    /// </summary>
    /// <param name="s">The stream to read the data from.</param>
    /// <param name="enc">The encoding used.</param>
    /// <param name="separator">The separator character between the fields</param>
    public CsvReader(Stream s, Encoding enc, char separator)
    {

      this.separator = separator;
      this.stream = s;
      if (!s.CanRead)
      {
        throw new CsvReaderException("Could not read the given data stream!");
      }
      reader = (enc != null) ? new StreamReader(s, enc) : new StreamReader(s);
    }

    /// <summary>
    /// Creates a new Csv reader for the given text file path.
    /// </summary>
    /// <param name="filename">The name of the file to be read.</param>
    public CsvReader(string filename) : this(filename, null, ',') { }

    /// <summary>
    /// Creates a new reader for the given text file path and separator character.
    /// </summary>
    /// <param name="filename">The name of the file to be read.</param>
    /// <param name="separator">The field separator character</param>
    public CsvReader(string filename, char separator) : this(filename, null, separator) { }

    /// <summary>
    /// Creates a new Csv reader for the given text file path and encoding.
    /// </summary>
    /// <param name="filename">The name of the file to be read.</param>
    /// <param name="enc">The encoding used.</param>
    public CsvReader(string filename, Encoding enc)
      : this(filename, enc, ',')
    { }

    /// <summary>
    /// Creates a new reader for the given text file path, encoding and field separator.
    /// </summary>
    /// <param name="filename">The name of the file to be read.</param>
    /// <param name="enc">The encoding used.</param>
    /// <param name="separator">The field separator character.</param>
    public CsvReader(string filename, Encoding enc, char separator)
      : this(new FileStream(filename, FileMode.Open), enc, separator)
    { }

    #endregion

    #region Properties

    /// <summary>
    /// The separator character for the fields. Comma for normal CSV.
    /// </summary>
    public char Separator
    {
      get { return separator; }
      set { separator = value; }
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Returns the fields for the next row of data (or null if at eof)
    /// </summary>
    /// <returns>A string array of fields or null if at the end of file.</returns>
    public string[] GetCsvLine()
    {

      string data = reader.ReadLine();
      if (data == null) return null;
      if (data.Length == 0) return new string[0];

      ArrayList result = new ArrayList();

      ParseCsvFields(result, data);

      return (string[])result.ToArray(typeof(string));
    }

    // Parses the fields and pushes the fields into the result arraylist
    private void ParseCsvFields(ArrayList result, string data)
    {

      int pos = -1;
      while (pos < data.Length)
        result.Add(ParseCsvField(data, ref pos));
    }

    // Parses the field at the given position of the data, modified pos to match
    // the first unparsed position and returns the parsed field
    private string ParseCsvField(string data, ref int startSeparatorPosition)
    {

      if (startSeparatorPosition == data.Length - 1)
      {
        startSeparatorPosition++;
        // The last field is empty
        return "";
      }

      int fromPos = startSeparatorPosition + 1;

      // Determine if this is a quoted field
      if (data[fromPos] == '"')
      {
        // If we're at the end of the string, let's consider this a field that
        // only contains the quote
        if (fromPos == data.Length - 1)
        {
          fromPos++;
          return "\"";
        }

        // Otherwise, return a string of appropriate length with double quotes collapsed
        // Note that FSQ returns data.Length if no single quote was found
        int nextSingleQuote = FindSingleQuote(data, fromPos + 1);
        startSeparatorPosition = nextSingleQuote + 1;
        return data.Substring(fromPos + 1, nextSingleQuote - fromPos - 1).Replace("\"\"", "\"");
      }

      // The field ends in the next separator or EOL
      int nextSeparator = data.IndexOf(separator, fromPos);
      if (nextSeparator == -1)
      {
        startSeparatorPosition = data.Length;
        return data.Substring(fromPos);
      }
      else
      {
        startSeparatorPosition = nextSeparator;
        return data.Substring(fromPos, nextSeparator - fromPos);
      }
    }

    // Returns the index of the next single quote mark in the string 
    // (starting from startFrom)
    private static int FindSingleQuote(string data, int startFrom)
    {

      int i = startFrom - 1;
      while (++i < data.Length)
        if (data[i] == '"')
        {
          // If this is a double quote, bypass the chars
          if (i < data.Length - 1 && data[i + 1] == '"')
          {
            i++;
            continue;
          }
          else
            return i;
        }
      // If no quote found, return the end value of i (data.Length)
      return i;
    }

    #endregion


    /// <summary>
    /// Disposes the reader. The underlying stream is closed.
    /// </summary>
    public void Dispose()
    {
      // Closing the reader closes the underlying stream, too
      if (reader != null) reader.Close();
      else if (stream != null)
        stream.Close(); // In case we failed before the reader was constructed
      GC.SuppressFinalize(this);
    }
  }


  /// <summary>
  /// Exception class for CsvReader exceptions.
  /// </summary>
  [Serializable]
  public class CsvReaderException : Exception
  {

    /// <summary>
    /// Constructs a new CsvReaderException.
    /// </summary>
    public CsvReaderException() : this("The CSV Reader encountered an error.") { }

    /// <summary>
    /// Constructs a new exception with the given message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public CsvReaderException(string message) : base(message) { }

    /// <summary>
    /// Constructs a new exception with the given message and the inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="inner">Inner exception that caused this issue.</param>
    public CsvReaderException(string message, Exception inner) : base(message, inner) { }

    /// <summary>
    /// Constructs a new exception with the given serialization information.
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    protected CsvReaderException(System.Runtime.Serialization.SerializationInfo info,
                                 System.Runtime.Serialization.StreamingContext context)
      : base(info, context)
    { }

  }

}
