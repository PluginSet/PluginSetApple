using PluginSet.Core;

namespace PluginSet.Apple
{
    /// <summary>
    /// 已迁移至 <see cref="PluginConstants"/>，保留别名便于旧代码编译。
    /// </summary>
    public static class PluginApplePushEvents
    {
        public const string PushTokenRefresh = PluginConstants.NOTIFY_PUSH_TOKEN_REFRESH;
        public const string PushRegisterReady = PluginConstants.NOTIFY_PUSH_REGISTER_READY;
    }
}
