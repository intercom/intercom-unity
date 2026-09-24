#import <UIKit/UIKit.h>
#import <UserNotifications/UserNotifications.h>
#import <Intercom/Intercom.h>
#import "UnityAppController.h"

// A UnityAppController subclass that wires up the two pieces a stock Unity app is missing for
// Intercom push on iOS:
//
//   1. It implements application:didRegisterForRemoteNotificationsWithDeviceToken: and forwards the
//      APNs token to Intercom. Without this, the token is never delivered to the SDK and Intercom's
//      push-handling swizzle (which only activates once the token is set) never engages.
//
//   2. It installs a UNUserNotificationCenter delegate (only if nothing else already owns it, e.g.
//      the Unity Mobile Notifications package), so the SDK has a delegate to swizzle for the modern
//      tap/foreground notification path.
//
// IMPL_APP_CONTROLLER_SUBCLASS makes Unity launch with this controller. Only one UnityAppController
// subclass can win, so a project that ships its own subclass should instead forward the token via
// IntercomClient.SetDeviceToken (see Documentation~/push-notifications.md).
@interface IntercomAppController : UnityAppController <UNUserNotificationCenterDelegate>
@end

@implementation IntercomAppController

- (BOOL)application:(UIApplication*)application didFinishLaunchingWithOptions:(NSDictionary*)launchOptions {
    BOOL result = [super application:application didFinishLaunchingWithOptions:launchOptions];

    UNUserNotificationCenter* center = [UNUserNotificationCenter currentNotificationCenter];
    if (center.delegate == nil) {
        center.delegate = self;
    }

    return result;
}

- (void)application:(UIApplication*)application
    didRegisterForRemoteNotificationsWithDeviceToken:(NSData*)deviceToken {
    // Forward to super only if UnityAppController itself implements this (guards against calling an
    // unimplemented optional delegate method on Unity versions that no longer provide it).
    if ([UnityAppController instancesRespondToSelector:_cmd]) {
        [super application:application didRegisterForRemoteNotificationsWithDeviceToken:deviceToken];
    }

    [Intercom setDeviceToken:deviceToken
                     success:^{
                       NSLog(@"[Intercom] Device token registered with Intercom");
                     }
                     failure:^(NSError* error) {
                       NSLog(@"[Intercom] Failed to register device token: %@", error);
                     }];
}

- (void)application:(UIApplication*)application
    didFailToRegisterForRemoteNotificationsWithError:(NSError*)error {
    if ([UnityAppController instancesRespondToSelector:_cmd]) {
        [super application:application didFailToRegisterForRemoteNotificationsWithError:error];
    }

    NSLog(@"[Intercom] Failed to register for remote notifications: %@", error);
}

#pragma mark - UNUserNotificationCenterDelegate

// Minimal delegate implementations so the Intercom SDK has methods to swizzle. Intercom intercepts
// its own notifications inside the swizzled implementations; anything that isn't an Intercom push
// is presented/dismissed normally here.
- (void)userNotificationCenter:(UNUserNotificationCenter*)center
       willPresentNotification:(UNNotification*)notification
         withCompletionHandler:(void (^)(UNNotificationPresentationOptions))completionHandler {
    if (@available(iOS 14.0, *)) {
        completionHandler(UNNotificationPresentationOptionBanner | UNNotificationPresentationOptionSound | UNNotificationPresentationOptionBadge);
    } else {
        completionHandler(UNNotificationPresentationOptionAlert | UNNotificationPresentationOptionSound | UNNotificationPresentationOptionBadge);
    }
}

- (void)userNotificationCenter:(UNUserNotificationCenter*)center
    didReceiveNotificationResponse:(UNNotificationResponse*)response
             withCompletionHandler:(void (^)(void))completionHandler {
    completionHandler();
}

@end

IMPL_APP_CONTROLLER_SUBCLASS(IntercomAppController)
