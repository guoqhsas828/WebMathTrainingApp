﻿using StoreManager.Models;

namespace StoreManager.Specifications
{
    public sealed class BasketWithItemsSpecification : BaseSpecification<Basket>
    {
        public BasketWithItemsSpecification(int basketId)
            :base(b => b.Id == basketId)
        {
            AddInclude(b => b.Items);
        }
        public BasketWithItemsSpecification(string buyerId)
            :base(b => b.BuyerId == buyerId)
        {
            AddInclude(b => b.Items);
        }
    }
}
