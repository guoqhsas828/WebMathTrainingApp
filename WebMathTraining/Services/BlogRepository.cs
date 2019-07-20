using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebMathTraining.Models;

namespace WebMathTraining.Services
{
  public abstract class InMemoryBlogServiceBase : IBlogService
  {
    public InMemoryBlogServiceBase(IHttpContextAccessor contextAccessor)
    {
      ContextAccessor = contextAccessor;
    }



    protected List<BlogPost> Cache { get; set; }

    protected IHttpContextAccessor ContextAccessor { get; }



    public virtual Task<IEnumerable<BlogPost>> GetPosts(int count, int skip = 0)

    {

      bool isAdmin = IsAdmin();



      var BlogPosts = Cache

          .Where(p => p.PubDate <= DateTime.UtcNow && (p.IsPublished || isAdmin))

          .Skip(skip)

          .Take(count);



      return Task.FromResult(BlogPosts);

    }



    public virtual Task<IEnumerable<BlogPost>> GetPostsByCategory(string category)

    {

      bool isAdmin = IsAdmin();



      var BlogPosts = from p in Cache

                  where p.PubDate <= DateTime.UtcNow && (p.IsPublished || isAdmin)

                  where p.Categories.Contains(category, StringComparer.OrdinalIgnoreCase)

                  select p;



      return Task.FromResult(BlogPosts);



    }



    public virtual Task<BlogPost> GetPostBySlug(string slug)

    {

      var BlogPost = Cache.FirstOrDefault(p => p.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

      bool isAdmin = IsAdmin();



      if (BlogPost != null && BlogPost.PubDate <= DateTime.UtcNow && (BlogPost.IsPublished || isAdmin))

      {

        return Task.FromResult(BlogPost);

      }



      return Task.FromResult<BlogPost>(null);

    }



    public virtual Task<BlogPost> GetPostById(string id)

    {

      var BlogPost = Cache.FirstOrDefault(p => p.ID.Equals(id, StringComparison.OrdinalIgnoreCase));

      bool isAdmin = IsAdmin();



      if (BlogPost != null && BlogPost.PubDate <= DateTime.UtcNow && (BlogPost.IsPublished || isAdmin))

      {

        return Task.FromResult(BlogPost);

      }



      return Task.FromResult<BlogPost>(null);

    }



    public virtual Task<IEnumerable<string>> GetCategories()

    {

      bool isAdmin = IsAdmin();



      var categories = Cache

          .Where(p => p.IsPublished || isAdmin)

          .SelectMany(BlogPost => BlogPost.Categories)

          .Select(cat => cat.ToLowerInvariant())

          .Distinct();



      return Task.FromResult(categories);

    }



    public abstract Task SavePost(BlogPost BlogPost);



    public abstract Task DeletePost(BlogPost BlogPost);



    public abstract Task<string> SaveFile(byte[] bytes, string fileName, string suffix = null);



    protected void SortCache()

    {

      Cache.Sort((p1, p2) => p2.PubDate.CompareTo(p1.PubDate));

    }



    protected bool IsAdmin()

    {

      return ContextAccessor.HttpContext?.User?.Identity.IsAuthenticated == true;

    }

  }

}
