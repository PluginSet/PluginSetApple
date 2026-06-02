#if ENABLE_PUSH_MODULE && (UNITY_EDITOR || (UNITY_IOS && ENABLE_APPLE_PUSH))
using System.Runtime.InteropServices;

namespace PluginSet.Apple
{
    internal static class ApplePushNativeApi
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PluginApplePush_RegisterForRemoteNotifications();
#endif

        public static void FetchToken()
        {
#if UNITY_IOS && !UNITY_EDITOR
            PluginApplePush_RegisterForRemoteNotifications();
#endif
        }
    }
}
#endif
