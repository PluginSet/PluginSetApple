using PluginSet.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace PluginSet.Apple.Editor
{
    [InitializeOnLoad]
    public static class ApplePluginFilters
    {
        private static bool FilterLoginPlugins(string s, BuildProcessorContext context)
        {
            var enable = context.BuildTarget == BuildTarget.iOS;
            var buildParams = context.BuildChannels.Get<BuildAppleParams>();
            enable = enable && buildParams.Enable && buildParams.EnableLogin;
            if (!enable)
            {
                Debug.Log("Filter lib file :::::::  " + s);
            }

            return !enable;
        }
        
        private static bool FilterPayPlugins(string s, BuildProcessorContext context)
        {
            var enable = context.BuildTarget == BuildTarget.iOS;
            var buildParams = context.BuildChannels.Get<BuildAppleParams>();
            enable = enable && buildParams.Enable && buildParams.EnablePurchase;
            if (!enable)
            {
                Debug.Log("Filter lib file :::::::  " + s);
            }

            return !enable;
        }
        
        private static bool FilterPushPlugins(string s, BuildProcessorContext context)
        {
            var enable = context.BuildTarget == BuildTarget.iOS;
            var buildParams = context.BuildChannels.Get<BuildAppleParams>();
            enable = enable && context.BuildChannels.IncludePushModule && buildParams.Enable && buildParams.EnablePush;
            if (!enable)
            {
                Debug.Log("Filter lib file :::::::  " + s);
            }

            return !enable;
        }

        static ApplePluginFilters()
        {
            PluginFilter.RegisterFilter("com.pluginset.apple/Plugins/iOS/Login", FilterLoginPlugins);
            PluginFilter.RegisterFilter("com.pluginset.apple/Plugins/iOS/Payment", FilterPayPlugins);
            PluginFilter.RegisterFilter("com.pluginset.apple/Plugins/iOS/Push", FilterPushPlugins);
        }
    }

}
