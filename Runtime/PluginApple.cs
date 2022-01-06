#if UNITY_IOS
using System;
using System.Collections;
using System.Collections.Generic;
using PluginSet.Core;
using UnityEngine;
using Logger = PluginSet.Core.Logger;

namespace PluginSet.Apple
{
    [PluginRegister]
    public partial class PluginApple : PluginBase, IStartPlugin, ILoginPlugin, IStore
    {
        private static readonly Logger Logger = LoggerManager.GetLogger("Apple");
        
        public const string GameObjectName = "AppleDelegateListener";
        
        public override string Name => "Apple";

        public int StartOrder => -100000;
        
        public bool IsRunning { get; private set; }
        public bool IsEnableLogin { get; private set; }

        /// <summary>
        /// 登录成功后的数据
        /// </summary>
        private string _loginData = string.Empty;

        /// <summary>
        /// 是否登陆成功
        /// </summary>
        public bool IsLoggedIn {
            get
            {
                return !string.IsNullOrEmpty(_loginData);
            }
        }

        private readonly List<Action<Result>> _loginCallbacks = new List<Action<Result>>();

        public IEnumerator StartPlugin()
        {
            if (IsRunning)
                yield break;

            IsRunning = true;
            
            var obj = new GameObject(GameObjectName);
            DontDestroyOnLoad(obj);
            
            var listener = obj.AddComponent<AppleDelegateListener>();
            listener.OnApplePasswordLoginSuccess += OnApplePasswordLoginSuccess;
            listener.OnAppleLoginStatusChange += OnAppleLoginStatusChanged;
            listener.OnAppleIdLoginSuccess += OnAppleIdLoginSuccess;
            listener.OnAppleLoginFail += OnAppleLoginFail;
            
#if !UNITY_EDITOR
            IsEnableLogin = AppleIosHelper.Plugin_Apple_Init_With_Delegate_Name(GameObjectName);
#elif UNITY_EDITOR
            IsEnableLogin = true;
#endif

#if ENABLE_APPLE_PURCHASE
            yield return StartPayment(listener);
#endif
        }

        public void DisposePlugin(bool isAppQuit = false)
        {
        }


        public void Login(Action<Result> callback = null)
        {
            if (callback != null)
                _loginCallbacks.Add(callback);
            
#if !UNITY_EDITOR
            AppleIosHelper.Plugin_Apple_Login_With_AppleId();
#else
            OnLoginFail("Invalid platform");
#endif
        }

        public void Logout(Action<Result> callback = null)
        {
            _loginData = string.Empty;
        }

        private void OnAppleIdLoginSuccess(Result result)
        {
            OnLoginSuccess(ref result);
            _loginData = result.Data;
        }

        private void OnApplePasswordLoginSuccess(string user, string password)
        {
            OnLoginFail("Invalid login type");
        }

        private void OnAppleLoginFail(string error)
        {
            OnLoginFail(error);
        }

        private void OnAppleLoginStatusChanged(string message)
        {
            Logger.Debug("OnAppleLoginStatusChanged message: {0}", message);
            SendNotification(PluginConstants.NOTIFY_LOGIN_STATUS_CHANGED, message);
        }

        private void OnLoginResult(ref Result result)
        {
            result.PluginName = Name;
            foreach (var callback in _loginCallbacks)
            {
                callback.Invoke(result);
            }
            _loginCallbacks.Clear();
        }

        private void OnLoginSuccess(ref Result result)
        {
            result.Success = true;
            OnLoginResult(ref result);
        }

        private void OnLoginFail(string error = "")
        {
            var result = new Result
            {
                Success = false,
                Error = error
            };
            OnLoginResult(ref result);
        }

        public string GetUserInfo()
        {
            return IsLoggedIn ? _loginData : "{}";
        }
        
        public void Review()
        {
#if !UNITY_EDITOR
            AppleIosHelper.Plugin_Apple_Store_Review();
#endif
        }
    }
}
#endif
