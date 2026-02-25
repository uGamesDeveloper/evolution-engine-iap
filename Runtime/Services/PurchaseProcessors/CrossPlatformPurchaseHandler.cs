using System;
using System.Collections.Generic;
using LittleBit.Modules.IAppModule.Data;
using Purchase;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.MiniJSON;
using UnityEngine.Purchasing.Security;

namespace LittleBit.Modules.IAppModule.Services.PurchaseProcessors
{
    public class CrossPlatformPurchaseHandler : IPurchaseHandler
    {
        
        
        
        private readonly CrossPlatformTangles _crossPlatformTangles;

        public CrossPlatformPurchaseHandler(CrossPlatformTangles crossPlatformTangles)
        {
            _crossPlatformTangles = crossPlatformTangles;
        }
        
        
        
        private CrossPlatformValidator CreateValidator()
        {
#if DEBUG_STOREKIT_TEST
            return new CrossPlatformValidator(_crossPlatformTangles.GetGoogleData(),
                _crossPlatformTangles.GetAppleTestData(), Application.identifier);
#else
            return new CrossPlatformValidator(_crossPlatformTangles.GetGoogleData(),
                _crossPlatformTangles.GetAppleData(), Application.identifier);
#endif
        }
        
        
        

        public void ProcessPurchase(PendingOrder pendingOrder, Product product,
            Action<bool, RecieptHandler> callback)
        {
            bool validPurchase = false;
            
            TextAsset jsonFile = Resources.Load<TextAsset>("BillingMode");

            if (jsonFile == null)
            {
                Debug.LogError($"[IAP] Failed to load BillingMode from resources.");
                callback?.Invoke(false, null);
                return;
            }

            BillingModeData billingModeData = JsonUtility.FromJson<BillingModeData>(jsonFile.text);
            Debug.Log("Store is " + billingModeData.androidStore);
            
            Dictionary<string, object> wrapper = Json.Deserialize(pendingOrder.Info.Receipt) as Dictionary<string, object>;
            var receiptHandler = new RecieptHandler(wrapper);
            
            if (billingModeData.androidStore.Equals("AmazonAppStore"))
                validPurchase = true; // Amazon не требует локальной валидации через Tangles
            else
                validPurchase = ValidateWithTangles(product, pendingOrder);
            
            if (product.definition.type == ProductType.NonConsumable)
            {
                bool isFirstTime = validPurchase && PlayerPrefs.GetInt(Definitions.PurchasedPrefsPrefix + product.definition.id, 0) == 0;
                callback?.Invoke(isFirstTime, receiptHandler);
            }
            else
            {
                callback?.Invoke(validPurchase, receiptHandler);
            }

            // Записываем флаг только если покупка действительно валидна
            if (validPurchase)
            {
                PlayerPrefs.SetInt(Definitions.PurchasedPrefsPrefix + product.definition.id, 1);
            }
        }
        
        
        
        /// <summary>
        /// Выполняет проверку чека через кроссплатформенный валидатор Unity.
        /// </summary>
        private bool ValidateWithTangles(Product product, PendingOrder pendingOrder)
        {
            try
            {
                var validator = CreateValidator();
                var purchaseReceipts = validator.Validate(pendingOrder.Info.Receipt);

                foreach (var productReceipt in purchaseReceipts)
                {
                    // Логика для Google Play
                    if (productReceipt is GooglePlayReceipt google)
                    {
                        // Сравнение данных из чека с данными заказа v5
                        if (string.Equals(pendingOrder.Info.TransactionID, google.purchaseToken) &&
                            string.Equals(product.definition.storeSpecificId, google.productID))
                        {
                            // Состояние 4 означает отложенный платеж (Deferred)
                            if ((int)google.purchaseState == 4) return false;
                            return true;
                        }
                    }

                    // Логика для Apple
                    if (productReceipt is AppleInAppPurchaseReceipt apple)
                    {
                        // ИСПРАВЛЕНИЕ: В v5 мы убираем appleProductIsRestored. 
                        // Валидация считается успешной при совпадении ID продукта и ID транзакции.
                        // TransactionID берется из pendingOrder.Info
                        
                        if (string.Equals(product.definition.storeSpecificId, apple.productID) &&
                            string.Equals(pendingOrder.Info.TransactionID, apple.transactionID))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (IAPSecurityException e) // Специфичное исключение для проблем с чеком
            {
                Debug.LogError($"[IAP] Security exception: {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAP] Unknown validation exception: {e.Message}");
            }

            return false;
        }
    }
}