using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using StoreManager.Models;
using WilderMinds.MetaWeblog;
using WebMathTraining.Models;

namespace WebMathTraining.Services
{
  public class MetaWeblogService : IMetaWeblogProvider
  {
    private readonly IBlogService _blog;

    private readonly IConfiguration _config;

    private readonly UserManager<ApplicationUser> _userServices;

    private readonly IHttpContextAccessor _context;

    public MetaWeblogService(IBlogService blog, IConfiguration config, IHttpContextAccessor context, UserManager<ApplicationUser> userServices)
    {
      _blog = blog;
      _config = config;
      _userServices = userServices;
      _context = context;
    }

    public async Task<Page> GetPageAsync(string v1, string v2, string v3, string v4)
    {
      throw new NotImplementedException();
    }

    public async Task<Page[]> GetPagesAsync(string v1, string v2, string v3, int v4)
    {
      throw new NotImplementedException();
    }

    public async Task<bool> DeletePageAsync(string v1, string v2, string v3, string v4)
    {
      throw new NotImplementedException();
    }

    public async Task<Author[]> GetAuthorsAsync(string v1, string v2, string v3)
    {
      throw new NotImplementedException();
    }

    public async Task<string> AddPageAsync(string v1, string v2, string v3, Page v4, bool v5)
    {
      throw new NotImplementedException();
    }

    public async Task<bool> EditPageAsync(string v1, string v2, string v3, string p, Page v4, bool v5)
    {
      throw new NotImplementedException();
    }

    public async Task<string> AddPostAsync(string blogid, string username, string password, WilderMinds.MetaWeblog.Post post, bool publish)
    {
      ValidateUser(username, password);
      var newPost = new BlogPost
      {
        Title = post.title,
        Slug = !string.IsNullOrWhiteSpace(post.wp_slug) ? post.wp_slug : BlogPost.CreateSlug(post.title),
        Content = post.description,
        IsPublished = publish,
        Categories = post.categories
      };

      if (post.dateCreated != DateTime.MinValue)
      {
        newPost.PubDate = post.dateCreated;
      }

      _blog.SavePost(newPost).GetAwaiter().GetResult();
      return newPost.ID;

    }



    public async Task<bool> DeletePostAsync(string key, string postid, string username, string password, bool publish)
    {
      ValidateUser(username, password);
      var post = _blog.GetPostById(postid).GetAwaiter().GetResult();
      if (post != null)
      {
        _blog.DeletePost(post).GetAwaiter().GetResult();
        return true;
      }
      return false;
    }



    public async Task<bool> EditPostAsync(string postid, string username, string password, WilderMinds.MetaWeblog.Post post, bool publish)
    {
      ValidateUser(username, password);
      var existing = _blog.GetPostById(postid).GetAwaiter().GetResult();
      if (existing != null)
      {
        existing.Title = post.title;
        existing.Slug = post.wp_slug;
        existing.Content = post.description;
        existing.IsPublished = publish;
        existing.Categories = post.categories;

        if (post.dateCreated != DateTime.MinValue)
        {
          existing.PubDate = post.dateCreated;
        }
        _blog.SavePost(existing).GetAwaiter().GetResult();
        return true;
      }

      return false;

    }

    public async Task<CategoryInfo[]> GetCategoriesAsync(string blogid, string username, string password)
    {
      ValidateUser(username, password);
      return _blog.GetCategories().GetAwaiter().GetResult().Select(cat =>
                         new CategoryInfo
                         {
                           categoryid = cat,
                           title = cat
                         }).ToArray();
    }



    public async Task<WilderMinds.MetaWeblog.Post> GetPostAsync(string postid, string username, string password)
    {
      ValidateUser(username, password);
      var post = _blog.GetPostById(postid).GetAwaiter().GetResult();

      if (post != null)
      {
        return ToMetaWebLogPost(post);
      }

      return null;
    }



    public async Task<WilderMinds.MetaWeblog.Post[]> GetRecentPostsAsync(string blogid, string username, string password, int numberOfPosts)
    {
      ValidateUser(username, password);
      return _blog.GetPosts(numberOfPosts).GetAwaiter().GetResult().Select(ToMetaWebLogPost).ToArray();
    }



    public async Task<BlogInfo[]> GetUsersBlogsAsync(string key, string username, string password)
    {
      ValidateUser(username, password);
      var request = _context.HttpContext.Request;
      string url = request.Scheme + "://" + request.Host;

      return new[] { new BlogInfo {
                blogid ="1",
                blogName = _config["blog:name"] ?? nameof(MetaWeblogService),
                url = url
            }};

    }



    public async Task<MediaObjectInfo> NewMediaObjectAsync(string blogid, string username, string password, MediaObject mediaObject)
    {
      ValidateUser(username, password);
      byte[] bytes = Convert.FromBase64String(mediaObject.bits);
      string path = _blog.SaveFile(bytes, mediaObject.name).GetAwaiter().GetResult();
      return new MediaObjectInfo { url = path };
    }



    public async Task<UserInfo> GetUserInfoAsync(string key, string username, string password)
    {
      ValidateUser(username, password);
      throw new NotImplementedException();
    }



    public async Task<int> AddCategoryAsync(string key, string username, string password, NewCategory category)
    {
      ValidateUser(username, password);
      throw new NotImplementedException();
    }



    private async void ValidateUser(string username, string password)
    {
      var user = await _userServices.FindByNameAsync(username);
      if (await _userServices.CheckPasswordAsync(user, password) == false)
      {
        throw new MetaWeblogException("Unauthorized");
      }

      var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
      identity.AddClaim(new Claim(ClaimTypes.Name, username));
      _context.HttpContext.User = new ClaimsPrincipal(identity);
    }



    private WilderMinds.MetaWeblog.Post ToMetaWebLogPost(BlogPost post)
    {
      var request = _context.HttpContext.Request;
      string url = request.Scheme + "://" + request.Host;

      return new WilderMinds.MetaWeblog.Post

      {

        postid = post.ID,

        title = post.Title,

        wp_slug = post.Slug,

        permalink = url + post.GetLink(),

        dateCreated = post.PubDate,

        description = post.Content,

        categories = post.Categories.ToArray()

      };
    }
  }
}