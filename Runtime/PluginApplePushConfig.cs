using UnityEngine;

namespace PluginSet.Apple
{
    public class PluginApplePushConfig : ScriptableObject
    {
        public bool EnablePush = true;
        public bool AutoClearNotifications = true;
        public string DefaultChannelId = "pluginset_push_default";
        public string DefaultChannelName = "Push Notifications";
    }
}
