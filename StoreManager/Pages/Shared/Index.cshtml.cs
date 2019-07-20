using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.eShopWeb.Web.ViewModels;
using StoreManager.Services;

namespace OrderManager.Pages.Shared
{
  public class IndexModel : PageModel
  {
    private readonly ICatalogViewModelService _catalogViewModelService;

    public IndexModel(ICatalogViewModelService catalogViewModelService)
    {
      _catalogViewModelService = catalogViewModelService;
      CatalogModel = _catalogViewModelService.GetCatalogItems(0, StoreManager.Constants.ITEMS_PER_PAGE, null, null).Result;
    }

    public CatalogIndexViewModel CatalogModel { get; set; } = new CatalogIndexViewModel();

    public async Task OnGet(CatalogIndexViewModel catalogModel, int? pageId)
    {
      CatalogModel = await _catalogViewModelService.GetCatalogItems(pageId ?? 0, StoreManager.Constants.ITEMS_PER_PAGE, catalogModel.BrandFilterApplied, catalogModel.TypesFilterApplied);
    }
    public void OnGet()
    {
      CatalogModel = _catalogViewModelService.GetCatalogItems(0, StoreManager.Constants.ITEMS_PER_PAGE, null, null).Result;
    }
  }
}