#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <GoogleSignIn/GoogleSignIn.h>
#if !UNITY_XCODE_PROJECT_TYPE_SWIFT
#import "AppDelegateListener.h"
#endif
extern "C" UIViewController* UnityGetGLViewController(void);
extern "C" void UnitySendMessage(const char*, const char*, const char*);

#if UNITY_XCODE_PROJECT_TYPE_SWIFT
@interface TFGoogleSignInListener : NSObject
#else
@interface TFGoogleSignInListener : NSObject<AppDelegateListener>
#endif
@end
@implementation TFGoogleSignInListener
- (void)onOpenURL:(NSNotification*)notification {
    NSURL* url = notification.userInfo[@"url"];
    if (url) [[GIDSignIn sharedInstance] handleURL:url];
}
- (void)onSceneOpenURLContexts:(NSNotification*)notification {
    id contexts = notification.userInfo[@"UnityNotificationsOpenURLContextsKey"];
    if (![contexts isKindOfClass:[NSSet class]]) return;
    for (UIOpenURLContext* context in (NSSet*)contexts)
        if ([context isKindOfClass:[UIOpenURLContext class]]) [[GIDSignIn sharedInstance] handleURL:context.URL];
}
@end

static void TFGoogleResult(NSString* target, int request, NSString* token, BOOL canceled, NSString* error) {
    NSDictionary* result = @{ @"requestId":@(request), @"idToken":token ?: @"",
                              @"canceled":@(canceled), @"error":error ?: @"" };
    NSData* data = [NSJSONSerialization dataWithJSONObject:result options:0 error:nil];
    NSString* json = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    UnitySendMessage(target.UTF8String, "OnGoogleSignIn", json.UTF8String);
}
extern "C" void TFGoogleSignIn(const char* target, int request, const char* clientId, const char* serverClientId) {
    NSString* receiver = [NSString stringWithUTF8String:target];
    NSString* client = [NSString stringWithUTF8String:clientId];
    NSString* server = serverClientId ? [NSString stringWithUTF8String:serverClientId] : @"";
    dispatch_async(dispatch_get_main_queue(), ^{
        static TFGoogleSignInListener* listener;
        static dispatch_once_t once;
        dispatch_once(&once, ^{
            listener = [TFGoogleSignInListener new];
#if UNITY_XCODE_PROJECT_TYPE_SWIFT
            [[NSNotificationCenter defaultCenter] addObserver:listener selector:@selector(onSceneOpenURLContexts:)
                name:@"UnitySceneOpenURLContexts" object:nil];
#else
            UnityRegisterAppDelegateListener(listener);
#endif
        });
        GIDConfiguration* configuration = server.length
            ? [[GIDConfiguration alloc] initWithClientID:client serverClientID:server]
            : [[GIDConfiguration alloc] initWithClientID:client];
        [GIDSignIn sharedInstance].configuration = configuration;
        [[GIDSignIn sharedInstance] signInWithPresentingViewController:UnityGetGLViewController()
            completion:^(GIDSignInResult* result, NSError* error) {
                BOOL canceled = error && error.code == kGIDSignInErrorCodeCanceled;
                TFGoogleResult(receiver, request, result.user.idToken.tokenString, canceled,
                    error && !canceled ? @"sign_in_failed" : @"");
            }];
    });
}
