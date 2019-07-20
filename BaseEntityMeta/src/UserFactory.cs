// 
// Copyright (c) WebMathTraining 2002-2017. All rights reserved.
// 

using System.Collections;
using System.Linq;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// </summary>
  public class UserFactory
  {
    /// <summary>
    /// Find all users
    /// </summary>
    public static IList FindAll()
    {
      return EntityContext.Query<User>().ToList();
    }

    /// <summary>
    /// Find user with specified userName
    /// </summary>
    public static User FindByName(string userName)
    {
      return EntityContext.Query<User>().SingleOrDefault(u => u.Name == userName);
    }

    /// <summary>
    ///   Checks to see if a User is not an Admin and has the "ShowInTraderList" Named Policy set
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public static bool ShowInTradersList(User user)
    {
      return user.Role != null && !user.Role.Administrator && SecurityPolicy.CheckNamedPolicy(user.Role, "ShowInTraderList");
    }

    #region Factory

    /// <summary>
    /// </summary>
    public static User CreateInstance()
    {
      return (User)Entity.CreateInstance();
    }

    /// <summary>
    /// </summary>
    public static ClassMeta Entity
    {
      get
      {
        if (entity_ == null)
          entity_ = ClassCache.Find("User");
        return entity_;
      }
    }

    private static ClassMeta entity_;

    #endregion
  } // class UserFactory
} // namespace WebMathTraining.Risk
