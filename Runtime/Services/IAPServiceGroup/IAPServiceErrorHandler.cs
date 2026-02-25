using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

namespace LittleBit.Modules.IAppModule.Services.IAPServiceGroup
{
    
    public class IAPServiceErrorHandler
    {
        public event Action<string> OnPurchaseFailedNotify;
        public event Action<bool, string> OnRestoreFailedNotify;

        
        public void OnProductsFetchFailed(ProductFetchFailed description)
        {
            string failedProducts = string.Join(", ", description.FailedFetchProducts.Select(p => p.id));
            Debug.LogError($"Products fetch failed! Reason: {description.FailureReason}. Failed products: [{failedProducts}]");
        }

        public void OnPurchaseFailed(FailedOrder failedOrder)
        {
            var firstItem = failedOrder.CartOrdered.Items().FirstOrDefault();
            string productId = firstItem?.Product.definition.id ?? "unknown";

            OnPurchaseFailedNotify?.Invoke(productId);

            Debug.LogError($"[IAPService] Purchasing failed! " +
                           $"Product ID: {productId}. " +
                           $"Reason: {failedOrder.FailureReason}. " +
                           $"Details: {failedOrder.Details}");
        }

        public void OnPurchasesFetchFailed(PurchasesFetchFailureDescription description)
        {
            // Просто передаем сообщение об ошибке дальше
            OnRestoreFailedNotify?.Invoke(false, $"Restore failed! {description.Message}");
        }
    }
    
   
}