#import "ATUnitySceneDelegate.h"
#import "UnityAppController.h"

@interface UnityAppController (ATSceneInit)
- (void)initUnityWithApplication:(UIApplication*)application;
@end

@implementation ATUnitySceneDelegate

- (void)scene:(UIScene *)scene
willConnectToSession:(UISceneSession *)session
      options:(UISceneConnectionOptions *)connectionOptions API_AVAILABLE(ios(13.0), tvos(13.0))
{
    if (![scene isKindOfClass: [UIWindowScene class]])
        return;

    UIWindowScene *windowScene = (UIWindowScene *)scene;
    UnityAppController *appController = GetAppController();
    if (appController == nil)
        return;

    // Always drive Unity UIWindow creation from the scene connection path on modern iOS SDKs.
    // AppDelegate may have deferred init when using UIApplicationSceneManifest.
    if (appController.window == nil)
    {
        [appController initUnityWithApplication: UIApplication.sharedApplication];
    }

    UIWindow *unityWindow = appController.window;
    if (unityWindow == nil)
        return;

    if (unityWindow.windowScene != windowScene)
        unityWindow.windowScene = windowScene;

    self.window = unityWindow;
    unityWindow.hidden = NO;
    [unityWindow makeKeyAndVisible];
}

- (void)sceneDidBecomeActive:(UIScene *)scene API_AVAILABLE(ios(13.0), tvos(13.0))
{
    if (self.window != nil)
    {
        self.window.hidden = NO;
        [self.window makeKeyAndVisible];
    }
}

@end
