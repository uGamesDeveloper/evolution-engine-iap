using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

namespace LittleBit.Modules.IAppModule.Data.ProductWrappers
{
    public class RuntimeProductWrapper : ProductWrapper
    {
        private readonly StoreController _controller;


        public RuntimeProductWrapper(Product product, StoreController controller)
        {
            _controller = controller;

            Definition = new() { Id = product.definition.id, Type = product.definition.type };

            TransactionData = new()
            {
                HasReceiptGetter = () => IsPurchased,
                ReceiptGetter = () => GetOrder()?.Info.Receipt ?? string.Empty,
                TransactionIdGetter = () => GetOrder()?.Info.TransactionID ?? string.Empty
            };

            Metadata = new()
            {
                CurrencyCode = product.metadata.isoCurrencyCode,
                CurrencySymbol = CurrencyCodeMapper.GetSymbol(product.metadata.isoCurrencyCode),
                LocalizedDescription = product.metadata.localizedDescription,
                LocalizedPrice = product.metadata.localizedPrice,
                LocalizedPriceString = product.metadata.localizedPriceString,
                LocalizedTitle = product.metadata.localizedTitle,
                
                CanPurchaseGetter = () => Definition.Type == ProductType.Consumable || !IsPurchased,
                IsPurchasedGetter = () => IsPurchasedInPrefs || 
                                          !string.IsNullOrEmpty(TransactionData.Receipt) || 
                                          !string.IsNullOrEmpty(TransactionData.TransactionId)
            };
        }

        private bool IsPurchased => Metadata.IsPurchasedGetter();
        private bool IsPurchasedInPrefs => PlayerPrefs.GetInt(Definitions.PurchasedPrefsPrefix + Definition.Id, 0) > 0;

        private Order GetOrder()
        {
            if (_controller == null) return null;
            
            var purchases = _controller.GetPurchases();
            return purchases.FirstOrDefault(order => 
                order.CartOrdered.Items().Any(p => p.Product.definition.id == Definition.Id));
        }



        internal void Purchase()
        {
            PlayerPrefs.SetInt(Definitions.PurchasedPrefsPrefix + Definition.Id, 1);
            PlayerPrefs.Save();
        }

    }
}