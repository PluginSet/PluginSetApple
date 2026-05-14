#if UNITY_EDITOR || (UNITY_IOS && ENABLE_APPLE_PURCHASE)
using System;
using PluginSet.Core;
using System.Collections.Generic;
using System.Net.Sockets;
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

        [Serializable]
        public class AppleProduct : IPaymentProduct
        {
            public static AppleProduct FakeProduct(string productId, ProductType type)
            {
                return new AppleProduct()
                {
                    productId = productId,
                    currency = "USD",
                    priceString = "$0.01",
                    title = "Fake item",
                    description = "Fake item",
                    type = type,
                };
            }
            

            [SerializeField] private string productId;
            [SerializeField] private int price;
            [SerializeField] private string currency;
            [SerializeField] private string priceString;
            [SerializeField] private string title;
            [SerializeField] private string description;
            public ProductType type;
            

            public override bool AvailableToPurchase => true;
            public override string ProductId => productId;
            public override int Price => price;
            public override string Currency => currency;
            public override string PriceString => priceString;
            public override string Title => title;
            public override ProductType Type => type;
            public override string Description => description;
        }
        
        public delegate void OnAppleInitProductsSuccess(string json);
        public delegate void OnAppleInitProductsFail(ErrorResult error);
        public delegate void OnAppleBuyProductSuccess(PayResult result);
        public delegate void OnAppleBuyProductFail(ErrorResult error);
        public delegate void OnAppleBuyProductCancel(ErrorResult error);
        public delegate void OnAppleRestoreTransactionSuccess(int count);
        public delegate void OnAppleRestoreTransactionFail(ErrorResult error);

        public event OnAppleInitProductsSuccess OnInitProductsSuccess;
        public event OnAppleInitProductsFail OnInitProductsFail;
        public event OnAppleBuyProductSuccess OnBuyProductSuccess;
        public event OnAppleBuyProductFail OnBuyProductFail;
        public event OnAppleBuyProductCancel OnBuyProductCancel;
        public event OnAppleRestoreTransactionSuccess OnRestoreTransactionSuccess;
        public event OnAppleRestoreTransactionFail OnRestoreTransactionFail;

        public void OnPluginAppleInitProductsSuccess(string json)
        {
            Logger.Debug($"OnPluginAppleInitProductsSuccess >>>>>>>>>>>>> {json} callback:{OnInitProductsSuccess}");
            OnInitProductsSuccess?.Invoke(json);
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

        public void OnPluginAppleRestoreTransactionSuccess(string json)
        {
            var count = int.Parse(json);
            OnRestoreTransactionSuccess?.Invoke(count);
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
            OnRestoreTransactionSuccess = null;
            OnRestoreTransactionFail = null;
        }
    }
}
#endif