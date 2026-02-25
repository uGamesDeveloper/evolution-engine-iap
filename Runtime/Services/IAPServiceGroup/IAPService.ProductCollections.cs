using System.Collections.Generic;
using System.Linq;
using LittleBit.Modules.IAppModule.Data.Products;
using LittleBit.Modules.IAppModule.Data.ProductWrappers;
using LittleBit.Modules.IAppModule.Data.Purchases;
using UnityEngine.Purchasing;

namespace LittleBit.Modules.IAppModule.Services.IAPServiceGroup
{
    public partial class IAPService
    {
        public class ProductCollections
        {
            public Dictionary<string, ProductConfig> AllProducts { get; } = new();
    
            // Кэш для ворпперов, чтобы не плодить объекты в памяти
            private readonly Dictionary<string, RuntimeProductWrapper> _runtimeWrappersCache = new();
    
            private IEnumerable<Product> _productsCollection;
            private StoreController _controller;

            public void AddConfig(OfferConfig productConfig)
            {
                AddToAllProducts(productConfig);
                productConfig.Products.ToList().ForEach(AddToAllProducts);
            }

            private void AddToAllProducts(ProductConfig productConfig)
            {
                if (productConfig == null || string.IsNullOrEmpty(productConfig.Id)) return;
                if (AllProducts.ContainsKey(productConfig.Id)) return;
        
                AllProducts.Add(productConfig.Id, productConfig);
            }

            // В v5 передаем контроллер вместе с коллекцией
            public void AddUnityIAPProductCollection(IEnumerable<Product> productsCollection, StoreController controller)
            {
                _productsCollection = productsCollection;
                _controller = controller;
                _runtimeWrappersCache.Clear(); // Очищаем кэш при обновлении коллекции
            }

            public RuntimeProductWrapper GetRuntimeProductWrapper(string id)
            {
                // 1. Проверяем кэш
                if (_runtimeWrappersCache.TryGetValue(id, out var cachedWrapper))
                    return cachedWrapper;

                // 2. Если нет в кэше, ищем в продуктах Unity
                if (_productsCollection == null || _controller == null) return null;

                var product = _productsCollection.FirstOrDefault(p => p.definition.id == id);
                if (product != null)
                {
                    var newWrapper = new RuntimeProductWrapper(product, _controller);
                    _runtimeWrappersCache.Add(id, newWrapper);
                    return newWrapper;
                }

                return null;
            }
        }
    }
}