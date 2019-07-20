//using BaseEntity.Metadata;
//using BaseEntity.Shared;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using StoreManager.Models;
using WebMathTraining.Utilities;

//using WebMathTraining.Services;
//using WebMathTraining.Utilities;

namespace WebMathTraining.Models
{
//  [Entity(EntityId = 101, TableName = "TestImages", Key = new string[] { "Id" })]
  public class TestImage : CatalogEntityModel //PersistentObject
  {

//    [GuidProperty(AllowNull = false)]

//    public Guid Id { get; set; }
    [Key]
    public int ObjectId
    {
      get { return Id; }
      set { Id = value; }
    }

//    [StringProperty(MaxLength = 128)]
    [MaxLength(128)] public string Name { get; set; }



//    [BinaryBlobProperty]
    public byte[] Data { get; set; }



//    [NumericProperty]
    public int Length { get; set; }



//    [EnumProperty]
    public CloudContainer Width { get; set; }



//    [NumericProperty]
    public int Height { get; set; }



//    [StringProperty(MaxLength = 32)]
    [MaxLength(32)] public string ContentType { get; set; }

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



    [NotMapped] private string _dataText;

  }

}