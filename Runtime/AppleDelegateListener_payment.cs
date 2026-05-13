#if UNITY_EDITOR || (UNITY_IOS && ENABLE_APPLE_PURCHASE)
using PluginSet.Core;
using System.Collections.Generic;
using UnityEngine;

namespace PluginSet.Apple
{
    public partial class AppleDelegateListener
    {
        public struct ErrorResult
        {
            public string errorCode;
            public string message;
        }
        
        public struct PayResult
        {
            public string transactionId;
            public string productId;
            public string extra;
            public string receipt;
            public string json;
        }

        public struct RestoreTransactionResult
        {
            public PayResult[] purchasedProducts;
        }

        public class AppleProduct : IPaymentProduct
        {
            
            public string productId { set; protected get; }
            public int price { set; protected get; }
            public string currency { set; protected get; }
            public string priceString { set; protected get; }
            public string title { set; protected get; }
            public string description { set; protected get; }
            public ProductType type { set; protected get; }
            

            public override bool AvailableToPurchase => true;
            public override string ProductId => productId;
            public override int Price => price;
            public override string Currency => currency;
            public override string PriceString => priceString;
            public override string Title => title;
            public override ProductType Type => type;
            public override string Description => description;
        }
        
        public delegate void OnAppleInitProductsSuccess(string json, Dictionary<string, IPaymentProduct> priceDict);
        public delegate void OnAppleInitProductsFail(ErrorResult error);
        public delegate void OnAppleBuyProductSuccess(PayResult result);
        public delegate void OnAppleBuyProductFail(ErrorResult error);
        public delegate void OnAppleBuyProductCancel(ErrorResult error);
        public delegate void OnAppleRestoreTransactionFail(ErrorResult error);

        public event OnAppleInitProductsSuccess OnInitProductsSuccess;
        public event OnAppleInitProductsFail OnInitProductsFail;
        public event OnAppleBuyProductSuccess OnBuyProductSuccess;
        public event OnAppleBuyProductFail OnBuyProductFail;
        public event OnAppleBuyProductCancel OnBuyProductCancel;
        public event OnAppleRestoreTransactionFail OnRestoreTransactionFail;

        public void OnPluginAppleInitProductsSuccess(string json)
        {
            Logger.Debug($"OnPluginAppleInitProductsSuccess >>>>>>>>>>>>> {json} callback:{OnInitProductsSuccess}");
            Dictionary<string, AppleProduct> dict = JsonUtility.FromJson<Dictionary<string, AppleProduct>>(json);
            Dictionary<string, IPaymentProduct> callback = new Dictionary<string, IPaymentProduct>();
            foreach (var kv in dict)
            {
                callback.Add(kv.Key, (IPaymentProduct)kv.Value);
            }
            OnInitProductsSuccess?.Invoke(json, callback);
        }

        public void OnPluginAppleInitProductsFail(string json)
        {
            var error = JsonUtility.FromJson<ErrorResult>(json);
            OnInitProductsFail?.Invoke(error);
        }

        public void OnPluginAppleBuyProductSuccess(string json)
        {
            var result = JsonUtility.FromJson<PayResult>(json);
            result.json = json;
            OnBuyProductSuccess?.Invoke(result);
        }

        public void OnPluginAppleBuyProductCancel(string json)
        {
            var error = JsonUtility.FromJson<ErrorResult>(json);
            OnBuyProductCancel?.Invoke(error);
        }

        public void OnPluginAppleBuyProductFail(string json)
        {
            var error = JsonUtility.FromJson<ErrorResult>(json);
            OnBuyProductFail?.Invoke(error);
        }

        public void OnPluginAppleRestoreTransactionFail(string json)
        {
            var error = JsonUtility.FromJson<ErrorResult>(json);
            OnRestoreTransactionFail?.Invoke(error);
        }

        private void OnDestroyPayment()
        {
            OnInitProductsSuccess = null;
            OnInitProductsFail = null;
            OnBuyProductSuccess = null;
            OnBuyProductFail = null;
            OnBuyProductCancel = null;
        }
    }
}
#endif