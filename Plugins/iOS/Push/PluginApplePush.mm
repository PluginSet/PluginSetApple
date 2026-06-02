#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <UserNotifications/UserNotifications.h>
#import "AppDelegateListener.h"
#import "PluginUtils.h"

static NSString* const kPushBridgeObject = @"PluginApplePushBridge";
static NSString* gApnsDeviceTokenHex = nil;

extern void _PluginSetNotificationStorePendingOpenJson(const char* json);

static void PushSendMessage(NSString* method, NSString* payload) {
    UnitySendMessage([kPushBridgeObject UTF8String], [method UTF8String], [payload UTF8String]);
}

static NSString* DeviceTokenToHex(NSData* deviceToken) {
    if (deviceToken == nil || deviceToken.length == 0) {
        return @"";
    }
    const unsigned char* dataBuffer = (const unsigned char*)deviceToken.bytes;
    NSMutableString* hex = [NSMutableString stringWithCapacity:(deviceToken.length * 2)];
    for (NSUInteger i = 0; i < deviceToken.length; ++i) {
        [hex appendFormat:@"%02x", dataBuffer[i]];
    }
    return hex;
}

static void SendApnsTokenToUnity() {
    if (gApnsDeviceTokenHex != nil && gApnsDeviceTokenHex.length > 0) {
        PushSendMessage(@"OnPushToken", gApnsDeviceTokenHex);
    }
}

@interface PluginApplePushAppDelegate : NSObject<AppDelegateListener, UNUserNotificationCenterDelegate>
@end

@implementation PluginApplePushAppDelegate

+ (void)load {
    static PluginApplePushAppDelegate* instance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        instance = [[PluginApplePushAppDelegate alloc] init];
        UnityRegisterAppDelegateListener(instance);
        [UNUserNotificationCenter currentNotificationCenter].delegate = instance;
    });
}

- (void)didFinishLaunchingWithOptions:(NSNotification*)notification {
    NSDictionary* launchOptions = notification.userInfo;
    if (launchOptions == nil) {
        return;
    }
    if (launchOptions[UIApplicationLaunchOptionsRemoteNotificationKey] == nil
        && [launchOptions[@"launchOptions"] isKindOfClass:[NSDictionary class]]) {
        launchOptions = launchOptions[@"launchOptions"];
    }
    NSDictionary* remoteNotification = launchOptions[UIApplicationLaunchOptionsRemoteNotificationKey];
    if (remoteNotification != nil) {
        [self storeOpenedNotification:remoteNotification isInitialLaunch:YES];
    }
}

- (void)didRegisterForRemoteNotificationsWithDeviceToken:(NSNotification*)notification {
    NSData* deviceToken = notification.userInfo[@"deviceToken"];
    gApnsDeviceTokenHex = DeviceTokenToHex(deviceToken);
    SendApnsTokenToUnity();
}

- (void)didFailToRegisterForRemoteNotificationsWithError:(NSNotification*)notification {
    NSError* error = notification.userInfo[@"error"];
    NSString* msg = error != nil ? error.localizedDescription : @"APNs register failed";
    PushSendMessage(@"OnPushTokenError", msg);
}

- (void)didReceiveRemoteNotification:(NSNotification*)notification {
    NSDictionary* userInfo = notification.userInfo;
    if (userInfo != nil) {
        [self sendPushMessage:userInfo];
    }
}

- (void)userNotificationCenter:(UNUserNotificationCenter*)center
       willPresentNotification:(UNNotification*)notification
         withCompletionHandler:(void (^)(UNNotificationPresentationOptions options))completionHandler {
    [self sendPushMessage:notification.request.content.userInfo];
    if (@available(iOS 14.0, *)) {
        completionHandler(UNNotificationPresentationOptionBanner | UNNotificationPresentationOptionSound);
    } else {
        completionHandler(UNNotificationPresentationOptionAlert | UNNotificationPresentationOptionSound);
    }
}

- (void)userNotificationCenter:(UNUserNotificationCenter*)center
didReceiveNotificationResponse:(UNNotificationResponse*)response
         withCompletionHandler:(void (^)(void))completionHandler {
    [self storeOpenedNotification:response.notification.request.content.userInfo isInitialLaunch:NO];
    completionHandler();
}

- (void)sendPushMessage:(NSDictionary*)userInfo {
    if (userInfo == nil) {
        return;
    }
    NSString* json = [self buildPayloadJson:userInfo isInitialLaunch:NO];
    if (json != nil) {
        PushSendMessage(@"OnPushMessage", json);
    }
}

- (void)storeOpenedNotification:(NSDictionary*)userInfo isInitialLaunch:(BOOL)isInitialLaunch {
    if (userInfo == nil) {
        return;
    }
    NSString* json = [self buildPayloadJson:userInfo isInitialLaunch:isInitialLaunch];
    if (json == nil) {
        return;
    }
    if (isInitialLaunch) {
        _PluginSetNotificationStorePendingOpenJson([json UTF8String]);
    } else {
        PushSendMessage(@"OnNotificationOpened", json);
    }
}

- (NSString*)buildPayloadJson:(NSDictionary*)userInfo isInitialLaunch:(BOOL)isInitialLaunch {
    NSString* title = userInfo[@"title"];
    NSString* body = userInfo[@"body"];
    if (title == nil) {
        NSDictionary* aps = userInfo[@"aps"];
        if ([aps isKindOfClass:[NSDictionary class]]) {
            NSDictionary* alert = aps[@"alert"];
            if ([alert isKindOfClass:[NSDictionary class]]) {
                title = alert[@"title"];
                body = alert[@"body"];
            } else if ([alert isKindOfClass:[NSString class]]) {
                body = (NSString*)alert;
            }
        }
    }
    NSString* actionId = userInfo[@"actionId"];
    NSMutableDictionary* data = [NSMutableDictionary dictionaryWithDictionary:userInfo];
    [data removeObjectForKey:@"title"];
    [data removeObjectForKey:@"body"];
    [data removeObjectForKey:@"actionId"];
    NSMutableDictionary* payload = [NSMutableDictionary dictionary];
    payload[@"title"] = title != nil ? title : @"";
    payload[@"body"] = body != nil ? body : @"";
    payload[@"actionId"] = actionId != nil ? actionId : @"";
    payload[@"data"] = data;
    payload[@"isInitialLaunch"] = @(isInitialLaunch);
    return [PluginUtils dictToJson:payload];
}

@end

#if defined(__cplusplus)
extern "C" {
#endif

void PluginApplePush_RegisterForRemoteNotifications() {
    dispatch_async(dispatch_get_main_queue(), ^{
        if (gApnsDeviceTokenHex != nil && gApnsDeviceTokenHex.length > 0) {
            SendApnsTokenToUnity();
            return;
        }
        [[UIApplication sharedApplication] registerForRemoteNotifications];
    });
}

#if defined(__cplusplus)
}
#endif
