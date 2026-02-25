using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LittleBit.Modules.IAppModule.Data.ProductWrappers;
using LittleBit.Modules.IAppModule.Data.Purchases;
using LittleBit.Modules.IAppModule.Services.PurchaseProcessors;
using LittleBitGames.Environment.Purchase;
using Purchase;
using UnityEngine;
using UnityEngine.Purchasing;

namespace LittleBit.Modules.IAppModule.Services.IAPServiceGroup
{
    public class IAPServiceProcessor
    {
        private readonly StoreController _controller;
        private readonly IPurchaseHandler _purchaseHandler;
        private readonly IAPService.ProductCollections _productCollection;
        private readonly List<IPurchaseValidator> _purchaseValidators;

        public event Action<string, RecieptHandler> OnSuccess;
        public event Action<string> OnFailed;
        public event Action OnProcessFinished;

        public IAPServiceProcessor(StoreController controller, 
                                    IPurchaseHandler handler, 
                                    IAPService.ProductCollections collection,
                                    List<IPurchaseValidator> validators)
        {
            _controller = controller;
            _purchaseHandler = handler;
            _productCollection = collection;
            _purchaseValidators = validators;
        }

        public void OnPurchasePending(PendingOrder pendingOrder)
        {
            var firstItem = pendingOrder.CartOrdered.Items().FirstOrDefault();
            string productId = firstItem?.Product.definition.id ?? string.Empty;

            if (string.IsNullOrEmpty(productId)) return;

            var product = _controller.GetProductById(productId);
            if (product == null) return;
            
            
            // string transactionId = pendingOrder.Info.TransactionID;
            // string receipt = pendingOrder.Info.Receipt;
            
            _purchaseHandler.ProcessPurchase(pendingOrder, product, (success, receipt) =>
            {
                if (success)
                {
                    // Запускаем валидацию прямо здесь
                    OtherValidate(pendingOrder, product, receipt);
                }
                else
                {
                    OnFailed?.Invoke(productId);
                }
            });
        }

        private async void OtherValidate(PendingOrder pendingOrder, Product product, RecieptHandler receipt)
        {
            var id = product.definition.id;

            foreach (var validator in _purchaseValidators)
            {
                var result = await validator.ValidateAsync();
                if (!result)
                {
                    OnFailed?.Invoke(id);
                    return;
                }
            }

            // Если всё успешно: подтверждаем покупку и уведомляем
            OnSuccess?.Invoke(id, receipt);
            _controller.ConfirmPurchase(pendingOrder);
        }

        public void OnPurchasesFetched(Orders orders)
        {
            foreach (var order in orders.PendingOrders) 
                OnPurchasePending(order);

            foreach (var confirmedOrder in orders.ConfirmedOrders)
            {
                var productId = confirmedOrder.CartOrdered.Items().FirstOrDefault()?.Product.definition.id;
                if (!string.IsNullOrEmpty(productId))
                {
                    var wrapper = _productCollection.GetRuntimeProductWrapper(productId);
                    (wrapper as RuntimeProductWrapper)?.Purchase();
                    OnSuccess?.Invoke(productId, null);
                }
            }

            OnProcessFinished?.Invoke();
        }
    }
}