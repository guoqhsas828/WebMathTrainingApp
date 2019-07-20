using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BaseEntity.Shared
{
  /// <summary>
  /// A table having a set of rows and columns.
  /// </summary>
  /// <remarks>
  /// The table is represented by each row being a Hashtable where each column is a key in the hashtable. The table itself 
  /// then is an ArrayList of Hashtables. The column names must be unique so there are no hashtable collisions.
  /// </remarks>
  public class ListTable
  {
    private ArrayList columns_;
    private ArrayList rows_;

    /// <summary>
    /// Default Constructor.
    /// </summary>
    public ListTable()
    {
      columns_ = new ArrayList();
      rows_ = new ArrayList();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="columns"></param>
    public ListTable(params string[] columns) : this()
    {
      if (columns != null)
      {
        for (int i = 0; i < columns.Length; i++)
        {
          this.Columns.Add(columns[i]);
        }
      }
    }

    /// <summary>
    /// Adds a new row to the table with the specified values. The values must be in the order of the 
    /// columns and have a value (NULL is allowed) specified.
    /// </summary>
    /// <param name="values"></param>
    public void AddRow(params object[] values)
    {
      if (values != null)
      {
        Hashtable row = new Hashtable();
        for (int i = 0; i < values.Length; i++)
        {
          row.Add(this.Columns[i], values[i]);
        }
        this.Rows.Add(row);
      }
    }

    /// <summary>
    /// Creates a string representation of the Table in a .csv compatible format.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      string tbl = "";
      foreach (Hashtable row in this.Rows)
      {
        foreach (string col in this.Columns)
        {
          tbl += row[col].ToString() + ",";
        }
        //remove last comma
        tbl = tbl.Remove(tbl.Length - 1, 1) + System.Environment.NewLine;
      }
      return tbl;
    }

    /// <summary>
    /// Copies the Table into a DataTable object.
    /// </summary>
    /// <returns></returns>
    public DataTable ToDataTable()
    {
      DataTable tbl = new DataTable();
      DataRow dr = null;

      //setup columns
      foreach (string col in this.Columns)
      {
        tbl.Columns.Add(col);
      }

      //setup rows
      foreach (Hashtable row in this.Rows)
      {
        dr = tbl.NewRow();
        foreach (string col in this.Columns)
        {
          dr[col] = row[col];
        }
        //add to tbl
        tbl.Rows.Add(dr);
      }

      //done
      return tbl;
    }

    /// <summary>
    /// Gets the list of Rows in the table.
    /// </summary>
    public ArrayList Rows
    {
      get { return rows_; }
    }
	
    /// <summary>
    /// Gets the list of columns in the table.
    /// </summary>
    public ArrayList Columns
    {
      get { return columns_; }
    }	
  }
}
