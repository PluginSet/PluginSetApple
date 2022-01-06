#if UNITY_IOS && ENABLE_APPLE_PURCHASE
using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using PluginSet.Core;
using UnityEngine;

namespace PluginSet.Apple
{
    public partial class PluginApple: IIAPurchasePlugin
    {
        private struct RequestingData
        {
            public string productId;
            public string extra;
            public Action<Result> callback;

            public RequestingData(string productId, string extra, Action<Result> callback)
            {
                this.productId = productId;
                this.extra = extra;
                this.callback = callback;
            }
        }

        private RequestingData? _requesting;
        
        public bool IsEnablePayment { get; private set; }
        
        public IEnumerator StartPayment(AppleDelegateListener listener)
        {
            listener.OnInitProductsSuccess += OnAppleInitProductsSuccess;
            listener.OnInitProductsFail += OnAppleInitProductsFail;
            listener.OnBuyProductSuccess += OnAppleBuyProductSuccess;
            listener.OnBuyProductFail += OnAppleBuyProductFail;

#if UNITY_IOS && !UNITY_EDITOR
            IsEnablePayment = AppleIosHelper.Plugin_Apple_Pay_Enable();
#elif UNITY_EDITOR
            IsEnablePayment = true;
#else
            IsEnablePayment = false;
#endif
            yield break;
        }

        public void Pay(string productId, Action<Result> callback = null, string jsonData = null)
        {
            Debug.Log("paywith type 33333333333333333333333333333" + Name + productId);
            Logger.Debug(Name + " call pay::::  " + productId);
            if (_requesting.HasValue)
            {
                callback?.Invoke(new Result
                {
                    Success = false,
                    Code = -1,
                    Error = "Cannot pay again when a payment has not completed",
                    Data = jsonData
                });
                Logger.Warn("Cannot pay again when a payment has not completed");
                return;
            }
            
            Logger.Debug(Name + " call pay:::: 1111111111111111111111111111111 " + productId);
            _requesting = new RequestingData(productId, jsonData, callback);
#if UNITY_IOS && !UNITY_EDITOR
            Logger.Debug(Name + " call pay:::: 2222222222222222222222222222222 " + productId);
            AppleIosHelper.Plugin_Apple_Buy_Prodcut_With_Product_Id(productId, jsonData);
#else
            callback?.Invoke(new Result
            {
                Success = false,
                Code = -2,
                PluginName = Name,
                Error = "Not support",
                Data = jsonData
            });
            Logger.Warn("Not support");
#endif
            
        }

        public void InitWithProducts(Dictionary<string, int> products)
        {
#if UNITY_IOS && !UNITY_EDITOR
            AppleIosHelper.Plugin_Apple_Pay_Init_With_Products(string.Join("#", products.Keys));
#endif
        }

        public void PaymentComplete(string transactionId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            AppleIosHelper.Plugin_Apple_Finish_Transaction(transactionId);
#endif
        }

        private void OnAppleInitProductsSuccess(Dictionary<string, AppleDelegateListener.AppleProduct> priceDict)
        {
            SendNotification(PluginConstants.IAP_ON_INIT_SUCCESS, priceDict.Values.ToList());
        }

        private void OnAppleInitProductsFail(AppleDelegateListener.ErrorResult error)
        {
            Logger.Debug("OnAppleInitProductsFail", error.message);
            SendNotification(PluginConstants.IAP_ON_INIT_FAILED, error.message);
        }

        private void OnAppleBuyProductSuccess(AppleDelegateListener.PayResult result)
        {
            Debug.Log("OnAppleBuyProductSuccess >>>>>>>>>>>>>>>>>>>> result =" + result.extra);
            if (_requesting.HasValue)
            {
                var data = _requesting.Value;
                if (data.productId == result.productId && (string.IsNullOrEmpty(result.extra) || data.extra == result.extra))
                {
                    Debug.Log("OnAppleBuyProductSuccess 00000000000000000000 callback ");
                    data.callback?.Invoke(new Result
                    {
                        Success = true,
                        Code = 0,
                        PluginName = Name,
                        Data = result.json
                    });

                    _requesting = null;
                    return;
                }
            }
            
            Debug.Log("OnAppleBuyProductSuccess 11111111111111111111 callback ");
            SendNotification(PluginConstants.IAP_CALLBACK_LOST_PAYMENTS, new Result
            {
                Success = true,
                Code = 0,
                PluginName = Name,
                Data = result.json
            });
        }

        private void OnAppleBuyProductFail(AppleDelegateListener.ErrorResult error)
        {
            Logger.Debug("OnAppleBuyProductFail", error.message);
            
            if (_requesting.HasValue)
            {
                var data = _requesting.Value;
                data.callback?.Invoke(new Result
                {
                    Success = false,
                    Code = -1,
                    Error = error.message,
                    PluginName = Name,
                    Data = data.extra
                });
                _requesting = null;
            }
        }
    }
}
#endif