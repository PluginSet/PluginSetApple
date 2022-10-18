using System.Collections;
using System.Collections.Generic;
using PluginSet.Core.Editor;
using UnityEditor;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
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
                project.Project.AddFrameworkToProject(project.MainFramework, k_StorekitFramework, false);
                project.Project.AddFrameworkToProject(project.UnityFramework, k_StorekitFramework, false);
                project.AddInAppPurchase();
            }
        }
    }
}
