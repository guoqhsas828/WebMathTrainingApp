using System.ComponentModel.DataAnnotations.Schema;

namespace StoreManager.Models
{
  public enum Continents
  {
    None = 0,
    Africa,
    Asia,
    Australia,
    Europe,
    America
  }

  public enum UserStatus
  {
    InActive = -1,
    Trial = 0,
    Active = 1
  }



  public class BaseEntityModel : BaseEntityModel<int>
  {
  }

  public class CatalogEntityModel : BaseEntityModel
  {
    
  }
  // This can easily be modified to be BaseEntity<T> and public T Id to support different key types.
  // Using non-generic integer types for simplicity and to ease caching logic

  public class BaseEntityModel<T> where T : struct
  {
    [NotMapped]
    public T Id { get; set; }
  }

  public enum TestAnswerType

  {

    None = 0,

    SingleChoice,

    MultipleChoice,

    Integer,

    Number,

    Text

  }

  public enum TestCategory

  {

    None,

    Math,

    Physics,

    History

  }


  public enum CloudContainer

  {

    None = 0,

    mathpicblobs = 1,

    mkg2012grp = 2,

    mkg2013grp = 3,

    mkgothers = 4,

    physicspicblobs = 8,

    misc = 9

  }



  public interface IViewModelBase<T> where T : struct
  {
    T Id { get; set; }
  }


}