using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StoreManager;


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



    //public static string SwapTextColumns(string result, int[] targetColsToSave)

    //{

    //  var retVal = new List<string>();

    //  try

    //  {

    //    var inputLines = string.IsNullOrEmpty(result) ? new List<string>() : result.Split('\n').ToList();

    //    foreach (var questionStr in inputLines)

    //    {

    //      var questionDetails = questionStr.Split(Constants.TxtUploadColumnBreaker);

    //      if (questionDetails.Length < targetColsToSave.Length) continue;

    //      var sbuilder = new StringBuilder();

    //      foreach (var idx in targetColsToSave)

    //      {

    //        if (sbuilder.Length > 0) sbuilder.Append(Constants.TxtUploadColumnBreaker);

    //        sbuilder.Append(questionDetails[idx].TrimEnd('\r'));

    //      }



    //      retVal.Add(sbuilder.ToString());

    //    }

    //  }

    //  catch (Exception e)

    //  {

    //    Console.WriteLine(e);

    //    throw;

    //  }



    //  return String.Join('\n', retVal);

    //}

  }

}