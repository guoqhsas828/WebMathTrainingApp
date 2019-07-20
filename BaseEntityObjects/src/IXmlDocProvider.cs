// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System.Reflection;
using System.Xml;

namespace BaseEntity.Shared
{
  /// <summary>
  /// An interface for a component that can provide Xml Documentation info for members of assemblies
  /// </summary>
  public interface IXmlDocProvider
  {
    /// <summary>
    /// Gets the summary documentation for a member.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns></returns>
    XmlText GetSummary(MemberInfo member);
  }
}