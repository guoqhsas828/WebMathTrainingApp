// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using Microsoft.Practices.Unity;
#if NETSTANDARD2_0 || NETSTANDARD2_1
using Unity;
#endif

namespace BaseEntity.Configuration
{
  /// <summary>
  /// 
  /// </summary>
  public interface IUnityContainerFactory
  {
    /// <summary>
    /// Resolves the specified container name.
    /// </summary>
    /// <param name="containerName">Name of the container.</param>
    /// <returns></returns>
    IUnityContainer Resolve(string containerName = null);

    /// <summary>
    /// Disposes the container.
    /// </summary>
    /// <param name="containerName">Name of the container.</param>
    void DisposeContainer(string containerName);
  }
}