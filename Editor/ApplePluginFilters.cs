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
            if (!enable)
            {
                Debug.Log("Filter lib file :::::::  " + s);
            }
                
            return !enable;
        }
        
        private static bool FilterPayPlugins(string s, BuildProcessorContext context)
        {
            var enable = context.BuildTarget == BuildTarget.iOS;
            var buildParams = context.BuildChannels.Get<BuildAppleParams>("Apple");
            if (!enable || !buildParams.EnablePurchase)
            {
                Debug.Log("Filter lib file :::::::  " + s);
            }
            
            return !enable || !buildParams.EnablePurchase;
        }
        
        static ApplePluginFilters()
        {
            PluginFilter.RegisterFilter("com.pluginset.apple/Plugins/iOS/Login", FilterLoginPlugins);
            PluginFilter.RegisterFilter("com.pluginset.apple/Plugins/iOS/Payment", FilterPayPlugins);
        }
    }

}
