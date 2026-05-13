//
//  IAPManager.m
//  basketball-mobile
//
//  Created by macbook on 2021/10/12.
//

#import "IAPManager.h"
#import "PluginUtils.h"
#import "NSDictionary+SafeUseDictionary.h"

@interface PluginApplePay: NSObject <IAPDelegateProtocol>
+(PluginApplePay *)getInstance;

@property(nonatomic, strong) IAPManager * manager;

- (void) initProducts:(NSArray<NSString *> *)prodctIds;
- (void) buyProduct:(NSString *)productId withExtra:(NSString *)extra;
- (void) finishTransaction:(NSString *)transactionId;

@end

static const char * appleDelegateGameObjectName = "AppleDelegateListener";

static const char * kAppleDelegateInitPrudctsSuccess = "OnPluginAppleInitProductsSuccess";
static const char * kAppleDelegateInitPrudctsFail = "OnPluginAppleInitProductsFail";
static const char * kAppleDelegateBuyPrudctSuccess = "OnPluginAppleBuyProductSuccess";
static const char * kAppleDelegateBuyPrudctFail = "OnPluginAppleBuyProductFail";
static const char * kAppleDelegateRestoreTransactionFail = "OnPluginAppleRestoreTransactionFail";

static PluginApplePay* instance;

@implementation PluginApplePay
+(PluginApplePay *) getInstance
{
    if (!instance) {
        instance = [PluginApplePay new];
#if !__has_feature(objc_arc)
        [instance retain];
#endif
    }
    return instance;
}

- (id) init
{
    if (self = [super init]) {
        self.manager = [[IAPManager alloc] initWithDelegate:self preset:nil];
    }
    return self;
}

- (void) initProducts:(NSArray<NSString *> *)prodctIds
{
    [self.manager initWithProductIds:prodctIds];
}

- (void) buyProduct:(NSString *)productId withExtra:(NSString *)extra
{
    [self.manager buyProductWithId:productId withExtra:extra ? extra : @""];
}

- (void) finishTransaction:(NSString *)transactionId
{
    [self.manager finishTransaction:transactionId];
}

- (void)handleIAPInitError:(int)errorCode withMsg:(NSString *)message {
    NSDictionary * result = @{
        @"errorCode": @(errorCode),
        @"message": message
    };
    UnitySendMessage(appleDelegateGameObjectName, kAppleDelegateInitPrudctsFail, [[PluginUtils dictToJson:result] UTF8String]);
}

- (void)handleIAPInitSuccess:(NSDictionary *)priceDict {
    UnitySendMessage(appleDelegateGameObjectName, kAppleDelegateInitPrudctsSuccess, [[PluginUtils dictToJson:priceDict] UTF8String]);
}

- (void)handleIAPPayError:(int)errorCode withMsg:(NSString *)message { 
    NSDictionary * result = @{
        @"errorCode": @(errorCode),
        @"message": message
    };
    UnitySendMessage(appleDelegateGameObjectName, kAppleDelegateBuyPrudctFail, [[PluginUtils dictToJson:result] UTF8String]);
}

- (void)handleIAPPaySuccess:(NSDictionary *)result {
    UnitySendMessage(appleDelegateGameObjectName, kAppleDelegateBuyPrudctSuccess, [[PluginUtils dictToJson:result] UTF8String]);
}

- (void)handleIAPRestoreTransactionFail:(int)errorCode withMsg:(NSString *)message { 
    NSDictionary * result = @{
        @"errorCode": @(errorCode),
        @"message": message
    };
    UnitySendMessage(appleDelegateGameObjectName, kAppleDelegateRestoreTransactionFail, [[PluginUtils dictToJson:result] UTF8String]);
}

@end

#if defined(__cplusplus)
extern "C"
{
#endif
    bool Plugin_Apple_Pay_Enable()
    {
        return [IAPManager enablePay];
    }

    /**
      products 将所有产品ID用#隔开连接成一个字符串传入
     */
    void Plugin_Apple_Pay_Init_With_Products(const char * prodcuts)
    {
        NSString * productsStr = [NSString stringWithUTF8String:prodcuts];
        NSArray * productIds = [productsStr componentsSeparatedByString:@"#"];
        [[PluginApplePay getInstance] initProducts:productIds];
    }
    
    /**
      extra 会作为成功回参中的extra原样返回（没有值时返回空字符串），但有可能会丢失
     */
    void Plugin_Apple_Buy_Prodcut_With_Product_Id(const char * productId, const char * extra)
    {
        NSString * productIdNS = [NSString stringWithUTF8String:productId];
        NSString * extraNS = nil;
        if (!!extra) {
            extraNS = [NSString stringWithUTF8String:extra];
        }
        [[PluginApplePay getInstance] buyProduct:productIdNS withExtra:extraNS];
    }
    
    void Plugin_Apple_Finish_Transaction(const char * transactionId)
    {
        NSString * transactionIdNS = [NSString stringWithUTF8String:transactionId];
        [[PluginApplePay getInstance] finishTransaction:transactionIdNS];
    }

    void Plugin_Apple_Restore_Transactions()
    {
        [[PluginApplePay getInstance] restoreCompletedTransactions];
    }
#if defined(__cplusplus)
}
#endif


