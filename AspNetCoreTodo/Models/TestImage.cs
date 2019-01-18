using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebMathTraining.Services;
using WebMathTraining.Utilities;

namespace WebMathTraining.Models
{
  public class TestImage
  {
    public Guid Id { get; set; }

    public string Name { get; set; }

    public byte[] Data { get; set; }

    public int Length { get; set; }

    public CloudContainer Width { get; set; }

    public int Height { get; set; }

    public string ContentType { get; set; }

    [NotMapped]
    public string DataText
    {
      get
      {
        if (_dataText == null)
        {
          if (Data == null || String.Compare(ContentType, "Text", StringComparison.InvariantCultureIgnoreCase) != 0)
            _dataText = null;
          else
            _dataText = EncodingUtil.ByteArrayToStr(Data);

        }
        return _dataText;
      }
      set
      {
        if (_dataText != value)
        {
          _dataText = value;
          Data = _dataText == null ? null : EncodingUtil.StrToByteArray(_dataText);
        }
      }
    }

    [NotMapped]
    private string _dataText;
  }
}
