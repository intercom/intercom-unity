#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <UserNotifications/UserNotifications.h>
#import <Intercom/ICMUserAttributes.h>
#import <Intercom/Intercom.h>
#import <Intercom/IntercomContent.h>

// Unity callback function declaration
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

// +setUnityVersion: is declared internally in the native SDK (not in the public Intercom.h),
// mirroring how the React Native / Cordova / Flutter wrapper setters are exposed. Re-declare it
// so this bridge can call it.
@interface Intercom (Unity)
+ (void)setUnityVersion:(NSString*)unityVersion;
@end

// Marker class the native SDK detects via NSClassFromString(@"IntercomUnity") in fetchSDKType to
// classify the wrapper as Unity — the peer of React Native's IntercomModule and Cordova's
// IntercomBridge. Its mere presence in the app is the signal; it has no other behaviour.
@interface IntercomUnity : NSObject
@end

@implementation IntercomUnity
@end

static id ICMFirstValueForKeys(NSDictionary* dict, NSArray<NSString*>* keys) {
    for (NSString* key in keys) {
        id value = dict[key];
        if (value != nil) {
            return value;
        }
    }
    return nil;
}

static NSDate* ICMDateFromUnixSeconds(id value) {
    if (![value isKindOfClass:[NSNumber class]]) {
        return nil;
    }
    return [NSDate dateWithTimeIntervalSince1970:[(NSNumber*)value doubleValue]];
}

static ICMCompany* ICMCompanyFromJsonDictionary(NSDictionary* dict) {
    if (![dict isKindOfClass:[NSDictionary class]]) {
        return nil;
    }

    ICMCompany* company = [ICMCompany new];

    id companyId = ICMFirstValueForKeys(dict, @[ @"companyId", @"company_id" ]);
    if ([companyId isKindOfClass:[NSString class]]) {
        company.companyId = companyId;
    }

    id name = dict[@"name"];
    if ([name isKindOfClass:[NSString class]]) {
        company.name = name;
    }

    NSDate* createdAt = ICMDateFromUnixSeconds(ICMFirstValueForKeys(dict, @[ @"createdAt", @"created_at" ]));
    if (createdAt != nil) {
        company.createdAt = createdAt;
    }

    id monthlySpend = ICMFirstValueForKeys(dict, @[ @"monthlySpend", @"monthly_spend" ]);
    if ([monthlySpend isKindOfClass:[NSNumber class]]) {
        company.monthlySpend = monthlySpend;
    }

    id plan = dict[@"plan"];
    if ([plan isKindOfClass:[NSString class]]) {
        company.plan = plan;
    }

    id customAttributes = ICMFirstValueForKeys(dict, @[ @"customAttributes", @"custom_attributes" ]);
    if ([customAttributes isKindOfClass:[NSDictionary class]]) {
        company.customAttributes = customAttributes;
    }

    return company;
}

// Splits a JSON dictionary into Intercom's standard user attributes and everything else, which
// falls through to customAttributes unchanged (matching the wrapper's existing behaviour).
static ICMUserAttributes* ICMUserAttributesFromJsonDictionary(NSDictionary* dict) {
    ICMUserAttributes* attributes = [ICMUserAttributes new];
    if (![dict isKindOfClass:[NSDictionary class]]) {
        return attributes;
    }

    NSSet<NSString*>* standardKeys = [NSSet setWithArray:@[
        @"name", @"email", @"phone",
        @"userId", @"user_id",
        @"languageOverride", @"language_override",
        @"signedUpAt", @"signed_up_at",
        @"unsubscribedFromEmails", @"unsubscribed_from_emails",
        @"companies", @"customAttributes", @"custom_attributes"
    ]];

    id name = dict[@"name"];
    if ([name isKindOfClass:[NSString class]]) {
        attributes.name = name;
    }

    id email = dict[@"email"];
    if ([email isKindOfClass:[NSString class]]) {
        attributes.email = email;
    }

    id phone = dict[@"phone"];
    if ([phone isKindOfClass:[NSString class]]) {
        attributes.phone = phone;
    }

    id userId = ICMFirstValueForKeys(dict, @[ @"userId", @"user_id" ]);
    if ([userId isKindOfClass:[NSString class]]) {
        attributes.userId = userId;
    }

    id languageOverride = ICMFirstValueForKeys(dict, @[ @"languageOverride", @"language_override" ]);
    if ([languageOverride isKindOfClass:[NSString class]]) {
        attributes.languageOverride = languageOverride;
    }

    NSDate* signedUpAt = ICMDateFromUnixSeconds(ICMFirstValueForKeys(dict, @[ @"signedUpAt", @"signed_up_at" ]));
    if (signedUpAt != nil) {
        attributes.signedUpAt = signedUpAt;
    }

    id unsubscribed = ICMFirstValueForKeys(dict, @[ @"unsubscribedFromEmails", @"unsubscribed_from_emails" ]);
    if ([unsubscribed isKindOfClass:[NSNumber class]]) {
        attributes.unsubscribedFromEmails = [(NSNumber*)unsubscribed boolValue];
    }

    id companiesValue = dict[@"companies"];
    if ([companiesValue isKindOfClass:[NSArray class]]) {
        NSMutableArray<ICMCompany*>* companies = [NSMutableArray array];
        for (id companyDict in (NSArray*)companiesValue) {
            ICMCompany* company = ICMCompanyFromJsonDictionary(companyDict);
            if (company != nil) {
                [companies addObject:company];
            }
        }
        attributes.companies = companies;
    }

    id customAttributesValue = ICMFirstValueForKeys(dict, @[ @"customAttributes", @"custom_attributes" ]);
    NSMutableDictionary* customAttributes = [NSMutableDictionary dictionary];
    if ([customAttributesValue isKindOfClass:[NSDictionary class]]) {
        [customAttributes addEntriesFromDictionary:customAttributesValue];
    }

    for (NSString* key in dict) {
        if ([standardKeys containsObject:key]) {
            continue;
        }
        id value = dict[key];
        if ([value isKindOfClass:[NSString class]] || [value isKindOfClass:[NSNumber class]]) {
            customAttributes[key] = value;
        }
    }
    attributes.customAttributes = customAttributes;

    return attributes;
}

extern "C" {
void _Initialize(const char* apiKey, const char* appId) {
    NSString* nsApiKey = [NSString stringWithUTF8String:apiKey];
    NSString* nsAppId = [NSString stringWithUTF8String:appId];

    [Intercom setApiKey:nsApiKey forAppId:nsAppId];
}

void _SetUnityVersion(const char* version) {
    // Guarded because +setUnityVersion: is internal to the SDK: an app that resolves an older
    // Intercom iOS SDK would otherwise raise unrecognized selector at init.
    if ([Intercom respondsToSelector:@selector(setUnityVersion:)]) {
        [Intercom setUnityVersion:[NSString stringWithUTF8String:version]];
    } else {
        NSLog(@"[Intercom] setUnityVersion: unavailable in the linked native SDK; skipping");
    }
}

void _LoginIdentifiedUser(const char* userId, const char* email, const char* attributesJson) {
    NSDictionary* attributesDict = nil;
    if (attributesJson != NULL) {
        NSString* nsAttributesJson = [NSString stringWithUTF8String:attributesJson];
        NSData* jsonData = [nsAttributesJson dataUsingEncoding:NSUTF8StringEncoding];
        if (jsonData != nil) {
            attributesDict = [NSJSONSerialization JSONObjectWithData:jsonData options:0 error:nil];
        }
    }

    ICMUserAttributes* attributes = ICMUserAttributesFromJsonDictionary(attributesDict);

    // C# Registration defaults these to "", which marshals to a non-NULL empty C string. Assigning
    // an empty userId makes the SDK reject the login, so length must be checked, not just NULL.
    if (userId != NULL && userId[0] != '\0') {
        attributes.userId = [NSString stringWithUTF8String:userId];
    }

    if (email != NULL && email[0] != '\0') {
        attributes.email = [NSString stringWithUTF8String:email];
    }

    [Intercom loginUserWithUserAttributes:attributes
                                  success:^{
                                    NSLog(@"[Intercom] User login successful");
                                    UnitySendMessage("IntercomManager", "HandleRegistrationSuccess", "");
                                  }
                                  failure:^(NSError* _Nonnull error) {
                                    NSLog(@"[Intercom] Failed to login identified user: %@", error);
                                    UnitySendMessage("IntercomManager", "HandleRegistrationFailure", [error.localizedDescription UTF8String]);
                                  }];
}

void _LoginUnidentifiedUser() {
    [Intercom loginUnidentifiedUserWithSuccess:^{
        NSLog(@"[Intercom] Unidentified user login successful");
        UnitySendMessage("IntercomManager", "HandleRegistrationSuccess", "");
    }
                                       failure:^(NSError* _Nonnull error) {
                                         NSLog(@"[Intercom] Failed to login unidentified user: %@", error);
                                         UnitySendMessage("IntercomManager", "HandleRegistrationFailure", [error.localizedDescription UTF8String]);
                                       }];
}

void _Logout() {
    [Intercom logout];
}

void _Present(const char* space) {
    NSString* nsSpace = [NSString stringWithUTF8String:space];

    if ([nsSpace caseInsensitiveCompare:@"home"] == NSOrderedSame) {
        [Intercom presentIntercom:home];
    } else if ([nsSpace caseInsensitiveCompare:@"messages"] == NSOrderedSame) {
        [Intercom presentIntercom:messages];
    } else if ([nsSpace caseInsensitiveCompare:@"helpcenter"] == NSOrderedSame) {
        [Intercom presentIntercom:helpCenter];
    } else if ([nsSpace caseInsensitiveCompare:@"tickets"] == NSOrderedSame) {
        [Intercom presentIntercom:tickets];
    } else {
        NSLog(@"[Intercom] Unknown space: %@", nsSpace);
    }
}

void _PresentContent(const char* contentType, const char* contentId) {
    NSString* nsContentType = [NSString stringWithUTF8String:contentType];
    NSString* nsContentId = [NSString stringWithUTF8String:contentId];

    if ([nsContentType caseInsensitiveCompare:@"article"] == NSOrderedSame) {
        [Intercom presentContent:[IntercomContent articleWithId:nsContentId]];
    } else if ([nsContentType caseInsensitiveCompare:@"carousel"] == NSOrderedSame) {
        [Intercom presentContent:[IntercomContent carouselWithId:nsContentId]];
    } else if ([nsContentType caseInsensitiveCompare:@"survey"] == NSOrderedSame) {
        [Intercom presentContent:[IntercomContent surveyWithId:nsContentId]];
    } else if ([nsContentType caseInsensitiveCompare:@"conversation"] == NSOrderedSame) {
        [Intercom presentContent:[IntercomContent conversationWithId:nsContentId]];
    } else if ([nsContentType caseInsensitiveCompare:@"ticket"] == NSOrderedSame) {
        [Intercom presentContent:[IntercomContent ticketWithId:nsContentId]];
    } else if ([nsContentType caseInsensitiveCompare:@"helpcentercollections"] == NSOrderedSame) {
        // Parse collection IDs - supports both single ID and comma-separated multiple IDs
        NSArray<NSString*>* collectionIds = [nsContentId componentsSeparatedByString:@","];

        // Trim whitespace from each ID
        NSMutableArray<NSString*>* trimmedIds = [[NSMutableArray alloc] init];
        for (NSString* collectionId in collectionIds) {
            NSString* trimmedId = [collectionId stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
            if (trimmedId.length > 0) {
                [trimmedIds addObject:trimmedId];
            }
        }

        if (trimmedIds.count > 0) {
            [Intercom presentContent:[IntercomContent helpCenterCollectionsWithIds:trimmedIds]];
        } else {
            NSLog(@"[Intercom] No valid collection IDs found in: %@", nsContentId);
        }
    } else {
        NSLog(@"[Intercom] Unknown content type: %@", nsContentType);
    }
}

void _DisplayMessageComposer(const char* initialMessage) {
    NSString* nsInitialMessage = initialMessage != NULL ? [NSString stringWithUTF8String:initialMessage] : @"";
    [Intercom presentMessageComposer:nsInitialMessage];
}

void _SetBottomPadding(int bottomPadding) {
    [Intercom setBottomPadding:bottomPadding];
}

void _SetInAppMessageVisibility(const char* visibility) {
    NSString* nsVisibility = [NSString stringWithUTF8String:visibility];
    BOOL isVisible = [nsVisibility caseInsensitiveCompare:@"visible"] == NSOrderedSame;
    [Intercom setInAppMessagesVisible:isVisible];
}

void _SetLauncherVisibility(const char* visibility) {
    NSString* nsVisibility = [NSString stringWithUTF8String:visibility];
    BOOL isVisible = [nsVisibility caseInsensitiveCompare:@"visible"] == NSOrderedSame;
    [Intercom setLauncherVisible:isVisible];
}

void _HideIntercom() {
    [Intercom hideIntercom];
}

void _SetUserAttributes(const char* attributesJson) {
    NSString* nsAttributesJson = [NSString stringWithUTF8String:attributesJson];
    NSData* jsonData = [nsAttributesJson dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary* attributes = [NSJSONSerialization JSONObjectWithData:jsonData options:0 error:nil];

    ICMUserAttributes* userAttributes = ICMUserAttributesFromJsonDictionary(attributes);

    [Intercom updateUser:userAttributes
                 success:^{
                   NSLog(@"[Intercom] User attributes updated successfully");
                   UnitySendMessage("IntercomManager", "HandleRegistrationSuccess", "");
                 }
                 failure:^(NSError* _Nonnull error) {
                   NSLog(@"[Intercom] Failed to update user: %@", error);
                   UnitySendMessage("IntercomManager", "HandleRegistrationFailure", [error.localizedDescription UTF8String]);
                 }];
}

void _LogEvent(const char* name) {
    NSString* nsName = [NSString stringWithUTF8String:name];
    [Intercom logEventWithName:nsName];
}

void _LogEventWithMetadata(const char* name, const char* metadataJson) {
    NSString* nsName = [NSString stringWithUTF8String:name];
    NSString* nsMetadataJson = [NSString stringWithUTF8String:metadataJson];
    NSData* jsonData = [nsMetadataJson dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary* metadata = [NSJSONSerialization JSONObjectWithData:jsonData options:0 error:nil];

    [Intercom logEventWithName:nsName metaData:metadata];
}

void _SetUserHash(const char* userHash) {
    NSString* nsUserHash = [NSString stringWithUTF8String:userHash];
    [Intercom setUserHash:nsUserHash];
}

void _SetUserJwt(const char* jwt) {
    NSString* nsJwt = [NSString stringWithUTF8String:jwt];
    [Intercom setUserJwt:nsJwt];
}

void _SetAuthTokens(const char* authTokensJson) {
    NSString* nsAuthTokensJson = [NSString stringWithUTF8String:authTokensJson];
    NSData* jsonData = [nsAuthTokensJson dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary* authTokens = [NSJSONSerialization JSONObjectWithData:jsonData options:0 error:nil];

    NSString* userHash = authTokens[@"userHash"];
    NSString* jwt = authTokens[@"jwt"];

    if (userHash) {
        [Intercom setUserHash:userHash];
    }
    if (jwt) {
        [Intercom setUserJwt:jwt];
    }
}

void _SetDeviceToken(const char* hexToken) {
    if (hexToken == NULL) {
        NSLog(@"[Intercom] Device token is null; ignoring");
        return;
    }

    NSString* raw = [NSString stringWithUTF8String:hexToken];

    // Tolerate the legacy NSData.description form "<aabb ccdd>" (angle brackets + spaces) by
    // stripping those separators, then require the remainder to be clean, even-length hex. We
    // reject (rather than silently mis-parse) anything else — notably the iOS 13+ description
    // "{length = 32, bytes = 0x...}", which a brittle scanner would turn into a corrupt token.
    NSCharacterSet* separators = [NSCharacterSet characterSetWithCharactersInString:@"<> \t\n\r"];
    NSString* hex = [[raw componentsSeparatedByCharactersInSet:separators] componentsJoinedByString:@""];

    NSCharacterSet* nonHex = [[NSCharacterSet characterSetWithCharactersInString:@"0123456789abcdefABCDEF"] invertedSet];
    if (hex.length == 0 || (hex.length % 2) != 0 || [hex rangeOfCharacterFromSet:nonHex].location != NSNotFound) {
        NSLog(@"[Intercom] Device token is not a clean hex string; refusing to register. "
               "Pass the lowercase hex APNs token, not NSData.description.");
        return;
    }

    NSMutableData* tokenData = [NSMutableData dataWithCapacity:hex.length / 2];
    for (NSUInteger i = 0; i + 1 < hex.length; i += 2) {
        unsigned int byte = 0;
        [[NSScanner scannerWithString:[hex substringWithRange:NSMakeRange(i, 2)]] scanHexInt:&byte];
        uint8_t b = (uint8_t)byte;
        [tokenData appendBytes:&b length:1];
    }

    [Intercom setDeviceToken:tokenData
                     success:^{
                       NSLog(@"[Intercom] Device token registered with Intercom");
                     }
                     failure:^(NSError* error) {
                       NSLog(@"[Intercom] Failed to set device token: %@", error);
                     }];
}

// Requests notification authorization and, if granted, registers for remote notifications. The
// resulting APNs token is forwarded to Intercom automatically by IntercomAppController (the
// UnityAppController subclass), so the C# layer only needs to call this once after Initialize().
void _RegisterForPushNotifications() {
    UNUserNotificationCenter* center = [UNUserNotificationCenter currentNotificationCenter];
    UNAuthorizationOptions options = UNAuthorizationOptionAlert | UNAuthorizationOptionSound | UNAuthorizationOptionBadge;
    [center requestAuthorizationWithOptions:options
                          completionHandler:^(BOOL granted, NSError* _Nullable error) {
                            if (error != nil) {
                                NSLog(@"[Intercom] Notification authorization error: %@", error);
                            }
                            if (granted) {
                                dispatch_async(dispatch_get_main_queue(), ^{
                                  [[UIApplication sharedApplication] registerForRemoteNotifications];
                                });
                            } else {
                                NSLog(@"[Intercom] Notification permission was not granted");
                            }
                          }];
}

void _SetLogLevel(const char* logLevel) {
    NSString* nsLogLevel = [NSString stringWithUTF8String:logLevel];

    if ([nsLogLevel caseInsensitiveCompare:@"verbose"] == NSOrderedSame ||
        [nsLogLevel caseInsensitiveCompare:@"debug"] == NSOrderedSame ||
        [nsLogLevel caseInsensitiveCompare:@"info"] == NSOrderedSame ||
        [nsLogLevel caseInsensitiveCompare:@"warn"] == NSOrderedSame ||
        [nsLogLevel caseInsensitiveCompare:@"error"] == NSOrderedSame ||
        [nsLogLevel caseInsensitiveCompare:@"assert"] == NSOrderedSame) {
        [Intercom enableLogging];
    } else {
        NSLog(@"[Intercom] Invalid log level: %@", nsLogLevel);
    }
}

int _GetUnreadConversationCount() {
    return (int)[Intercom unreadConversationCount];
}

bool _IsUserLoggedIn() {
    return [Intercom isUserLoggedIn];
}

const char* _FetchLoggedInUserAttributes() {
    ICMUserAttributes* attributes = [Intercom fetchLoggedInUserAttributes];
    if (!attributes) {
        return strdup("{}");
    }

    NSMutableDictionary* userDict = [NSMutableDictionary new];
    if (attributes.userId) {
        userDict[@"userId"] = attributes.userId;
    }
    if (attributes.email) {
        userDict[@"email"] = attributes.email;
    }

    NSData* jsonData = [NSJSONSerialization dataWithJSONObject:userDict options:0 error:nil];
    NSString* jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
    return strdup([jsonString UTF8String]);
}
}
