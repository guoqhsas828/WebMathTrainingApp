using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace BaseEntity.Database.Engine
{
  /// <summary>
  /// 
  /// </summary>
  public class VarBinarySource : IDisposable
  {
    private long? _length;
    private readonly SqlCommand _insertCommand;
    private readonly SqlCommand _readCommand;
    private readonly SqlCommand _writeCommand;

    /// <summary>
    /// Initializes a new instance of the <see cref="VarBinarySource"/> class.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="table">The table.</param>
    /// <param name="dataColumn">The data column.</param>
    /// <param name="key">The key.</param>
    public VarBinarySource(SqlConnection connection, 
                           string table, 
                           string dataColumn, 
                           Dictionary<string, object> key)
    {
      _length = GetLength(connection, table, dataColumn, key);
      _insertCommand = CreateInsertCommand(connection, table, dataColumn, key);
      _readCommand = CreateReadCommand(connection, table, dataColumn, key);
      _writeCommand = CreateWriteCommand(connection, table, dataColumn, key);
    }

    /// <summary>
    /// Gets the length.
    /// </summary>
    /// <value>The length.</value>
    public long? Length
    {
      get { return _length; }
    }

    #region IDisposable Members

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
      if (_readCommand != null)
      {
        _readCommand.Dispose();
      }
      if (_writeCommand != null)
      {
        _writeCommand.Dispose();
      }
    }

    #endregion

    private static SqlCommand CreateReadCommand(SqlConnection connection,
                                                string table, 
                                                string dataColumn, 
                                                Dictionary<string, object> key)
    {
      var readCommand = connection.CreateCommand();
      var sb = new StringBuilder();
      sb.AppendFormat(@"select substring({0}, @offset, @length) from {1}", dataColumn, table);

      int k = 0;
      foreach (var col in key.Keys)
      {
        sb.Append(k == 0 ? " where " : " and ");
        sb.AppendFormat("{0} = @key{1}", col, k);

        readCommand.Parameters.Add(String.Format("@key{0}", k), ToSqlDbType(key[col])).Value = key[col];

        k++;
      }

      readCommand.CommandText = sb.ToString();

      readCommand.CommandTimeout = SessionFactory.CommandTimeout;

      readCommand.Parameters.Add("@offset", SqlDbType.BigInt);
      readCommand.Parameters.Add("@length", SqlDbType.BigInt);
      return readCommand;
    }

    private static SqlCommand CreateWriteCommand(SqlConnection connection,
                                                 string table, 
                                                 string dataColumn, 
                                                 Dictionary<string, object> key)
    {
      var writeCommand = connection.CreateCommand();
      var sb = new StringBuilder();
      sb.AppendFormat(@"update {0} set {1}.write(@buffer, @offset, @length)", table, dataColumn);

      int k = 0;
      foreach (var col in key.Keys)
      {
        sb.Append(k == 0 ? " where " : " and ");
        sb.AppendFormat("{0} = @key{1}", col, k);

        writeCommand.Parameters.Add(String.Format("@key{0}", k), ToSqlDbType(key[col])).Value = key[col];

        k++;
      }

      writeCommand.CommandText = sb.ToString();

      writeCommand.CommandTimeout = SessionFactory.CommandTimeout;

      writeCommand.Parameters.Add("@offset", SqlDbType.BigInt);
      writeCommand.Parameters.Add("@length", SqlDbType.BigInt);
      writeCommand.Parameters.Add("@buffer", SqlDbType.VarBinary);
      return writeCommand;
    }

    private static SqlCommand CreateInsertCommand(SqlConnection connection,
                                                  string table,
                                                  string dataColumn,
                                                  Dictionary<string, object> key)
    {
      var insertCommand = connection.CreateCommand();
      var sb = new StringBuilder();
      sb.AppendFormat(@"insert into {0} ({1}", table, dataColumn);

      int k = 0;
      foreach (var col in key.Keys)
      {
        sb.AppendFormat(",{0}", col);

        k++;
      }

      sb.Append(") values (0x00");

      k = 0;
      foreach (var col in key.Keys)
      {
        sb.AppendFormat(",@key{0}", k);

        insertCommand.Parameters.Add(String.Format("@key{0}", k), ToSqlDbType(key[col])).Value = key[col];

        k++;
      }

      sb.Append(")");

      insertCommand.CommandText = sb.ToString();

      insertCommand.CommandTimeout = SessionFactory.CommandTimeout;

      return insertCommand;
    }

    private static long? GetLength(SqlConnection connection, 
                                   string table,
                                   string dataColumn, 
                                   Dictionary<string, object> key)
    {
      using (var command = connection.CreateCommand())
      {
        var length = command.Parameters.Add("@length", SqlDbType.BigInt);
        length.Direction = ParameterDirection.Output;

        var sb = new StringBuilder();
        sb.AppendFormat(@"select @length = cast(datalength({0}) as bigint) from {1}", dataColumn, table);

        int k = 0;
        foreach (var col in key.Keys)
        {
          sb.Append(k == 0 ? " where " : " and ");
          sb.AppendFormat("{0} = @key{1}", col, k);

          command.Parameters.Add(String.Format("@key{0}", k), ToSqlDbType(key[col])).Value = key[col];

          k++;
        }

        command.CommandText = sb.ToString();

        command.ExecuteNonQuery();
        return length.Value == DBNull.Value ? null : (long?) length.Value;
      }
    }

    /// <summary>
    /// Reads the specified offset.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public byte[] Read(long offset, long length)
    {
      // substring is 1-based.
      _readCommand.Parameters["@offset"].Value = offset + 1;
      _readCommand.Parameters["@length"].Value = length;
      return (byte[])_readCommand.ExecuteScalar();
    }

    /// <summary>
    /// Writes the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    public void Write(byte[] buffer, long offset, long length)
    {
      if (_length == null)
      {
        _insertCommand.ExecuteNonQuery();
        _length = 0;
      }

      _writeCommand.Parameters["@buffer"].Value = buffer;
      _writeCommand.Parameters["@offset"].Value = offset;
      _writeCommand.Parameters["@length"].Value = length;
      _writeCommand.ExecuteNonQuery();

      _length += length;
    }

    private static SqlDbType ToSqlDbType(object @object)
    {
      if (@object is Int32)
      {
        return SqlDbType.Int;
      }
      if (@object is Int64)
      {
        return SqlDbType.BigInt;
      }
      if (@object is String)
      {
        return SqlDbType.VarChar;
      }
      if (@object is Boolean)
      {
        return SqlDbType.Bit;
      }
      if (@object is Double || @object is Single)
      {
        return SqlDbType.Float;
      }
			if (@object is Guid)
			{
				return SqlDbType.UniqueIdentifier;
			}

      throw new NotSupportedException(String.Format("No SqlDbType for {0}", @object.GetType()));
    }
  }
}