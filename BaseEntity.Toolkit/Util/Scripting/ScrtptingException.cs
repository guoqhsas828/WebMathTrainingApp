/*
 *   2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Util.Scripting
{
  /// <summary>
  ///   The text position
  /// </summary>
  public interface ITextPosition
  {
    /// <summary>
    /// Gets the begin position.
    /// </summary>
    /// <remarks></remarks>
    int BeginPosition { get; }
    /// <summary>
    /// Gets the end position.
    /// </summary>
    /// <remarks></remarks>
    int EndPosition { get; }
  }

  internal class TextPosition : ITextPosition
  {
    public int BeginPosition { get; private set; }
    public int EndPosition { get; private set; }
    public TextPosition(int begin, int end)
    {
      BeginPosition = begin;
      EndPosition = end;
    }
  }

  /// <summary>
  ///   The exception representing an error in script.
  /// </summary>
  /// <remarks></remarks>
  public class ScriptingException : ToolkitException
  {
    /// <summary>
    /// Gets the text position in script where the error occurs.
    /// </summary>
    /// <remarks></remarks>
    public ITextPosition Position { get; private set; }

    internal ScriptingException(string msg)
      : this(msg, null)
    { }

    internal ScriptingException(string msg, int begin, int end)
      : this(msg, new TextPosition(begin, end))
    { }

    internal ScriptingException(string msg, ITextPosition pos)
      : base(msg)
    {
      Position = pos;
    }
  }
}
