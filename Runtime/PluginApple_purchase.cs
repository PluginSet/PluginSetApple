#if UNITY_EDITOR || (UNITY_IOS && ENABLE_APPLE_PURCHASE)
using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using PluginSet.Core;
using UnityEngine;
using UnityEngine.Networking;

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

        private event Action<string> onTransactionCompleted = null;
        
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
            if (_requesting.HasValue)
            {
                callback?.Invoke(new Result
                {
                    Success = false,
                    Code = PluginConstants.FailDefaultCode,
                    Error = "Cannot pay again when a payment has not completed",
                    Data = jsonData
                });
                Logger.Warn("Cannot pay again when a payment has not completed");
                return;
            }
            
            _requesting = new RequestingData(productId, jsonData, callback);
#if UNITY_IOS && !UNITY_EDITOR
            AppleIosHelper.Plugin_Apple_Buy_Prodcut_With_Product_Id(productId, jsonData);
#else
            callback?.Invoke(new Result
            {
                Success = false,
                Code = PluginConstants.FailDefaultCode,
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
            onTransactionCompleted?.Invoke(transactionId);
        }

        public void AddOnPaymentCompleted(Action<string> completed)
        {
            onTransactionCompleted += completed;
        }

        public void RemoveOnPaymentCompleted(Action<string> completed)
        {
            onTransactionCompleted -= completed;
        }

        private void OnAppleInitProductsSuccess(string json, Dictionary<string, AppleDelegateListener.AppleProduct> priceDict)
        {
            SendNotification(PluginConstants.IAP_ON_INIT_SUCCESS, new Result
            {
                PluginName = Name,
                Success = true,
                Code = PluginConstants.SuccessCode,
                Data = json,
                DataObject = priceDict,
            });
        }

        private void OnAppleInitProductsFail(AppleDelegateListener.ErrorResult error)
        {
            Logger.Debug("OnAppleInitProductsFail", error.message);
            SendNotification(PluginConstants.IAP_ON_INIT_FAILED, new Result
            {
                PluginName = Name,
                Success = false,
                Code = int.Parse(error.errorCode),
                Error = error.message,
            });
        }

        private void OnAppleBuyProductSuccess(AppleDelegateListener.PayResult result)
        {
            if (_requesting.HasValue)
            {
                var data = _requesting.Value;
                if (data.productId == result.productId && (string.IsNullOrEmpty(result.extra) || data.extra == result.extra))
                {
                    if (data.callback != null)
                    {
                        try
                        {
                            data.callback.Invoke(new Result
                            {
                                Success = true,
                                Code = PluginConstants.SuccessCode,
                                PluginName = Name,
                                Data = result.json
                            });
                        }
                        catch (Exception e)
                        {
                            Logger.Error($"Buy product success call error: {e.Message}: {e}");
                        }
                    }

                    _requesting = null;
                    return;
                }
            }
            
            SendNotification(PluginConstants.IAP_CALLBACK_LOST_PAYMENTS, new Result
            {
                Success = true,
                Code = PluginConstants.SuccessCode,
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
                if (data.callback != null)
                {
                    try
                    {
                        data.callback?.Invoke(new Result
                        {
                            Success = false,
                            Code = PluginConstants.FailDefaultCode,
                            Error = error.message,
                            PluginName = Name,
                            Data = data.extra
                        });
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Buy product fail call error: {e.Message}: {e}");
                    }
                }
                _requesting = null;
            }
        }
    }
}
#endif