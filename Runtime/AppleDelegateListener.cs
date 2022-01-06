#if UNITY_IOS
using System;
using System.Collections.Generic;
using PluginSet.Core;
using PluginSet.Core.MiniJSON;
using UnityEngine;
using Logger = PluginSet.Core.Logger;

namespace PluginSet.Apple
{
    public partial class AppleDelegateListener : MonoBehaviour
    {
        private static readonly Logger Logger = LoggerManager.GetLogger("Apple");

        public delegate void OnAppleIdLoginSuccessDelegate(Result result);

        public delegate void OnApplePasswordLoginSuccessDelegate(string user, string password);

        public delegate void OnAppleLoginFailDelegate(string error);

        public delegate void OnAppleLoginStatusChangeDelegate(string message);

        public event OnAppleIdLoginSuccessDelegate OnAppleIdLoginSuccess;
        public event OnApplePasswordLoginSuccessDelegate OnApplePasswordLoginSuccess;
        public event OnAppleLoginFailDelegate OnAppleLoginFail;
        public event OnAppleLoginStatusChangeDelegate OnAppleLoginStatusChange;

        public void OnPluginAppleLoginSuccess(string json)
        {
            var dict = Json.DeserializeObject(json);
            if (dict == null)
            {
                OnPluginAppleLoginFail("Invalid Result");
                return;
            }

            //if (dict.TryGetValue("type", out var type))
            string type = dict.GetString("type");
            if (!string.IsNullOrEmpty(type))
            {
                if ("AppleId".Equals(type))
                {
                    //throw new NotImplementedException();
                    // OnAppleIdLoginSuccess?.Invoke(new Result
                    // {
                    //     Success = true,
                    //     Token = dict["authToken"],
                    //     Extra = json,
                    //     UserInfo = new UserInfo
                    //     {
                    //         UserId = dict["userId"],
                    //         NickName = dict["userName"],
                    //     }
                    // });
                    // return;

                    OnAppleIdLoginSuccess?.Invoke(new Result
                    {
                        Success = true,
                        Data = json
                    });
                    return;
                }
                else if ("Password".Equals(type))
                {
                    OnApplePasswordLoginSuccess?.Invoke(dict.GetString("user"), dict.GetString("password"));
                    return;
                }
            }
            
            OnPluginAppleLoginFail("Invalid login type");
        }

        public void OnPluginAppleLoginFail(string error)
        {
            OnAppleLoginFail?.Invoke(error);
        }

        public void OnPluginAppleLoginStatusChange(string message)
        {
            OnAppleLoginStatusChange?.Invoke(message);
        }

        private void OnDestroy()
        {
            OnApplePasswordLoginSuccess = null;
            OnAppleLoginStatusChange = null;
            OnAppleIdLoginSuccess = null;
            OnAppleLoginFail = null;
            
#if ENABLE_APPLE_PURCHASE
            OnDestroyPayment();
#endif
        }
    }
}
#endif
