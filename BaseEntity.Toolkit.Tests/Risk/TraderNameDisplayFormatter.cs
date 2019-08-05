/*
 * TraderNameDisplayFormatter.cs
 *
 *
 */
using System;
using System.Collections;
using System.Xml;
using BaseEntity.Configuration;
using BaseEntity.Metadata;

namespace BaseEntity.Risk
{

 	/// <summary>
	/// Provides a list of LegalEntities based on Roles
	/// This is used for GUI screen dropdown lists
	///</summary>
	public static class TraderNameDisplayFormatter
	{
 	  /// <summary>
 	  ///   The format to display trader name in
 	  /// </summary>
 	  public enum NameFormat
 	  {
 	    /// <summary>
 	    ///   loginname
 	    /// </summary>
 	    LoginName,

 	    /// <summary>
 	    ///   FirstName LastName
 	    /// </summary>
 	    FirstLast,

 	    /// <summary>
 	    ///   LastName, FirstName
 	    /// </summary>
 	    LastFirst
 	  }

 	  /// <summary>
    /// 
    /// </summary>
 	  public static readonly NameFormat CurrentFormat; 
 
    /// <summary>
    /// Initialize the trader display name format from config file.
    /// </summary>
    /// <remarks>Defaults to Loginname format on missing/error</remarks>
    static TraderNameDisplayFormatter()
    {
      XmlElement configXml = Configurator.GetConfigXml("TraderName",null);
      if (configXml == null)
      {
        CurrentFormat = NameFormat.LoginName;
      }
      else
      {
        string val = configXml.GetAttribute("DisplayFormat");
        if (val.StartsWith("First", StringComparison.InvariantCultureIgnoreCase))
        {
          CurrentFormat = NameFormat.FirstLast;
        }
        else if (val.StartsWith("Last", StringComparison.InvariantCultureIgnoreCase))
        {
          CurrentFormat = NameFormat.LastFirst;
        }
        else
        {
          CurrentFormat = NameFormat.LoginName;
        }
      }
    }

    /// <summary>
    /// Format the trader display name in the currently configured format
    /// </summary>
    /// <param name="user">the User object of the trader</param>
    /// <returns>a formatted display name</returns>
    public static string FormatName(User user)
    {
      if (user == null) throw new ArgumentNullException("user");

      return FormatName(user.Name, user.FirstName, user.LastName);
    }

    /// <summary>
    /// Format the trader display name in the currently configured format
    /// </summary>
    /// <param name="name">Login name</param>
    /// <param name="firstName">First name</param>
    /// <param name="lastName">Last name</param>
    /// <returns>a formatted display name</returns>
    public static string FormatName(string name, string firstName, string lastName)
    {
      // Always default to loginname if other data is missing
      if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        return name;

      switch (CurrentFormat)
      {
        case NameFormat.FirstLast:
          return String.Format("{0} {1}", firstName, lastName);
        case NameFormat.LastFirst:
          return String.Format("{0}, {1}", lastName, firstName);
        default:
          return name;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public class TraderNameComparer : IComparer
		{
			#region IComparer Members

      /// <summary>
      /// Compares two objects and returns a value indicating whether one is less than, equal to, or greater than the other.
      /// </summary>
      /// <param name="x">The first object to compare.</param>
      /// <param name="y">The second object to compare.</param>
      /// <returns>
      /// Value
      /// Condition
      /// Less than zero
      /// <paramref name="x"/> is less than <paramref name="y"/>.
      /// Zero
      /// <paramref name="x"/> equals <paramref name="y"/>.
      /// Greater than zero
      /// <paramref name="x"/> is greater than <paramref name="y"/>.
      /// </returns>
      /// <exception cref="T:System.ArgumentException">
      /// Neither <paramref name="x"/> nor <paramref name="y"/> implements the <see cref="T:System.IComparable"/> interface.
      /// -or-
      /// <paramref name="x"/> and <paramref name="y"/> are of different types and neither one can handle comparisons with the other.
      /// </exception>
			public int Compare(object x, object y)
			{
        User left = x as User;
        User right = y as User;

			  return FormatName(left).CompareTo(FormatName(right)); 
      }
			#endregion
		}
	}
}
