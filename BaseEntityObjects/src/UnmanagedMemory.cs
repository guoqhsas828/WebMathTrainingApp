using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using log4net;

namespace BaseEntity.Shared
{
  /// <summary>
  /// Utility class for allocating and releasing unmanaged memory blocks
  /// </summary>
  public static class UnmanagedMemory
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(UnmanagedMemory));
    static UnmanagedMemory()
    {
      var dynamicMethod = new DynamicMethod("Memset", MethodAttributes.Public | MethodAttributes.Static,
        CallingConventions.Standard,
        null, new[] { typeof(IntPtr), typeof(byte), typeof(int) }, typeof(UnmanagedMemory), true);

      ILGenerator generator = dynamicMethod.GetILGenerator();
      generator.Emit(OpCodes.Ldarg_0);
      generator.Emit(OpCodes.Ldarg_1);
      generator.Emit(OpCodes.Ldarg_2);
      generator.Emit(OpCodes.Initblk);
      generator.Emit(OpCodes.Ret);

      MemSetAction = (Action<IntPtr, byte, int>)dynamicMethod.CreateDelegate(typeof(Action<IntPtr, byte, int>));
    }

    /// <summary>
    /// Allocate a block of unmanaged memory
    /// </summary>
    /// <param name="blockSize">size in bytes</param>
    public static IntPtr Allocate(int blockSize)
    {
      var pathData = Marshal.AllocCoTaskMem(blockSize);
      if (pathData == IntPtr.Zero)
      {
        Logger.ErrorFormat("Error in memory allocation of blocksize {0}", blockSize);
        throw new OutOfMemoryException("Memory Allocation Failed.");
      }
      Logger.DebugFormat("Zeroing new memory. blocksize {0}", blockSize);
      ClearMemory(pathData, blockSize);
      return pathData;
    }

    /// <summary>
    /// Release a block of unmanaged memory
    /// </summary>
    /// <param name="intPtr"></param>
    public static void Free(IntPtr intPtr)
    {
      if (intPtr != IntPtr.Zero)
        Marshal.FreeCoTaskMem(intPtr);
    }

    /// <summary>
    /// Zero out a block of memory
    /// </summary>
    /// <param name="ptr">Pointer to unmanaged memory</param>
    /// <param name="blockSize">size in bytes</param>
    public static void ClearMemory(IntPtr ptr, int blockSize)
    {
      byte b = 0;
      MemSetAction(ptr, b, blockSize);
    }
    private static Action<IntPtr, byte, int> MemSetAction { get; set; }

  }
}