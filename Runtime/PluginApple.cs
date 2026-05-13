#if UNITY_EDITOR || (UNITY_IOS && ENABLE_APPLE)
using System;
using System.Collections;
using System.Collections.Generic;
using PluginSet.Core;
using UnityEngine;
using Logger = PluginSet.Core.Logger;

namespace PluginSet.Apple
{
    [PluginRegister]
    public partial class PluginApple : PluginBase, IStartPlugin, IStore
    {
        private static readonly Logger Logger = LoggerManager.GetLogger("Apple");
        
        private const string GameObjectName = "AppleDelegateListener";
        
        public override string Name => "Apple";

        public int StartOrder => PluginsStartOrder.SdkDefault;
        
        public bool IsRunning { get; private set; }

        public IEnumerator StartPlugin()
        {
            if (IsRunning)
                yield break;

            IsRunning = true;
            
#if UNITY_EDITOR || (ENABLE_APPLE_LOGIN || ENABLE_APPLE_PURCHASE)
            var obj = new GameObject(GameObjectName);
            DontDestroyOnLoad(obj);
            var listener = obj.AddComponent<AppleDelegateListener>();
            
#if UNITY_EDITOR || ENABLE_APPLE_LOGIN
            yield return StartLogin(listener);
#endif
#if UNITY_EDITOR || ENABLE_APPLE_PURCHASE
            yield return StartPayment(listener);
#endif
#endif
        }

        public void DisposePlugin(bool isAppQuit = false)
        {
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
