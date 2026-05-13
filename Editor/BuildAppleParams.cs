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
    }
}
