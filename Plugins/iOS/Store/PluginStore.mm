#import <StoreKit/StoreKit.h>


#if defined(__cplusplus)
extern "C"
{
#endif
    void Plugin_Apple_Store_Review()
    {
        if (@available(iOS 10.3, *)) {
            [SKStoreReviewController requestReview];
        }
    }

#if defined(__cplusplus)
}
#endif

