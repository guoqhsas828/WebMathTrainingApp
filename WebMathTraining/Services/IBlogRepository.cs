using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebMathTraining.Models;

namespace WebMathTraining.Services
{
  public interface IBlogService
  {
    Task<IEnumerable<BlogPost>> GetPosts(int count, int skip = 0);

    Task<IEnumerable<BlogPost>> GetPostsByCategory(string category);

    Task<BlogPost> GetPostBySlug(string slug);

    Task<BlogPost> GetPostById(string id);

    Task<IEnumerable<string>> GetCategories();

    Task SavePost(BlogPost BlogPost);

    Task DeletePost(BlogPost BlogPost);

    Task<string> SaveFile(byte[] bytes, string fileName, string suffix = null);
  }


  //public interface ITestService
  //{
  //  string GetData();
  //}

  //public class TestService : ITestService
  //{
  //  private AppConfiguration _settings;

  //  public TestService(AppConfiguration config)
  //  {
  //    _settings = config;
  //  }

  //  public string GetData()
  //  {
  //    return "some magic string";
  //  }
  //}
}
