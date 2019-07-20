using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.Web.Interfaces;
using Microsoft.eShopWeb.Web.Pages.Basket;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StoreManager.Interfaces;
using StoreManager.Models;
using StoreManager.Specifications;

namespace Microsoft.eShopWeb.Web.Services
{
    public class BasketViewModelService : IBasketViewModelService
    {
        private readonly ICatalogRepository<Basket> _basketRepository;
        private readonly IUriComposer _uriComposer;
        private readonly IAsyncRepository<Product> _itemRepository;

        public BasketViewModelService(ICatalogRepository<Basket> basketRepository,
            IAsyncRepository<Product> itemRepository,
            IUriComposer uriComposer)
        {
            _basketRepository = basketRepository;
            _uriComposer = uriComposer;
            _itemRepository = itemRepository;
        }

        public async Task<BasketViewModel> GetOrCreateBasketForUser(string userName)
        {
            var basketSpec = new BasketWithItemsSpecification(userName);
            var basket = (await _basketRepository.ListAsync(basketSpec)).FirstOrDefault();

            if (basket == null)
            {
                return await CreateBasketForUser(userName);
            }
            return await CreateViewModelFromBasket(basket);
        }

        private async Task<BasketViewModel> CreateViewModelFromBasket(Basket basket)
        {
            var viewModel = new BasketViewModel();
            viewModel.Id = basket.Id;
            viewModel.BuyerId = basket.BuyerId;
            viewModel.Items = await GetBasketItems(basket.Items); ;
            return viewModel;
        }

        private async Task<BasketViewModel> CreateBasketForUser(string userId)
        {
            var basket = new Basket() { BuyerId = userId };
            await _basketRepository.AddAsync(basket);

            return new BasketViewModel()
            {
                BuyerId = basket.BuyerId,
                Id = basket.Id,
                Items = new List<BasketItemViewModel>()
            };
        }

        private async Task<List<BasketItemViewModel>> GetBasketItems(ICollection<BasketItem> basketItems)
        {
            var items = new List<BasketItemViewModel>();
            foreach (var item in basketItems)
            {
                var itemModel = new BasketItemViewModel
                {
                    Id = item.Id,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    CatalogItemId = item.CatalogItemId
                };
                var catalogItem = await _itemRepository.GetByIdAsync(item.CatalogItemId);
                itemModel.PictureUrl = _uriComposer.ComposePicUri(catalogItem.ProductImageUrl);
                itemModel.ProductName = catalogItem.ProductName;
                items.Add(itemModel);
            }

            return items;
        }
    }
}
