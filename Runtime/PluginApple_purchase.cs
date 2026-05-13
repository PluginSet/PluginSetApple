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
            public Action<PurchaseResult> callback;

            public RequestingData(string productId, string extra, Action<PurchaseResult> callback)
            {
                this.productId = productId;
                this.extra = extra;
                this.callback = callback;
            }
        }

        private RequestingData? _requesting;

        private Action<IAPInitResult> _initProductsCallback;
        private Action<Result> _restoreProductsCallback;
        
        public bool IsEnablePayment { get; private set; }
        
        private IEnumerator StartPayment(AppleDelegateListener listener)
        {
            listener.OnInitProductsSuccess += OnAppleInitProductsSuccess;
            listener.OnInitProductsFail += OnAppleInitProductsFail;
            listener.OnBuyProductSuccess += OnAppleBuyProductSuccess;
            listener.OnBuyProductFail += OnAppleBuyProductFail;
            listener.OnRestoreTransactionFail += OnRestoreTransactionFail;

#if UNITY_IOS && !UNITY_EDITOR
            IsEnablePayment = AppleIosHelper.Plugin_Apple_Pay_Enable();
#elif UNITY_EDITOR
            IsEnablePayment = true;
#else
            IsEnablePayment = false;
#endif
            yield break;
        }

        public void Pay(string productId, Action<PurchaseResult> callback = null, string jsonData = null)
        {
            if (_requesting.HasValue)
            {
                callback?.Invoke(new PurchaseResult
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
            callback?.Invoke(new PurchaseResult()
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

        public void InitWithProducts(Dictionary<string, ProductType> products, Action<IAPInitResult> callback = null)
        {
            if (callback != null)
                _initProductsCallback += callback;
#if UNITY_IOS && !UNITY_EDITOR
            AppleIosHelper.Plugin_Apple_Pay_Init_With_Products(string.Join("#", products.Keys));
#else
            var fakeProducts = new Dictionary<string, IPaymentProduct>();
            foreach (var kv in products)
            {
                fakeProducts.Add(kv.Key, new AppleDelegateListener.AppleProduct()
                {
                    productId = kv.Key,
                    currency = "USD",
                    priceString = "$0.01",
                    title = "Fake item",
                    description = "Fake item",
                    type = kv.Value
                });
            }
            _initProductsCallback?.Invoke(new IAPInitResult()
            {
                Success = true,
                Code = PluginConstants.SuccessCode,
                PluginName = Name,
                Data = JsonUtil.ToJson(fakeProducts),
                DataObject = fakeProducts
            });
            _initProductsCallback = null;
#endif
        }

        public void PaymentComplete(string transactionId, Action<Result> callback = null)
        {
#if UNITY_IOS && !UNITY_EDITOR
            AppleIosHelper.Plugin_Apple_Finish_Transaction(transactionId);
#endif
            callback?.Invoke(new Result()
            {
                Success = true,
                Code = PluginConstants.SuccessCode,
                Data = transactionId,
                PluginName = Name,
            });
        }


        public void RestorePayments(Action<Result> callback = null, string json = null)
        {
            if (callback != null)
                _restoreProductsCallback += callback;
#if UNITY_IOS && !UNITY_EDITOR
            AppleIosHelper.Plugin_Apple_Restore_Transactions();
#else
            _restoreProductsCallback?.Invoke(new Result()
            {
                Success = false,
                Code = PluginConstants.InvalidCode,
                Error = "Invalid platform"
            });
            _restoreProductsCallback = null;
#endif
        }

        private void OnAppleInitProductsSuccess(string json, Dictionary<string, IPaymentProduct> priceDict)
        {
            Logger.Debug($"OnAppleInitProductsSuccess >>>>>>>>>>>>> {json}");
            _initProductsCallback?.Invoke( new IAPInitResult()
            {
                PluginName = Name,
                Success = true,
                Code = PluginConstants.SuccessCode,
                Data = json,
                DataObject = priceDict,
            });
            _initProductsCallback = null;
        }

        private void OnAppleInitProductsFail(AppleDelegateListener.ErrorResult error)
        {
            Logger.Debug("OnAppleInitProductsFail", error.message);
            _initProductsCallback?.Invoke( new IAPInitResult()
            {
                PluginName = Name,
                Success = false,
                Code = int.Parse(error.errorCode),
                Error = error.message,
            });
            _initProductsCallback = null;
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
                            data.callback.Invoke(new PurchaseResult()
                            {
                                Success = true,
                                Code = PluginConstants.SuccessCode,
                                PluginName = Name,
                                Data = result.json,
                                DataObject = new PurchaseCallback()
                                {
                                    transactionId = result.transactionId,
                                    productId = result.productId,
                                    callback = result.receipt,
                                }
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
            
            _restoreProductsCallback?.Invoke(new Result()
            {
                Success = true,
                Code = PluginConstants.SuccessCode,
                PluginName = Name,
            });
            _restoreProductsCallback = null;
            
            SendNotification(PluginConstants.IAP_CALLBACK_LOST_PAYMENTS, new PurchaseResult()
            {
                Success = true,
                Code = PluginConstants.SuccessCode,
                PluginName = Name,
                Data = result.json,
                DataObject = new PurchaseCallback()
                {
                    transactionId = result.transactionId,
                    productId = result.productId,
                    callback = result.receipt,
                }
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
                        data.callback?.Invoke(new PurchaseResult()
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

        private void OnRestoreTransactionFail(AppleDelegateListener.ErrorResult error)
        {
            _restoreProductsCallback?.Invoke(new Result()
            {
                Success = false,
                Code = PluginConstants.FailDefaultCode,
                Error = error.message,
                PluginName = Name,
            });
            _restoreProductsCallback = null;
        }
    }
}
#endif