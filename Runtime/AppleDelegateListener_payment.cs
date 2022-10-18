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

        public class AppleProduct : IPaymentProduct
        {
            
            public string productId { set; protected get; }
            public float price { set; protected get; }
            public string currency { set; protected get; }
            public string priceString { set; protected get; }
            public string title { set; protected get; }
            public string description { set; protected get; }
            

            public override bool AvailableToPurchase => true;
            public override string ProductId => productId;
            public override float Price => price;
            public override string Currency => currency;
            public override string PriceString => priceString;
            public override string Title => title;
            public override string Description => description;
        }
        
        public delegate void OnAppleInitProductsSuccess(string json, Dictionary<string, AppleProduct> priceDict);
        public delegate void OnAppleInitProductsFail(ErrorResult error);
        public delegate void OnAppleBuyProductSuccess(PayResult result);
        public delegate void OnAppleBuyProductFail(ErrorResult error);
        public delegate void OnAppleBuyProductCancel(ErrorResult error);

        public event OnAppleInitProductsSuccess OnInitProductsSuccess;
        public event OnAppleInitProductsFail OnInitProductsFail;
        public event OnAppleBuyProductSuccess OnBuyProductSuccess;
        public event OnAppleBuyProductFail OnBuyProductFail;
        public event OnAppleBuyProductCancel OnBuyProductCancel;

        public void OnPluginAppleInitProductsSuccess(string json)
        {
            Logger.Debug($"OnPluginAppleInitProductsSuccess >>>>>>>>>>>>> {json} callback:{OnInitProductsSuccess}");
            Dictionary<string, AppleProduct> dict = JsonUtility.FromJson<Dictionary<string, AppleProduct>>(json);
            OnInitProductsSuccess?.Invoke(json, dict);
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