using System.Collections;
using System.Collections.Generic;
using PluginSet.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace PluginSet.Apple.Editor
{
    [BuildTools]
    public static class BuildAppleTools
    {
        const string k_StorekitFramework = "StoreKit.framework";
        
        [OnSyncEditorSetting]
        public static void OnSyncEditorSetting(BuildProcessorContext context)
        {
            if (context.BuildTarget != BuildTarget.iOS)
                return;
            
            var buildParams = context.BuildChannels.Get<BuildAppleParams>();
            if (!buildParams.Enable)
                return;
            
            context.AddLinkAssembly("PluginSet.Apple");
            context.Symbols.Add("ENABLE_APPLE");
            if (buildParams.EnableLogin)
                context.Symbols.Add("ENABLE_APPLE_LOGIN");
            
            if (buildParams.EnablePurchase)
                context.Symbols.Add("ENABLE_APPLE_PURCHASE");
        }

        [iOSXCodeProjectModify]
        public static void OnIosXCodeProjectModify(BuildProcessorContext context, PBXProjectManager project)
        {
            if (context.BuildTarget != BuildTarget.iOS)
                return;
            
            var buildParams = context.BuildChannels.Get<BuildAppleParams>();
            project.AddSignInWithApple();

            if (buildParams.EnablePurchase)
            {
                project.AddFrameworkToProject(project.MainFramework, k_StorekitFramework, false);
                project.AddFrameworkToProject(project.UnityFramework, k_StorekitFramework, false);
                project.AddInAppPurchase();
            }
        }
    }
}
