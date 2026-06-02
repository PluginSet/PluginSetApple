using System.Collections;
using System.Collections.Generic;
using PluginSet.Core;
using PluginSet.Core.Editor;
using UnityEngine;

namespace PluginSet.Apple.Editor
{
    [BuildChannelsParams("Apple", "苹果登录插件参数")]
    [VisibleCaseBoolValue("SupportIOS", true)]
    public class BuildAppleParams : ScriptableObject
    {
        [Tooltip("是否启用苹果插件")]
        public bool Enable;

        [Tooltip("是否启用苹果登录")]
        public bool EnableLogin;
        
        [Tooltip("是否启用苹果支付")]
        public bool EnablePurchase;

        [Tooltip("是否启用 iOS 原生 APNs 推送（需在 Apple 开发者后台配置 Push 证书/Key）")]
        public bool EnablePush;

        [Tooltip("回到前台时自动清除通知中心已送达通知")]
        public bool AutoClearNotifications = true;
    }
}
