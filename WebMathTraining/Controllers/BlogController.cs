﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebMathTraining.Data;
using WebMathTraining.Services;
using WebEssentials.AspNetCore.Pwa;
using WebMathTraining.Models;

namespace WebMathTraining.Controllers
{
  [Route("[controller]/[action]")]
  public class BlogController : Controller
    {
    private readonly IBlogService _blog;
    private readonly IOptionsSnapshot<BlogSettings> _settings;
    private readonly WebManifest _manifest;

    public BlogController(IBlogService blogService, IOptionsSnapshot<BlogSettings> settings, WebManifest manifest)
    {
      _blog = blogService;
      _settings = settings;
      _manifest = manifest;
    }

    //[Route("/blog/{page:int?}")]
    [OutputCache(Profile = "default")]
    public async Task<IActionResult> Index(int page = 0)
    {
      var posts = await _blog.GetPosts(_settings.Value.PostsPerPage, _settings.Value.PostsPerPage * page);
      ViewData["Title"] = _manifest.Name;
      ViewData["Description"] = _manifest.Description;
      ViewData["prev"] = $"/Blog/Index?page={page + 1}";
      ViewData["next"] = $"/Blog/Index?page={(page <= 1 ? null : page - 1 + "/")}";
      ViewData["page"] = page;
      return View(nameof(Index), posts); //
    }

    //[Route("/blog/category/{category}/{page:int?}")]
    [OutputCache(Profile = "default")]
    public async Task<IActionResult> Category(string category, int page = 0)
    {
      var posts = (await _blog.GetPostsByCategory(category)).Skip(_settings.Value.PostsPerPage * page).Take(_settings.Value.PostsPerPage);
      ViewData["Title"] = _manifest.Name + " " + category;
      ViewData["Description"] = $"Articles posted in the {category} category";
      ViewData["prev"] = $"/blog/category/{category}/{page + 1}/";
      ViewData["next"] = $"/blog/category/{category}/{(page <= 1 ? null : page - 1 + "/")}";
      ViewData["page"] = page;
      return View("~/Views/Blog/Index.cshtml", posts);
    }

    // This is for redirecting potential existing URLs from the old URL format
    [Route("/blog/post/{slug}")]
    [HttpGet]
    public IActionResult Redirects(string slug)
    {
      return LocalRedirectPermanent($"/blog/{slug}");
    }

    [Route("/blog/{slug?}")]
    [OutputCache(Profile = "default")]
    public async Task<IActionResult> Post(string slug)
    {
      var post = await _blog.GetPostBySlug(slug);

      if (post != null)
      {
        return View(post);
      }

      return NotFound();
    }

    //[Route("/blog/edit/{id?}")]
    [HttpGet, Authorize]
    public async Task<IActionResult> Edit(string id)
    {
      ViewData["AllCats"] = (await _blog.GetCategories()).ToList();

      if (string.IsNullOrEmpty(id))
      {
        return View(new BlogPost());
      }

      var post = await _blog.GetPostById(id);

      if (post != null)
      {
        return View(post);
      }

      return NotFound();
    }

    [Route("/blog/{slug?}")]
    [HttpPost, Authorize, AutoValidateAntiforgeryToken]
    public async Task<IActionResult> UpdatePost(BlogPost post)
    {
      if (!ModelState.IsValid)
      {
        return View("Edit", post);
      }

      var existing = await _blog.GetPostById(post.ID) ?? post;
      string categories = Request.Form["categories"];

      existing.Categories = categories.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim().ToLowerInvariant()).ToList();
      existing.Title = post.Title.Trim();
      existing.Slug = !string.IsNullOrWhiteSpace(post.Slug) ? post.Slug.Trim() : Models.BlogPost.CreateSlug(post.Title);
      existing.IsPublished = post.IsPublished;
      existing.Content = post.Content.Trim();
      existing.Excerpt = post.Excerpt.Trim();

      await SaveFilesToDisk(existing);

      await _blog.SavePost(existing);

      return Redirect(post.GetEncodedLink());
    }

    private async Task SaveFilesToDisk(BlogPost post)
    {
      var imgRegex = new Regex("<img[^>].+ />", RegexOptions.IgnoreCase | RegexOptions.Compiled);
      var base64Regex = new Regex("data:[^/]+/(?<ext>[a-z]+);base64,(?<base64>.+)", RegexOptions.IgnoreCase);

      foreach (Match match in imgRegex.Matches(post.Content))
      {
        XmlDocument doc = new XmlDocument();
        doc.LoadXml("<root>" + match.Value + "</root>");

        var img = doc.FirstChild.FirstChild;
        var srcNode = img.Attributes["src"];
        var fileNameNode = img.Attributes["data-filename"];

        // The HTML editor creates base64 DataURIs which we'll have to convert to image files on disk
        if (srcNode != null && fileNameNode != null)
        {
          var base64Match = base64Regex.Match(srcNode.Value);
          if (base64Match.Success)
          {
            byte[] bytes = Convert.FromBase64String(base64Match.Groups["base64"].Value);
            srcNode.Value = await _blog.SaveFile(bytes, fileNameNode.Value).ConfigureAwait(false);

            img.Attributes.Remove(fileNameNode);
            post.Content = post.Content.Replace(match.Value, img.OuterXml);
          }
        }
      }
    }

    //[Route("/blog/deletepost/{id}")]
    [HttpPost, Authorize, AutoValidateAntiforgeryToken]
    public async Task<IActionResult> DeletePost(string id)
    {
      var existing = await _blog.GetPostById(id);

      if (existing != null)
      {
        await _blog.DeletePost(existing);
        return Redirect("/");
      }

      return NotFound();
    }

    //[Route("/blog/comment/{postId}")]
    [HttpPost]
    public async Task<IActionResult> AddComment(string postId, BlogTag comment)
    {
      var post = await _blog.GetPostById(postId);

      if (!ModelState.IsValid)
      {
        return View("Post", post);
      }

      if (post == null || !post.AreCommentsOpen(_settings.Value.CommentsCloseAfterDays))
      {
        return NotFound();
      }

      comment.IsAdmin = User.Identity.IsAuthenticated;
      comment.Content = comment.Content.Trim();
      comment.Author = comment.Author.Trim();
      if (string.IsNullOrEmpty(comment.Author)) comment.Author = User.Identity.Name;
      comment.Email = comment.Email.Trim();
      
      // the website form key should have been removed by javascript
      // unless the comment was posted by a spam robot
      //if (!Request.Form.ContainsKey("website"))
      {
        post.Comments.Add(comment);
        await _blog.SavePost(post);
      }

      return Redirect(post.GetEncodedLink() + "#" + comment.Id);
    }

    //[Route("/blog/comment/{postId}/{commentId}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(string postId, string commentId)
    {
      var post = await _blog.GetPostById(postId);

      if (post == null)
      {
        return NotFound();
      }

      var comment = post.Comments.FirstOrDefault(c => c.Id.Equals(commentId, StringComparison.OrdinalIgnoreCase));

      if (comment == null)
      {
        return NotFound();
      }

      post.Comments.Remove(comment);
      await _blog.SavePost(post);

      return Redirect(post.GetEncodedLink() + "#comments");
    }
  }
}

