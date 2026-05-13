#if UNITY_EDITOR || (UNITY_IOS && ENABLE_APPLE_LOGIN)
using System;
using System.Collections;
using System.Collections.Generic;
using PluginSet.Core;
using UnityEngine;
using Logger = PluginSet.Core.Logger;

namespace PluginSet.Apple
{
    public partial class PluginApple : ILoginPlugin
    {
        public bool IsEnableLogin { get; private set; }

        /// <summary>
        /// 登录成功后的数据
        /// </summary>
        private string _loginData = string.Empty;

        /// <summary>
        /// 是否登陆成功
        /// </summary>
        public bool IsLoggedIn => !string.IsNullOrEmpty(_loginData);

        private readonly List<Action<Result>> _loginCallbacks = new List<Action<Result>>();

        private IEnumerator StartLogin(AppleDelegateListener listener)
        {
            listener.OnApplePasswordLoginSuccess += OnApplePasswordLoginSuccess;
            listener.OnAppleLoginStatusChange += OnAppleLoginStatusChanged;
            listener.OnAppleIdLoginSuccess += OnAppleIdLoginSuccess;
            listener.OnAppleLoginFail += OnAppleLoginFail;
            listener.OnAppleLoginCancel += OnAppleLoginCancel;
            
#if !UNITY_EDITOR
            IsEnableLogin = AppleIosHelper.Plugin_Apple_IsEnableLogin();
#elif UNITY_EDITOR
            IsEnableLogin = true;
#endif
            yield break;
        }

        /// <summary>
        /// </summary>
        /// <param name="callback">
        /// 回调玩家数据：
        /// type string AppleId
        /// userId string
        /// userName string
        /// idToken string
        /// authToken string
        /// status number
        /// </param>
        public void Login(Action<Result> callback = null, string _ = null)
        {
            if (callback != null)
                _loginCallbacks.Add(callback);
            
#if !UNITY_EDITOR
            AppleIosHelper.Plugin_Apple_Login_With_AppleId();
#else
            OnLoginFail(PluginConstants.FailDefaultCode, "Invalid platform");
#endif
        }

        public void Logout(Action<Result> callback = null, string _ = null)
        {
            _loginData = string.Empty;
        }

        private void OnAppleIdLoginSuccess(Result result)
        {
            _loginData = result.Data;
            OnLoginSuccess(ref result);
        }

        private void OnApplePasswordLoginSuccess(string user, string password)
        {
            OnLoginFail(PluginConstants.FailDefaultCode, "Invalid login type");
        }

        private void OnAppleLoginCancel(string error)
        {
            OnLoginFail(PluginConstants.CancelCode, error);
        }

        private void OnAppleLoginFail(string error)
        {
            OnLoginFail(PluginConstants.FailDefaultCode, error);
        }

        private void OnAppleLoginStatusChanged(string message)
        {
            _loginData = null;
            Logger.Debug("OnAppleLoginStatusChanged message: {0}", message);
            SendNotification(PluginConstants.NOTIFY_LOGIN_STATUS_CHANGED, message);
        }

        private void OnLoginResult(ref Result result)
        {
            result.PluginName = Name;
            result.Code = PluginConstants.SuccessCode;
            
            foreach (var callback in _loginCallbacks)
            {
                try
                {
                    callback.Invoke(result);
                }
                catch (Exception e)
                {
                    Logger.Error($"Call login result error:{e.Message}:{e}");
                }
            }
            _loginCallbacks.Clear();
        }

        private void OnLoginSuccess(ref Result result)
        {
            result.Success = true;
            OnLoginResult(ref result);
        }

        private void OnLoginFail(int code, string error = "")
        {
            var result = new Result
            {
                Success = false,
                PluginName = Name,
                Code = code,
                Error = error
            };
            OnLoginResult(ref result);
        }

        public string GetUserLoginData()
        {
            return _loginData;
        }
    }
}
#endif
