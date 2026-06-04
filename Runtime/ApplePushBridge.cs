#if ENABLE_PUSH_MODULE && (UNITY_IOS && ENABLE_APPLE_PUSH)
using PluginSet.Core;
using PluginSet.Core.MiniJSON;
using UnityEngine;
using Logger = PluginSet.Core.Logger;

namespace PluginSet.Apple
{
    public class ApplePushBridge : MonoBehaviour
    {
        public const string GameObjectName = "PluginApplePushBridge";

        private static ApplePushBridge _instance;
        private static PluginApple _plugin;

        private static readonly Logger Logger = LoggerManager.GetLogger("ApplePush");

        public static void EnsureCreated(PluginApple plugin)
        {
            _plugin = plugin;
            if (_instance != null)
                return;

            var go = new GameObject(GameObjectName);
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ApplePushBridge>();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            _plugin?.OnPushApplicationPause(pauseStatus);
        }

        public void OnPushToken(string token)
        {
            Logger.Debug($"OnPushToken: {token}");
            _plugin?.HandlePushToken(token);
        }

        public void OnPushTokenError(string message)
        {
            Logger.Warn($"OnPushTokenError: {message}");
            _plugin?.HandlePushTokenError(message);
        }

        public void OnPushMessage(string json)
        {
            Logger.Debug($"OnPushMessage: {json}");
            var message = ParsePushMessage(json);
            if (message != null)
                _plugin?.OnPushMessageReceived(message);
        }

        public void OnNotificationOpened(string json)
        {
            Logger.Debug($"OnNotificationOpened: {json}");
            var response = ParseNotificationResponse(json);
            if (response != null)
                _plugin?.OnNotificationOpened(response);
        }

        private static PushMessage ParsePushMessage(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            var root = Json.DeserializeObject(json);
            if (root.IsNull)
                return null;

            return new PushMessage
            {
                Title = root.GetString("title"),
                Body = root.GetString("body"),
                Data = ExtractDataDictionary(json),
            };
        }

        private static NotificationResponse ParseNotificationResponse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            var root = Json.DeserializeObject(json);
            if (root.IsNull)
                return null;

            return new NotificationResponse
            {
                Message = new PushMessage
                {
                    Title = root.GetString("title"),
                    Body = root.GetString("body"),
                    Data = ExtractDataDictionary(json),
                },
                ActionId = root.GetString("actionId"),
                IsInitialLaunch = ParseIsInitialLaunch(json),
            };
        }

        private static bool ParseIsInitialLaunch(string json)
        {
            var raw = Json.Deserialize(json) as System.Collections.Generic.Dictionary<string, object>;
            if (raw == null || !raw.TryGetValue("isInitialLaunch", out var value))
                return false;
            if (value is bool b)
                return b;
            return bool.TryParse(value.ToString(), out var parsed) && parsed;
        }

        private static System.Collections.Generic.Dictionary<string, object> ExtractDataDictionary(string json)
        {
            var raw = Json.Deserialize(json) as System.Collections.Generic.Dictionary<string, object>;
            if (raw == null || !raw.TryGetValue("data", out var dataObj))
                return new System.Collections.Generic.Dictionary<string, object>();

            if (dataObj is System.Collections.Generic.Dictionary<string, object> dict)
                return new System.Collections.Generic.Dictionary<string, object>(dict);

            return new System.Collections.Generic.Dictionary<string, object>();
        }
    }
}
#endif
