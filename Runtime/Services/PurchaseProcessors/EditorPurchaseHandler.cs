using System;
using System.Collections.Generic;
using Purchase;
using UnityEngine.Purchasing;

namespace LittleBit.Modules.IAppModule.Services.PurchaseProcessors
{
    public class EditorPurchaseHandler : IPurchaseHandler
    {
        // public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args, Action<bool, RecieptHandler> callback)
        // {
        //     callback?.Invoke(true, null);
        //     return PurchaseProcessingResult.Complete;
        // }

        public void ProcessPurchase(PendingOrder pendingOrder, Product product,
            Action<bool, RecieptHandler> callback)
        {
            callback?.Invoke(true, null);
        }
    }
}