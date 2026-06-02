#if ENABLE_PUSH_MODULE && (UNITY_EDITOR || (UNITY_IOS && ENABLE_APPLE_PUSH))
using System.Collections;
using System.Collections.Generic;
using PluginSet.Core;
using UnityEngine;
using Logger = PluginSet.Core.Logger;

namespace PluginSet.Apple
{
    public partial class PluginApple : IRemotePushNotificationPlugin, IPrivacyAuthorizationCallback, IUserSet
    {
        private static readonly Logger PushLogger = LoggerManager.GetLogger("ApplePush");

        private string _pushToken;
        private string _pushUserId;
        private bool _autoClearNotifications;
        private PluginApplePushConfig _pushConfig;

        public bool IsEnablePush => _pushConfig != null && _pushConfig.EnablePush;

        public bool HasPushToken => !string.IsNullOrEmpty(_pushToken);

        public string PushToken => _pushToken ?? string.Empty;

        public void SubscribeToTopic(string topic)
        {
            if (!IsEnablePush)
                return;

            PushLogger.Warn("SubscribeToTopic is not supported on native APNs.");
        }

        public void UnsubscribeFromTopic(string topic)
        {
            if (!IsEnablePush)
                return;

            PushLogger.Warn("UnsubscribeFromTopic is not supported on native APNs.");
        }

        private void InitPush(PluginSetConfig config)
        {
            _pushConfig = config.Get<PluginApplePushConfig>();
            if (_pushConfig == null)
                _pushConfig = ScriptableObject.CreateInstance<PluginApplePushConfig>();
            _autoClearNotifications = _pushConfig.AutoClearNotifications;
        }

        public void OnPrivacyAuthorization()
        {
            if (!IsEnablePush)
                return;

            ApplePushBridge.EnsureCreated(this);
            StartCoroutine(StartApplePush());
        }

        public void SetUserInfo(bool isNewUser, string userId, Dictionary<string, object> pairs = null)
        {
            if (!IsEnablePush)
                return;

            _pushUserId = userId;
            RegisterPushWithServer();
        }

        public void ClearUserInfo()
        {
            UnregisterPushWithServer();
            _pushUserId = null;
        }

        public void FlushUserInfo()
        {
        }

        internal void OnPushApplicationPause(bool pauseStatus)
        {
            if (!IsEnablePush || pauseStatus || !_autoClearNotifications)
                return;

            Devices.RemoveAllDeliveredNotifications();
        }

        internal void HandlePushToken(string token)
        {
            if (!IsEnablePush || string.IsNullOrEmpty(token))
                return;

            _pushToken = token;
            SendNotification(PluginConstants.NOTIFY_PUSH_TOKEN_REFRESH,
                PushTokenRefreshEventData.Create(Name, token, _pushUserId));
            OnPushTokenRefresh();
        }

        internal void HandlePushTokenError(string message)
        {
            PushLogger.Warn($"Push token error: {message}");
        }

        internal void OnPushMessageReceived(PushMessage message)
        {
            SendNotification(PluginConstants.NOTIFY_ONRECEIVED_PUSH_MESSAGE, message);
        }

        internal void OnNotificationOpened(NotificationResponse response)
        {
            SendNotification(PluginConstants.NOTIFY_NOTIFICATION_OPENED, response);
        }

        private IEnumerator StartApplePush()
        {
            yield return null;
            Devices.RequestNotificationPermissions();
            yield return null;
            ApplePushNativeApi.FetchToken();
            yield return null;

            var initial = Devices.GetInitialNotification();
            if (initial != null)
                OnNotificationOpened(initial);

            if (!string.IsNullOrEmpty(_pushUserId) && HasPushToken)
                RegisterPushWithServer();
        }

        private void OnPushTokenRefresh()
        {
            if (string.IsNullOrEmpty(_pushUserId) || !HasPushToken)
                return;

            RegisterPushWithServer();
        }

        private void RegisterPushWithServer()
        {
            if (string.IsNullOrEmpty(_pushUserId) || !HasPushToken)
                return;

            SendNotification(PluginConstants.NOTIFY_PUSH_REGISTER_READY,
                PushRegisterEventData.CreateRegister(Name, _pushUserId, _pushToken));
        }

        private void UnregisterPushWithServer()
        {
            SendNotification(PluginConstants.NOTIFY_PUSH_REGISTER_READY,
                PushRegisterEventData.CreateUnregister(Name));
        }
    }
}
#endif
