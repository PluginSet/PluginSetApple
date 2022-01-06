using System.Collections;
using System.Collections.Generic;
using PluginSet.Core.Editor;
using UnityEditor;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace PluginSet.Apple.Editor
{
    [BuildTools]
    public static class BuildAppleTools
    {
        [OnSyncEditorSetting]
        public static void OnSyncEditorSetting(BuildProcessorContext context)
        {
            if (context.BuildTarget != BuildTarget.iOS)
                return;
            
            var buildParams = context.BuildChannels.Get<BuildAppleParams>("Apple");
            context.Symbols.Add("ENABLE_APPLE");
            context.AddLinkAssembly("PluginSet.Apple");
            
            if (buildParams.EnablePurchase)
                context.Symbols.Add("ENABLE_APPLE_PURCHASE");
        }

        [iOSXCodeProjectModify]
        public static void OnIosXCodeProjectModify(BuildProcessorContext context, PBXProjectManager project)
        {
            if (context.BuildTarget != BuildTarget.iOS)
                return;
            
            var buildParams = context.BuildChannels.Get<BuildAppleParams>("Apple");
            project.AddSignInWithApple();

            if (buildParams.EnablePurchase)
            {
                project.AddInAppPurchase();
            }
        }
    }
}
