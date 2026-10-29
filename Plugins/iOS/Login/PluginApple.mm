
#import <AuthenticationServices/AuthenticationServices.h>
#import "PluginUtils.h"
#import "PluginListener.h"

@interface PluginApple : UIViewController<
    AppDelegateListener,
    ASAuthorizationControllerDelegate,
    ASAuthorizationControllerPresentationContextProviding>
@end

static const char * appleDelegateGameObjectName = "AppleDelegateListener";

static const char * kAppDeleteSuccessCallback = "OnPluginAppleLoginSuccess";
static const char * kAppDeleteCancelCallback = "OnPluginAppleLoginCancel";
static const char * kAppDeleteFailCallback = "OnPluginAppleLoginFail";
static const char * kAppDeleteLoginStatusChange = "OnPluginAppleLoginStatusChange";

@implementation PluginApple
{
    NSString * loginedUserId;
    int loginType;
}

PluginApple* instance;

+(PluginApple*)GetInstance
{
    if (instance == NULL)
    {
        instance = [[PluginApple alloc]init];
    }
    return instance;
}

- (void)loginWithAppleId
{
    if (@available(iOS 13.0, *)) {
        ASAuthorizationAppleIDProvider * appleIDProvider = [[ASAuthorizationAppleIDProvider alloc] init];
        
        ASAuthorizationAppleIDRequest * request = [appleIDProvider createRequest];
        [request setRequestedScopes:@[ASAuthorizationScopeFullName, ASAuthorizationScopeEmail]];
            
        ASAuthorizationController * authorizationController = [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[request]];
        authorizationController.delegate = self;
        authorizationController.presentationContextProvider = self;
        [authorizationController performRequests];
    } else {
        UnitySendMessage(appleDelegateGameObjectName, kAppDeleteFailCallback, "Invalid");
    }
}

- (void)loginWithPassword
{
    if (@available(iOS 13.0, *)) {
        ASAuthorizationPasswordProvider * passwordProvider = [[ASAuthorizationPasswordProvider alloc] init];
        
        ASAuthorizationPasswordRequest * request = [passwordProvider createRequest];
        
        ASAuthorizationController * authorizationController = [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[request]];
        authorizationController.delegate = self;
        authorizationController.presentationContextProvider = self;
        [authorizationController performRequests];
    } else {
        UnitySendMessage(appleDelegateGameObjectName, kAppDeleteFailCallback, "Invalid");
    }
}


- (void)authorizationController:(ASAuthorizationController *)controller didCompleteWithAuthorization:(ASAuthorization *)authorization API_AVAILABLE(ios(13.0))
{
    NSLog(@"%s - %@, auth=%@", __FUNCTION__, controller, authorization);
   
    if ([authorization.credential isKindOfClass:[ASAuthorizationAppleIDCredential class]]) {
        // ASAuthorizationAppleIDCredential
        ASAuthorizationAppleIDCredential *appleIDCredential = authorization.credential;
        NSString* userIdentifier = appleIDCredential.user;
        NSPersonNameComponents* fullName = appleIDCredential.fullName;
        
        NSString* fullNameDisplay =
        [NSPersonNameComponentsFormatter localizedStringFromPersonNameComponents:fullName style:NSPersonNameComponentsFormatterStyleDefault options:0];
        
        NSString* email = appleIDCredential.email;
        NSString* identityToken = [[NSString alloc] initWithData:appleIDCredential.identityToken encoding:NSUTF8StringEncoding]; // access token
        NSString* authorizationCode = [[NSString alloc] initWithData:appleIDCredential.authorizationCode encoding:NSUTF8StringEncoding]; // refresh token
        ASUserDetectionStatus realUserStatus = appleIDCredential.realUserStatus;
        NSLog(@"Apple login success, userId=%@, fullName=%@, email=%@, identityToken=%@, authorizationCode=%@, realUserStatus=%@", userIdentifier, fullNameDisplay, email, identityToken, authorizationCode, @(realUserStatus));
        
        NSDictionary * dict = @{
            @"type" : @"AppleId",
            @"userId" : userIdentifier,
            @"userName" : fullNameDisplay,
            //@"email" : email,
            @"idToken" : identityToken,
            @"authToken" : authorizationCode,
            @"status" : @(realUserStatus)
        };
        
        loginType = 1;
        loginedUserId = userIdentifier;
        
        UnitySendMessage(appleDelegateGameObjectName, kAppDeleteSuccessCallback, [[PluginUtils dictToJson:dict] UTF8String]);
    }
    else if ([authorization.credential isKindOfClass:[ASPasswordCredential class]])
    {
        ASPasswordCredential * passwordCredential = authorization.credential;
        NSString* user = passwordCredential.user;
        NSString* password = passwordCredential.password;
        // 安全提示：不要把 password 打进日志（NSLog 会写入设备日志，可能被收集/泄露），仅记录用户标识
        NSLog(@"Apple login success, user=%@", user);
   
        NSDictionary * dict = @{
            @"type" : @"Password",
            @"user" : user,
            @"password" : password
        };
        
        loginType = 2;
        loginedUserId = user;
        
        UnitySendMessage(appleDelegateGameObjectName, kAppDeleteSuccessCallback, [[PluginUtils dictToJson:dict] UTF8String]);
    }
    else
    {
        loginType = 0;
        loginedUserId = nil;
        
        UnitySendMessage(appleDelegateGameObjectName, kAppDeleteFailCallback, "Not supported login type");
    }
}

-(void) authorizationController:(ASAuthorizationController *)controller didCompleteWithError:(NSError *)error  API_AVAILABLE(ios(13.0))
{
    NSLog(@"Apple login failed. %s - error=%@", __FUNCTION__, error);
    
    loginType = 0;
    loginedUserId = nil;
    
    NSString *errorMsg = nil;
    switch (error.code)
    {
        case ASAuthorizationErrorCanceled:
            errorMsg = @"ASAuthorizationErrorCanceled";
            UnitySendMessage(appleDelegateGameObjectName, kAppDeleteCancelCallback, [errorMsg UTF8String]);
            return;
        case ASAuthorizationErrorFailed:
            errorMsg = @"ASAuthorizationErrorFailed";
            break;
        case ASAuthorizationErrorInvalidResponse:
            errorMsg = @"ASAuthorizationErrorInvalidResponse";
            break;
        case ASAuthorizationErrorNotHandled:
            errorMsg = @"ASAuthorizationErrorNotHandled";
            break;
        case ASAuthorizationErrorUnknown:
            errorMsg = @"ASAuthorizationErrorUnknown";
            break;
        default:
            errorMsg = @"unknown_error";
            break;
    }
    UnitySendMessage(appleDelegateGameObjectName, kAppDeleteFailCallback, [errorMsg UTF8String]);
}

-(void) checkAppleLoginState
{
    if (@available(iOS 13.0, *))
    {
        if (loginType == 1)
        {
            ASAuthorizationAppleIDProvider *appleIDProvider = [[ASAuthorizationAppleIDProvider alloc] init];
            [appleIDProvider getCredentialStateForUserID:loginedUserId completion:^(ASAuthorizationAppleIDProviderCredentialState credentialState, NSError * _Nullable error)
            {
                if (error)
                {
                    UnitySendMessage(appleDelegateGameObjectName, kAppDeleteLoginStatusChange, [[NSString stringWithFormat:@"error code = %ld", (long)error.code] UTF8String]);
                }
                else
                {
                    switch (credentialState)
                    {
                        case ASAuthorizationAppleIDProviderCredentialAuthorized:
                            break;
                        case ASAuthorizationAppleIDProviderCredentialRevoked:
                        case ASAuthorizationAppleIDProviderCredentialNotFound:
                        case ASAuthorizationAppleIDProviderCredentialTransferred:
                            NSString * status = [NSString stringWithFormat:@"status %ld",(long)credentialState];
                            UnitySendMessage(appleDelegateGameObjectName, kAppDeleteLoginStatusChange,[status UTF8String]);
                            break;
                    }
                }
            }];
        }
    }
}

// check login status when enter force ground
- (void)willEnterForeground:(NSNotification*)notification
{
    [self checkAppleLoginState];
}

// delete api override ------------------------------

- (nonnull ASPresentationAnchor)presentationAnchorForAuthorizationController:(nonnull ASAuthorizationController *)controller  API_AVAILABLE(ios(13.0)){
    return self.view.window;
}

- (void)encodeWithCoder:(nonnull NSCoder *)coder {
    [super encodeWithCoder:coder];
}

- (void)traitCollectionDidChange:(nullable UITraitCollection *)previousTraitCollection {
    [super traitCollectionDidChange:previousTraitCollection];
}

- (void)preferredContentSizeDidChangeForChildContentContainer:(nonnull id<UIContentContainer>)container {
    [super preferredContentSizeDidChangeForChildContentContainer:container];
}

- (CGSize)sizeForChildContentContainer:(nonnull id<UIContentContainer>)container withParentContainerSize:(CGSize)parentSize {
    return [super sizeForChildContentContainer: container
                       withParentContainerSize:parentSize];
}

- (void)systemLayoutFittingSizeDidChangeForChildContentContainer:(nonnull id<UIContentContainer>)container {
    [super systemLayoutFittingSizeDidChangeForChildContentContainer:container];
}

- (void)viewWillTransitionToSize:(CGSize)size withTransitionCoordinator:(nonnull id<UIViewControllerTransitionCoordinator>)coordinator {
    [super viewWillTransitionToSize:size withTransitionCoordinator:coordinator];
}

- (void)willTransitionToTraitCollection:(nonnull UITraitCollection *)newCollection withTransitionCoordinator:(nonnull id<UIViewControllerTransitionCoordinator>)coordinator {
    [super willTransitionToTraitCollection:newCollection
                 withTransitionCoordinator:coordinator];
}

- (void)didUpdateFocusInContext:(nonnull UIFocusUpdateContext *)context withAnimationCoordinator:(nonnull UIFocusAnimationCoordinator *)coordinator {
    [super didUpdateFocusInContext:context withAnimationCoordinator:coordinator];
}

- (void)setNeedsFocusUpdate {
    [super setNeedsFocusUpdate];
}

- (BOOL)shouldUpdateFocusInContext:(nonnull UIFocusUpdateContext *)context {
    return [super shouldUpdateFocusInContext:context];
}

- (void)updateFocusIfNeeded {
    [super updateFocusIfNeeded];
}

#if defined(__cplusplus)
extern "C"
{
#endif
    bool Plugin_Apple_IsEnableLogin()
    {
        if (@available(iOS 13.0, *))
            return true;
        else
            return false;
    }
    
    void Plugin_Apple_Login_With_AppleId()
    {
        [[PluginApple GetInstance]loginWithAppleId];
    }
    
    void Plugin_Apple_Login_With_Password()
    {
        [[PluginApple GetInstance]loginWithPassword];
    }
#if defined(__cplusplus)
}
#endif

@end
