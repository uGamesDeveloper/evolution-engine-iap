using System;
using System.Collections.Generic;
using LittleBit.Modules.IAppModule.Data.ProductWrappers;
using LittleBit.Modules.IAppModule.Data.Purchases;
using LittleBit.Modules.IAppModule.Services.PurchaseProcessors;
using LittleBitGames.Environment.Events;
using LittleBitGames.Environment.Purchase;
using Purchase;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Purchasing;

namespace LittleBit.Modules.IAppModule.Services.IAPServiceGroup
{
    public partial class IAPService : IService, IIAPService,IIAPRevenueEvent, IDisposable
    {
        //ToDo понять что это такое)
        private const string CartType = "Shop";
        private const string Signature = "VVO";
        private const string ItemType = "Offer";
        
        
        private Action _onRestoreCompleted;
        private StoreController _controller;
        
        private readonly ProductCollections _productCollection;
        private readonly IPurchaseHandler _purchaseHandler;
        private readonly List<OfferConfig> _offerConfigs;
        private readonly List<IPurchaseValidator> _purchaseValidators;
        public event Action<string, RecieptHandler> OnPurchasingSuccess;
        public event Action<string> OnPurchasingFailed;
        public event Action<bool, string> OnPurchasingRestored;
        public event Action OnInitializationComplete;
        
        public event Action<IDataEventEcommerce> OnPurchasingProductSuccess;
        
        private readonly IAPServiceErrorHandler _errorHandler;
        private IAPServiceProcessor _processor;

        /// <summary>
        /// Возвращает true, если Unity Services инициализированы и продукты загружены из стора.
        /// </summary>
        public bool IsInitialized { get; private set; }

        
        public bool PurchaseRestored
        {
            get => PlayerPrefs.GetInt("PurchaseRestored", 0) == 1;
            private set => PlayerPrefs.SetInt("PurchaseRestored", value ? 1 : 0);
        }

        public IAPService(IPurchaseHandler purchaseHandler, List<OfferConfig> offerConfigs, List<IPurchaseValidator> purchaseValidators)
        {
            _productCollection = new ProductCollections();
            _purchaseHandler = purchaseHandler;
            _offerConfigs = offerConfigs;
            _purchaseValidators = purchaseValidators;
            _errorHandler = new IAPServiceErrorHandler();
            
            InitAsync();
        }
        
        
        /// <summary>
        /// Асинхронная инициализация: Unity Services -> StoreController -> Processor.
        /// </summary>
        private async void InitAsync()
        {
            _errorHandler.OnPurchaseFailedNotify += InvokePurchaseFailed;
            _errorHandler.OnRestoreFailedNotify += InvokeRestoreFailed;
            
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }
                
                _controller = UnityIAPServices.StoreController();
                
                _processor = new IAPServiceProcessor(_controller, _purchaseHandler, _productCollection, _purchaseValidators);
                _processor.OnSuccess += InvokePurchasingSuccess;
                _processor.OnFailed += InvokePurchasingFailed;
                _processor.OnProcessFinished += () =>
                {
                    _onRestoreCompleted?.Invoke();
                    _onRestoreCompleted = null;
                };
                
                _controller.OnProductsFetched += OnProductsFetched;
                _controller.OnProductsFetchFailed += _errorHandler.OnProductsFetchFailed;
                
                _controller.OnPurchasePending += _processor.OnPurchasePending;
                _controller.OnPurchaseFailed += _errorHandler.OnPurchaseFailed;
                
                _controller.OnPurchasesFetched += _processor.OnPurchasesFetched;
                _controller.OnPurchasesFetchFailed += _errorHandler.OnPurchasesFetchFailed;

                await _controller.Connect();
                
                var products = new List<ProductDefinition>();
                foreach (var offer in _offerConfigs)
                {
                    products.Add(new ProductDefinition(offer.Id, offer.ProductType));
                    _productCollection.AddConfig(offer);
                }
                
                _controller.FetchProducts(products);
            }
            catch (Exception e)
            {
                Debug.LogError($"IAP Initialization failed: {e.Message}");
            }
        }

        /// <summary>
        /// Обработка успешной покупки: уведомление подписчиков и отправка события аналитики.
        /// </summary>
        private void InvokePurchasingSuccess(string id, RecieptHandler receipt)
        {
            OnPurchasingSuccess?.Invoke(id, receipt);
            PurchasingProductSuccess(id, receipt);
        }

        private void InvokePurchasingFailed(string id) => OnPurchasingFailed?.Invoke(id);
        
        
        private void InvokePurchaseFailed(string id) => OnPurchasingFailed?.Invoke(id);
        
        /// <summary>
        /// Обработка ошибок восстановления. Срабатывает только если был активен процесс Restore.
        /// </summary>
        private void InvokeRestoreFailed(bool success, string message)
        {
            if (_onRestoreCompleted != null)
            {
                _onRestoreCompleted = null;
                OnPurchasingRestored?.Invoke(success, message);
            }
        }
        
        /// <summary>
        /// Вызывается при успешном получении метаданных продуктов (цены, описания) из магазина.
        /// </summary>
        private void OnProductsFetched(List<Product> products)
        {
            _productCollection.AddUnityIAPProductCollection(products, _controller);
            IsInitialized = true;
            OnInitializationComplete?.Invoke();
        }
        
        
        /// <summary>
        /// Запускает процесс покупки продукта.
        /// </summary>
        /// <param name="id">Идентификатор продукта из OfferConfig.</param>
        /// <param name="freePurchase">Если true, покупка считается успешной без обращения к стору.</param>
        public void Purchase(string id, bool freePurchase)
        {
            foreach (var purchaseValidator in _purchaseValidators)
            {
                purchaseValidator.Reset();
            }
            
            var product = _controller.GetProductById(id);

            if (product == null || !product.availableToPurchase)
            {
                Debug.LogWarning($"[IAPService] Product {id} is not available for purchase.");
                return;
            }

            if (freePurchase)
            {
                OnPurchasingSuccess?.Invoke(id, null);
                PurchasingProductSuccess(id, null);
                return;
            }
            
            _controller.PurchaseProduct(product);
        }
        
        /// <summary>
        /// Возвращает обертку продукта для работы с метаданными и статусом владения в UI.
        /// </summary>
        public IProductWrapper GetProductWrapper(string id)
        {
            var wrapper = _productCollection.GetRuntimeProductWrapper(id);
            if (wrapper == null)
            {
                Debug.LogError($"[IAPService] Can't find product wrapper with id:{id}");
            }
            return wrapper;
        }
        
        /// <summary>
        /// Запускает процесс восстановления Non-Consumable покупок.
        /// После завершения перебора всех заказов будет вызван OnProcessFinished в процессоре.
        /// </summary>
        public void RestorePurchasedProducts()
        {
            if (PurchaseRestored) return;

            // Вместо установки флага, мы определяем, что нужно сделать, когда придет ответ
            _onRestoreCompleted = () =>
            {
                PurchaseRestored = true;
                OnPurchasingRestored?.Invoke(true, "Restore complete successfully!");
                Debug.Log("[IAPService] Restore process finished.");
            };
            _controller.FetchPurchases();
        }
        
        
        private void PurchasingProductSuccess(string productId, RecieptHandler receipt)
        {
            var product = GetProductWrapper(productId);
            var metadata = product.Metadata;
            var definition = product.Definition;
            var stringReceipt = product.TransactionData.Receipt;

            bool isRestorePurchase = _onRestoreCompleted != null;
            
            if (!isRestorePurchase)
            {
                var data = new DataEventEcommerce(
                    metadata.CurrencyCode,
                    (double) metadata.LocalizedPrice,
                    ItemType, definition.Id,
                    CartType, stringReceipt,
                    Signature, product.TransactionData.TransactionId,
                    receipt);       
                OnPurchasingProductSuccess?.Invoke(data);
            }
        }

        
        public void Dispose()
        {
            _controller.OnProductsFetched -= OnProductsFetched;
            _controller.OnProductsFetchFailed -= _errorHandler.OnProductsFetchFailed;
                
            _controller.OnPurchasePending -= _processor.OnPurchasePending;
            _controller.OnPurchaseFailed -= _errorHandler.OnPurchaseFailed;
                
            _controller.OnPurchasesFetched -= _processor.OnPurchasesFetched;
            _controller.OnPurchasesFetchFailed -= _errorHandler.OnPurchasesFetchFailed;
            
            _errorHandler.OnPurchaseFailedNotify -= InvokePurchaseFailed;
            _errorHandler.OnRestoreFailedNotify -= InvokeRestoreFailed;
        }
    }
}
