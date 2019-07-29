using System;
using System.Text.RegularExpressions;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///  Specify strike value and option type.
  ///  Mainly used for user inputs.
  /// </summary>
  [Serializable]
  public struct StrikeSpec : IComparable<StrikeSpec>
  {
    /// <summary>
    /// The empty spec.
    /// </summary>
    public static readonly StrikeSpec Empty
      = new StrikeSpec(Double.NaN, OptionType.None);

    /// <summary>
    /// Gets a value indicating whether this instance is empty.
    /// </summary>
    /// <value><c>true</c> if this instance is empty; otherwise, <c>false</c>.</value>
    public bool IsEmpty
    {
      get { return Type == OptionType.None; }
    }

    /// <summary>
    /// Gets the option style.
    /// </summary>
    /// <value>The style.</value>
    public OptionStyle Style { get { return OptionStyle.European; } }

    /// <summary>
    /// Gets the option type.
    /// </summary>
    /// <value>The type.</value>
    public OptionType Type { get; }

    /// <summary>
    /// Gets the strike.
    /// </summary>
    /// <value>The strike.</value>
    public double Strike { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StrikeSpec"/> struct.
    /// </summary>
    /// <param name="strike">The strike.</param>
    /// <param name="type">The type.</param>
    private StrikeSpec(double strike, OptionType type)
    {
      Strike = strike;
      Type = type;
    }

    /// <summary>
    /// Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
    public override string ToString()
    {
      return Strike.ToString("R") + ' ' + Type.ToString();
    }

    /// <summary>
    /// Creates the specified strike specification.
    /// </summary>
    /// <param name="strike">The strike.</param>
    /// <param name="type">The type.</param>
    /// <returns>StrikeSpec.</returns>
    public static StrikeSpec Create(double strike, OptionType type)
    {
      return new StrikeSpec(strike, type);
    }

    /// <summary>
    /// Parses the specified string into an instance of the <see cref="DeltaSpec" /> struct.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>An instance of the <see cref="StrikeSpec" /> struct</returns>
    /// <exception cref="ToolkitException"></exception>
    /// <remarks>The input must be the format "NUMBER [C|CALL|P|PUT].  Examples are
    /// "1.2C" or "1.2 Call" for call option, "110P" or "110 Put" for put option.
    /// An empty string indicate no input and will be ignored.</remarks>
    public static StrikeSpec Parse(string input)
    {
      if (input != null) input = input.Trim();
      if (String.IsNullOrEmpty(input))
      {
        return Empty;
      }
      var match = RegexInput.Match(input);
      if (!match.Success)
      {
        throw new ToolkitException(String.Format(
          "Not a legitimate strike specification: {0}", input));
      }
      var type = String.IsNullOrEmpty(match.Groups[3].Value)
        ? OptionType.Call
        : OptionType.Put;
      var strike = Double.Parse(match.Groups[1].Value);
      return new StrikeSpec(strike, type);
    }

    internal static StrikeSpec Parse(object input)
    {
      if (input is StrikeSpec)
        return (StrikeSpec)input;
      return input == null ? Empty : Parse(input.ToString());
    }

    /// <summary>
    /// The _regex input
    /// </summary>
    private static readonly Regex RegexInput = new Regex(
      @"^(\d+(?:\.\d*)?)\s*(?:(Call|c)|(Put|p))?$",
      RegexOptions.IgnoreCase | RegexOptions.Compiled);

    #region IComparable<StrikeSpec> Members

    /// <summary>
    /// Compares the current object with another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>A 32-bit signed integer that indicates the relative order of the objects being compared. The return value has the following meanings: Value Meaning Less than zero This object is less than the <paramref name="other" /> parameter.Zero This object is equal to <paramref name="other" />. Greater than zero This object is greater than <paramref name="other" />.</returns>
    public int CompareTo(StrikeSpec other)
    {
      if (Double.IsNaN(Strike))
      {
        return Double.IsNaN(other.Strike) ? 0 : -1;
      }
      if (Strike.AlmostEquals(other.Strike))
      {
        return (int)other.Type - (int)Type;
      }
      return Strike < other.Strike ? -1 : 1;
    }

    #endregion
  }
}
