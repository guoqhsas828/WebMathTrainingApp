﻿using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.Web.ViewModels;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StoreManager.Interfaces;
using StoreManager.Models;
using StoreManager.Services;
using StoreManager.Specifications;

namespace Microsoft.eShopWeb.Web.Services
{
    /// <summary>
    /// This is a UI-specific service so belongs in UI project. It does not contain any business logic and works
    /// with UI-specific types (view models and SelectListItem types).
    /// </summary>
    public class CatalogViewModelService : ICatalogViewModelService
    {
        private readonly ILogger<CatalogViewModelService> _logger;
        private readonly IAsyncRepository<Product> _itemRepository;
        private readonly IAsyncRepository<CatalogBrand> _brandRepository;
        private readonly IAsyncRepository<ProductType> _typeRepository;
        private readonly IUriComposer _uriComposer;

        public CatalogViewModelService(
            ILoggerFactory loggerFactory,
            IAsyncRepository<Product> itemRepository,
            IAsyncRepository<CatalogBrand> brandRepository,
            IAsyncRepository<ProductType> typeRepository,
            IUriComposer uriComposer)
        {
            _logger = loggerFactory.CreateLogger<CatalogViewModelService>();
            _itemRepository = itemRepository;
            _brandRepository = brandRepository;
            _typeRepository = typeRepository;
            _uriComposer = uriComposer;
        }

        public async Task<CatalogIndexViewModel> GetCatalogItems(int pageIndex, int itemsPage, int? brandId, int? typeId)
        {
            _logger.LogInformation("GetCatalogItems called.");

            var filterSpecification = new CatalogFilterSpecification(brandId, typeId);
            var filterPaginatedSpecification =
                new CatalogFilterPaginatedSpecification(itemsPage * pageIndex, itemsPage, brandId, typeId);

            // the implementation below using ForEach and Count. We need a List.
            var itemsOnPage = await _itemRepository.ListAsync(filterPaginatedSpecification);
            var totalItems = await _itemRepository.CountAsync(filterSpecification);

            foreach (var itemOnPage in itemsOnPage)
            {
                itemOnPage.ProductImageUrl = _uriComposer.ComposePicUri(itemOnPage.ProductImageUrl);
            }

            var vm = new CatalogIndexViewModel()
            {
                CatalogItems = itemsOnPage.Select(i => new CatalogItemViewModel()
                {
                    Id = i.Id,
                    Name = i.ProductName,
                    PictureUri = i.ProductImageUrl,
                    Price = Convert.ToDecimal(i.DefaultSellingPrice)
                }),
                Brands = await GetBrands(),
                Types = await GetTypes(),
                BrandFilterApplied = brandId ?? 0,
                TypesFilterApplied = typeId ?? 0,
                PaginationInfo = new PaginationInfoViewModel()
                {
                    ActualPage = pageIndex,
                    ItemsPerPage = itemsOnPage.Count,
                    TotalItems = totalItems,
                    TotalPages = int.Parse(Math.Ceiling(((decimal)totalItems / itemsPage)).ToString())
                }
            };

            vm.PaginationInfo.Next = (vm.PaginationInfo.ActualPage == vm.PaginationInfo.TotalPages - 1) ? "is-disabled" : "";
            vm.PaginationInfo.Previous = (vm.PaginationInfo.ActualPage == 0) ? "is-disabled" : "";

            return vm;
        }

        public async Task<IEnumerable<SelectListItem>> GetBrands()
        {
            _logger.LogInformation("GetBrands called.");
            var brands = await _brandRepository.ListAllAsync();

            var items = new List<SelectListItem>
            {
                new SelectListItem() { Value = null, Text = "All", Selected = true }
            };
            foreach (CatalogBrand brand in brands)
            {
                items.Add(new SelectListItem() { Value = brand.Id.ToString(), Text = brand.Brand });
            }

            return items;
        }

      public async Task<IEnumerable<SelectListItem>> GetTypes()
      {
        _logger.LogInformation("GetTypes called.");
        var types = await _typeRepository.ListAllAsync();
        var items = new List<SelectListItem>
        {
          new SelectListItem() {Value = null, Text = "All", Selected = true}
        };
        foreach (var type in types)
        {
          items.Add(new SelectListItem() {Value = type.Id.ToString(), Text = type.ProductTypeName});
        }

        return items;
      }
    }
}
