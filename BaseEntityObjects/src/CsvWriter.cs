/*
 * CsvWriter.cs - Utility class for writer CSV files
 *
 * This class is part of JHLib (a free .NET tool library by Jouni Heikniemi). Refer to:
 *
 *    http://www.heikniemi.net/jhlib
 *
 * for more details on JHLib.
 *
 */

using System;
using System.Collections;
using System.Data;
using System.IO;
using System.Text;

namespace BaseEntity.Shared 
{

  /// <summary>
  /// A tool class for writing Csv and other char-separated field files.
  /// </summary>
  public class CsvWriter : StreamWriter 
	{

    #region Data

    private char separator;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new Csv writer for the given filename (overwriting existing contents).
    /// </summary>
    /// <param name="filename">The name of the file being written to.</param>
    public CsvWriter(string filename) 
      : this(filename, ',', false) { }

    /// <summary>
    /// Creates a new Csv writer for the given filename.
    /// </summary>
    /// <param name="filename">The name of the file being written to.</param>
    /// <param name="append">True if the contents shall be appended to the
    /// end of the possibly existing file.</param>
    public CsvWriter(string filename, bool append) 
      : this(filename, ',', append) { }

    /// <summary>
    /// Creates a new Csv writer for the given filename and encoding.
    /// </summary>
    /// <param name="filename">The name of the file being written to.</param>
    /// <param name="enc">The encoding used.</param>
    /// <param name="append">True if the contents shall be appended to the
    /// end of the possibly existing file.</param>
    public CsvWriter(string filename, Encoding enc, bool append) 
      : this(filename, enc, ',', append) { }

    /// <summary>
    /// Creates a new writer for the given filename and separator.
    /// </summary>
    /// <param name="filename">The name of the file being written to.</param>
    /// <param name="separator">The field separator character used.</param>
    /// <param name="append">True if the contents shall be appended to the
    /// end of the possibly existing file.</param>
    public CsvWriter(string filename, char separator, bool append) 
      : base(filename, append) { this.separator = separator; }

    /// <summary>
    /// Creates a new writer for the given filename, separator and encoding.
    /// </summary>
    /// <param name="filename">The name of the file being written to.</param>
    /// <param name="enc">The encoding used.</param>
    /// <param name="separator">The field separator character used.</param>
    /// <param name="append">True if the contents shall be appended to the
    /// end of the possibly existing file.</param>
    public CsvWriter(string filename, Encoding enc, char separator, bool append) 
      : base(filename, append, enc) { this.separator = separator; }

    /// <summary>
    /// Creates a new Csv writer for the given stream.
    /// </summary>
    /// <param name="s">The stream to write the CSV to.</param>
    public CsvWriter(Stream s) 
      : this(s, ',') { }

    /// <summary>
    /// Creates a new writer for the given stream and separator character.
    /// </summary>
    /// <param name="s">The stream to write the CSV to.</param>
    /// <param name="separator">The field separator character used.</param>
    public CsvWriter(Stream s, char separator) 
      : base(s) { this.separator = separator; }

    /// <summary>
    /// Creates a new writer for the given stream, separator and encoding.
    /// </summary>
    /// <param name="s">The stream to write the CSV to.</param>
    /// <param name="enc">The encoding used.</param>
    /// <param name="separator">The field separator character used.</param>
    public CsvWriter(Stream s, Encoding enc, char separator) 
      : base(s, enc) { this.separator = separator; }

    #endregion

    #region Properties

    /// <summary>
    /// The separator character for the fields. Comma for normal CSV.
    /// </summary>
    public char Separator {
      get { return separator; }
      set { separator = value; }
    }

    #endregion


		/// <summary>
    /// Write a table to a csv file with specified file name
		/// </summary>
		/// <param name="fileName">Output file</param>
		/// <param name="cols">List of column headers</param>
		/// <param name="rows">List of rows (each row is represented as a hashtable)</param>
		public static void WriteFile(string fileName, ArrayList cols, ArrayList rows)
		{
			using (CsvWriter writer = new CsvWriter(fileName) )
			{
				writer.WriteFields(cols);
				foreach (Hashtable row in rows)
				{
					ArrayList values = new ArrayList(row.Count);
					for (int i=0; i<cols.Count; i++)
						values.Add( row[ cols[i] ] );
					writer.WriteFields( values );
				}
			}
		}

    /// <summary>
    /// Write DataTable as csv file
    /// </summary>
    /// <param name="fileName">output file name</param>
    /// <param name="dt">DataTable</param>
    public static void WriteDataTableToFile(string fileName,DataTable dt)
    {
      if (dt==null || dt.Columns.Count==0)
        return;

      using (var writer = new CsvWriter(fileName))
      {
        //write column names
        var cols = new ArrayList(dt.Columns.Count);
        foreach (DataColumn col in dt.Columns)
          cols.Add(col.ColumnName);
        writer.WriteFields(cols);

        //write row data
        foreach (DataRow row in dt.Rows)
        {
          var values = new ArrayList(dt.Columns.Count);
          foreach(DataColumn col in dt.Columns)
          {
            var dtVal = row[col];
            var val = dtVal == DBNull.Value ? null : dtVal;
            values.Add(val);
          }
          writer.WriteFields(values);
        }
      }      
    }

		/// <summary>
		/// </summary>
		/// <param name="content"></param>
		public void WriteFields(ArrayList content)
		{
			string s;
			for (int i = 0; i < content.Count; ++i) {
				s = (content[i] != null ? content[i].ToString() : "");
				if (s.IndexOfAny(new char[] { Separator, '"' }) >= 0)
					// We have to quote the string
					s = "\"" + s.Replace("\"", "\"\"") + "\"";
				Write(s);

				// Write the separator unless we're at the last position
				if (i < content.Count-1)
					Write(separator);
			}
			Write(NewLine);
		}

		/// <summary>
		/// </summary>
		/// <param name="content"></param>
    public void WriteFields(params object[] content) 
		{
			ArrayList list = new ArrayList(content.Length);
			for (int i=0; i<content.Length; i++)
			{
				list.Add( content[i] );
			}

			WriteFields(list);
		}

  }

}