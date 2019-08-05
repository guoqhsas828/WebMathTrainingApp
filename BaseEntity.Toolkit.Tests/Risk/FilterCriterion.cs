using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BaseEntity.Risk
{
  /// <summary>
  /// Represents a parsed filter string
  /// </summary>
  public class FilterCriterion : ICollection<string>, ICollection
  {
    #region Constructors

    /// <summary>
    /// Construct instance from specified input
    /// </summary>
    /// <param name="input"></param>
    public FilterCriterion(string input)
    {
      _input = input ?? "";
      _reader = new StringReader(_input);
      _values = new HashSet<string>();

      // Position the cursor on the first input character 
      ReadChar();
      
      SkipWhiteSpace();
      
      if (!_eof)
      {
        string value;
        while ((value = ParseComponent()) != null)
        {
          _values.Add(value);
        }
      }
    }

    #endregion

    /// <summary>
    /// Parse a single part of a (possibly) multi-part filter string
    /// </summary>
    /// <returns>The parsed value, or null if no more values or an error was encountered</returns>
    private string ParseComponent()
    {
      SkipWhiteSpace();

      if (_eof)
      {
        return null;
      }

      if (_currentChar == '+')
      {
        ReadChar();
      }

      return ParseValue();
    }

    private string ParseValue()
    {
      var sb = new StringBuilder();

      SkipWhiteSpace();

      bool escape = false;

      while (true)
      {
        if (_eof)
        {
          if (escape)
          {
            _errorMsg = "Unexpected eof while reading input [" + _input + "]";
            return null;
          }
          else
            break;
        }
        if (_currentChar == '\\' && !escape)
        {
          escape = true;
        }
        else if (escape)
        {
          switch (_currentChar)
          {
            case '+':
              sb.Append(_currentChar);
              break;
            case '\\':
              sb.Append('\\');
              break;
            default:
              _errorMsg = "Unknown escape combination [" + _currentChar + "]";
              return null;
          }
          escape = false;
        }
        else if (_currentChar == '+')
        {
          break;
        }
        else
        {
          sb.Append(_currentChar);
        }

        ReadChar();
      }

      return sb.ToString();
    }

    private void ReadChar()
    {
      int data = _reader.Read();

      _eof = data == -1;

      if (!_eof)
      {
        _currentChar = (char)data;
        if (_currentChar == '\n')
        {
          _offset = 1;
        }
        else
        {
          _offset++;
        }
      }
      else
      {
        _currentChar = '\0';
      }
    }

    /// <summary>
    /// Consume any whitespace
    /// </summary>
    /// <returns></returns>
    private void SkipWhiteSpace()
    {
      while (!_eof && char.IsWhiteSpace(_currentChar))
      {
        ReadChar();
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public HashSet<string> Values
    {
      get { return _values; }
    }

    /// <summary>
    /// 
    /// </summary>
    public bool HasValues
    {
      get { return IsValid && (Count != 0); }
    }

    /// <summary>
    /// 
    /// </summary>
    public bool IsValid
    {
      get { return _errorMsg == null; }
    }

    /// <summary>
    /// 
    /// </summary>
    public string ErrorMsg
    {
      get { return _errorMsg; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      if (IsValid)
      {
        List<string> values = _values.Select(SafeString).ToList();
        return String.Join("+", values.ToArray());
      }
      else
      {
        return _errorMsg;
      }
    }

    private static string SafeString(string value)
    {
      var sb = new StringBuilder();
      char[] charArray = value.ToCharArray();
      for (int i=0; i<charArray.Length;i++)
      {
        char c = charArray[i];
        if (c == '+' || c == '-' || c == '\\')
        {
          sb.Append('\\');
        }
        sb.Append(c);
      }
      return sb.ToString();
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="array"></param>
    /// <param name="index"></param>
    public void CopyTo(Array array, int index)
    {
      foreach (var value in _values)
      {
        array.SetValue(value, index);
        index++;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public int Count
    {
      get { return _values.Count; }
    }

    /// <summary>
    /// 
    /// </summary>
    public object SyncRoot
    {
      get { throw new NotImplementedException(); }
    }

    /// <summary>
    /// 
    /// </summary>
    public bool IsSynchronized
    {
      get { throw new NotImplementedException(); }
    }

    /// <summary>
    /// 
    /// </summary>
    public bool IsReadOnly
    {
      get { return false; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool Contains(string value)
    {
      return _values.Contains(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="array"></param>
    /// <param name="arrayIndex"></param>
    public void CopyTo(string[] array, int arrayIndex)
    {
      throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    void ICollection<string>.Add(string value)
    {
      _values.Add(value);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool Remove(string value)
    {
      return _values.Remove(value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="match"></param>
    /// <returns></returns>
    public int RemoveWhere(Predicate<string> match)
    {
      return _values.RemoveWhere(match);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IEnumerator<string> GetEnumerator()
    {
      return _values.GetEnumerator();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool Add(string item)
    {
      return _values.Add(item);
    }

    /// <summary>
    /// 
    /// </summary>
    public void Clear()
    {
      _values.Clear();
      _errorMsg = null;
    }

    #region Data

    private readonly TextReader _reader;
    private readonly string _input;
    private string _errorMsg;
    private readonly HashSet<string> _values;
    private char _currentChar;
    private int _offset;
    private bool _eof;

    #endregion
  }
}