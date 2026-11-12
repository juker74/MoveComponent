#import <UIKit/UIKit.h>

// Minimal UIWindowSceneDelegate for Xcode/iOS SDKs that require UIScene lifecycle.
// Full support belongs in Unity 2022.3.72f1+ (UnityScene.h/mm). This is a bridge for older editors.
API_AVAILABLE(ios(13.0), tvos(13.0))
@interface ATUnitySceneDelegate : UIResponder <UIWindowSceneDelegate>
@property (strong, nonatomic) UIWindow *window;
@end
