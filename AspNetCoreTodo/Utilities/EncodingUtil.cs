using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebMathTraining.Utilities
{
  public static class EncodingUtil
  {
    public static byte[] StrToByteArray(string str)
    {
      var encoding = new System.Text.UTF8Encoding();
      return encoding.GetBytes(str);
    }

    public static string ByteArrayToStr(byte[] bytes)
    {
      var encoding = new System.Text.UTF8Encoding();
      return encoding.GetString(bytes, 0, bytes.Length);
    }

    public static bool SwapTextColumns(int origCol, int targetCol)
    {
      try
      {

      }
      catch (Exception e)
      {
        Console.WriteLine(e);
        throw;
      }
      return true;
    }
  }
}
