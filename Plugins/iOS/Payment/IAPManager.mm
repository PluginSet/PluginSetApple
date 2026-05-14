//
//  IAPManager.m
//  basketball-mobile
//
//  Created by macbook on 2021/10/12.
//

#import "IAPManager.h"
#import <StoreKit/StoreKit.h>
#import "NSDictionary+SafeUseDictionary.h"

#if __has_feature(objc_arc)
#define IAP_ENABLE_ARC 0
#else
#define IAP_ENABLE_ARC 1
#endif

#define KEY_APPSTORE_PRODUCT_DICT  @"AppStoreProductDict"
#define KEY_APPSOTRE_CUSTOM_ORDER  @"AppStoreCustomOrder"

#define ERROR_CODE_PAY_FAIL (1000)

#define IF_NIL_EMPTY(s) (s ? s : @"")

@interface IAPManager()
@property(nonatomic, strong) id<IAPDelegateProtocol> delegate;
@property(nonatomic, strong) NSString * applicationUserName;

@property(nonatomic, strong) SKProductsRequest * request;
@property(nonatomic, strong) NSMutableSet * requestProductIds;
@property(nonatomic, strong) NSArray * products;
@property(nonatomic, strong) NSDictionary * productsPriceDict;

@property(nonatomic, strong) NSString * prePurchaseProductId;
@property(nonatomic, strong) NSString * prePurchaseExtra;

@property(nonatomic, strong) NSMutableDictionary * customExtras;
@property(nonatomic, strong) NSString * purchasingProductId;
@property(nonatomic, strong) NSMutableDictionary * unFinishedProducts;
@property(nonatomic, strong) NSMutableDictionary * unFinishedTransacions;

@property BOOL hasInvokePayResult;

-(void) initProducts;
@end


@implementation IAPManager

+ (bool) enablePay
{
    return [SKPaymentQueue canMakePayments];
}

- (void)loadCustomOrderDict:(NSString *)userName
{
    if (self.customExtras) {
#if IAP_ENABLE_ARC
        [self.customExtras release];
#endif
        self.customExtras = nil;
    }
    
    NSString * saveKey = [NSString stringWithFormat:@"%@_%@", KEY_APPSOTRE_CUSTOM_ORDER, userName];
    NSDictionary * dict = [[NSUserDefaults standardUserDefaults] objectForKey:saveKey];
    if (dict) {
        self.customExtras = [NSMutableDictionary dictionaryWithDictionary:dict];
    } else {
        self.customExtras = [NSMutableDictionary new];
    }
#if IAP_ENABLE_ARC
    [self.customExtras retain];
#endif
}

- (void)saveCustomOrderDict
{
    if (!self.customExtras) return;
    NSString * userName = self.applicationUserName ? self.applicationUserName : @"null";
    NSString * saveKey = [NSString stringWithFormat:@"%@_%@", KEY_APPSOTRE_CUSTOM_ORDER, userName];
    [[NSUserDefaults standardUserDefaults] setObject:self.customExtras forKey:saveKey];
    [[NSUserDefaults standardUserDefaults] synchronize];
}

- (id) initWithDelegate:(NSObject<IAPDelegateProtocol> *)delegate preset:(NSString *)applicationUserName
{
    if (self = [self init]) {
        [self setApplicationUserName:applicationUserName];
        self.requestProductIds = [[NSMutableSet alloc] init];
        self.hasInvokePayResult = [delegate respondsToSelector:@selector(invokePayResult:currency:price:success:)];
        self.delegate = delegate;
        [[SKPaymentQueue defaultQueue] addTransactionObserver:self];
    }
    return self;
}

-(void) setApplicationUserName:(NSString *)applicationUserName {
    _applicationUserName = applicationUserName;
    self.purchasingProductId = nil;  // clean user purchasing product
    [self loadCustomOrderDict:self.applicationUserName ? self.applicationUserName : @"null"];
}

- (void) dealloc {
    [[SKPaymentQueue defaultQueue] removeTransactionObserver:self];
    if (self.request != nil) {
        [self.request cancel];
        self.request = nil;
    }
    if (self.unFinishedProducts) {
#if IAP_ENABLE_ARC
        [self.unFinishedProducts release];
#endif
        self.unFinishedProducts = nil;
    }
    if (self.unFinishedTransacions) {
#if IAP_ENABLE_ARC
        [self.unFinishedTransacions release];
#endif
        self.unFinishedTransacions = nil;
    }
#if IAP_ENABLE_ARC
    [super dealloc];
#endif
}

- (void) initWithProductIds:(NSArray *)productIds {
    if (![SKPaymentQueue canMakePayments]) {
        [self.delegate handleIAPInitError:ERROR_CODE_PAY_FAIL + 0 withMsg:@"您的手机没有打开程序内付费购买"];
        return;
    }
    
    // check all request
    BOOL dirty = NO;
    for (NSString * productId in productIds) {
        if ([self.requestProductIds containsObject:productId]) {
            continue;
        }
        dirty = YES;
        [self.requestProductIds addObject:productId];
    }
    
    if (!dirty) {
        [self.delegate handleIAPInitSuccess:self.productsPriceDict ? self.productsPriceDict : @{}];
        return;
    }
    
    [self initProducts];
}

- (void) initProducts
{
    self.products = nil;
    
    if (self.request) {
        [self.request cancel];
        self.request = nil;
    }
    
    self.request = [[SKProductsRequest alloc] initWithProductIdentifiers:self.requestProductIds];
    self.request.delegate = self;
    [self.request start];
}

- (SKProduct *) findProduct:(NSString *)productId
{
    SKProduct * findProduct = nil;
    for (SKProduct * product in self.products) {
        if ([product.productIdentifier isEqual:productId]) {
            findProduct = product;
            break;
        }
    }

    return findProduct;
}

- (void) buyProductWithId:(NSString*) productId withExtra:(NSString *)extra
{
    if (![self.requestProductIds containsObject:productId]) {
        self.prePurchaseProductId = productId;
        self.prePurchaseExtra = extra;
        [self.requestProductIds addObject:productId];
        [self initProducts];
        return;
    }
    
    if (self.request) {
        self.prePurchaseProductId = productId;
        self.prePurchaseExtra = extra;
        return;
    }
    
    if (self.products == nil) {
        [self.delegate handleIAPPayError:ERROR_CODE_PAY_FAIL + 1 withMsg:@"初始化未完成"];
        return;
    }
    
    if (self.purchasingProductId) {
        [self.delegate handleIAPPayError:ERROR_CODE_PAY_FAIL + 2 withMsg:@"正在支付中，请稍后重试"];
        return;
    }
    
    if (self.unFinishedProducts && [[self.unFinishedProducts allValues] containsObject:productId]) {
        [self.delegate handleIAPPayError:ERROR_CODE_PAY_FAIL + 3 withMsg:@"还有未处理的订单信息"];
        return;
    }
    
    SKProduct * findProduct = [self findProduct:productId];
    if (findProduct == nil) {
        [self.delegate handleIAPPayError:ERROR_CODE_PAY_FAIL + 4 withMsg:@"未找到相应的商品"];
        return;
    }
    
    self.purchasingProductId = productId;
    [self.customExtras setValue:extra forKey:productId];
    [self saveCustomOrderDict];
    
    SKMutablePayment * payment = [SKMutablePayment paymentWithProduct:findProduct];
//    SKMutablePayment * payment = [SKMutablePayment paymentWithProductIdentifier:productId];
    payment.applicationUsername = self.applicationUserName;
//    payment.simulatesAskToBuyInSandbox = true;
    [[SKPaymentQueue defaultQueue] addPayment:payment];
}

- (void) restoreCompletedTransactions {
    if (self.applicationUserName) {
        [[SKPaymentQueue defaultQueue] restoreCompletedTransactionsWithApplicationUsername:self.applicationUserName];
    } else {
        [[SKPaymentQueue defaultQueue] restoreCompletedTransactions];
    }
}

- (void) finishTransaction:(NSString*) transactionId {
    if (!transactionId) return;
    if (self.unFinishedProducts && [self.unFinishedProducts hasKey:transactionId]) {
        [self.unFinishedProducts removeObjectForKey:transactionId];
    }
    if (!self.unFinishedTransacions || ![self.unFinishedTransacions hasKey:transactionId]) return;
    SKPaymentTransaction * transaction = [self.unFinishedTransacions valueForKey:transactionId];
    [self finishTransactionInternal:transaction success:true];
    [self.unFinishedTransacions removeObjectForKey:transactionId];
}

- (void) finishTransactionInternal:(SKPaymentTransaction *)transaction success:(bool)success
{
    NSString * productId = transaction.payment.productIdentifier;
    if (self.customExtras) {
        if ([self.customExtras hasKey:productId]) {
            [self.customExtras removeObjectForKey:productId];
            [self saveCustomOrderDict];
        }
    }
    
    if (self.hasInvokePayResult) {
        SKProduct * product = [self findProduct:productId];
        int price = product ? product.price.intValue : 0;
        NSString * currency = product ? product.priceLocale.currencyCode : @"unknow";
        NSString * trasactionId = transaction.transactionIdentifier;
        if (!trasactionId) {
            int nowSecond =(int)[[NSDate date] timeIntervalSince1970];
            trasactionId = [NSString stringWithFormat:@"unknow_%@_%d", productId, nowSecond];
        }
        
        [self.delegate invokePayResult:trasactionId
                              currency:currency
                                 price:price
                               success:success];
    }
        
    [[SKPaymentQueue defaultQueue] finishTransaction:transaction];
}

- (void) onPaymentTransactionPurchased:(SKPaymentTransaction *) transaction {
    NSLog(@"onPaymentTransactionPurchased >>>>> %@=>%@ %@", self.applicationUserName, transaction.payment.applicationUsername, transaction.transactionIdentifier);
    if (self.applicationUserName) {
        NSString * transUserName = transaction.payment.applicationUsername;
        if (transUserName && ![self.applicationUserName isEqual:transUserName])
        {
            NSLog(@"这是另一个用户的订单，当前用户名：%@，订单用户名%@", self.applicationUserName, transUserName);
            return;
        }
    }
    
    NSURL * appStoreReceiptUrl = [[NSBundle mainBundle]appStoreReceiptURL];
    if (!appStoreReceiptUrl) {
        [self.delegate handleIAPPayError:ERROR_CODE_PAY_FAIL + 5 withMsg:@"Empty appStore receipt url"];
        [[SKPaymentQueue defaultQueue] finishTransaction:transaction];
        return;
    }
    
    if (![[NSFileManager defaultManager] fileExistsAtPath:appStoreReceiptUrl.path]) {
        [self.delegate handleIAPPayError:ERROR_CODE_PAY_FAIL + 6 withMsg:@"Receipt file not found"];
        [[SKPaymentQueue defaultQueue] finishTransaction:transaction];
        return;
    }
    
    __auto_type receiptData = [[NSFileManager defaultManager] contentsAtPath:appStoreReceiptUrl.path];
    if (!receiptData) {
        [self.delegate handleIAPPayError:ERROR_CODE_PAY_FAIL + 7 withMsg:@"Receipt data is nil"];
        [[SKPaymentQueue defaultQueue] finishTransaction:transaction];
        return;
    }
    
    if (!self.unFinishedProducts) {
        self.unFinishedProducts = [NSMutableDictionary new];
#if IAP_ENABLE_ARC
        [self.unFinishedProducts retain];
#endif
    }
    if (!self.unFinishedTransacions) {
        self.unFinishedTransacions = [NSMutableDictionary new];
#if IAP_ENABLE_ARC
        [self.unFinishedTransacions retain];
#endif
    }
    NSString * transactionId = transaction.transactionIdentifier;
    [self.unFinishedTransacions setValue:transaction forKey:transactionId];
    
    NSString * productId = transaction.payment.productIdentifier;
    [self.unFinishedProducts setValue:productId forKey:transactionId];
    
    NSString * customExtra = @"";
    if (self.customExtras) {
        customExtra = [self.customExtras optString:productId default:@""];
    }
    
    NSDictionary * result = @{
        @"transactionId": transactionId,
        @"productId": productId,
        @"extra": customExtra,
        @"receipt": [receiptData base64EncodedStringWithOptions:0]
    };
    [self.delegate handleIAPPaySuccess:result];
}

- (void)checkCleanPurchasingProduct:(SKPaymentTransaction *)transaction
{
    if (self.applicationUserName) {
        // check application user name
        NSString * userName = transaction.payment.applicationUsername;
        if (userName && ![userName isEqual:self.applicationUserName]) {
            return;
        }
    }
    
    if ([transaction.payment.productIdentifier isEqual:self.purchasingProductId]) {
        self.purchasingProductId = nil;
    }
}

#pragma delegate api

- (void)productsRequest:(nonnull SKProductsRequest *)request didReceiveResponse:(nonnull SKProductsResponse *)response {
    
    NSMutableDictionary * priceDict = [NSMutableDictionary new];
    NSNumberFormatter * numberFormatter = [NSNumberFormatter new];
    [numberFormatter setFormatterBehavior:NSNumberFormatterBehavior10_4];
    [numberFormatter setNumberStyle:NSNumberFormatterCurrencyStyle];
    
    for (SKProduct * product in response.products) {
        [numberFormatter setLocale:product.priceLocale];
        NSString * formattedPrice = [numberFormatter stringForObjectValue:product.price];
        NSString * productId = product.productIdentifier;
        NSDecimalNumber *price = product.price;
        NSString * currency = product.priceLocale.currencyCode;
        NSString * title = IF_NIL_EMPTY(product.localizedTitle);
        NSString * desc = IF_NIL_EMPTY(product.localizedDescription);
        [priceDict setObject:@{
            @"productId": productId,
            @"price": price,
            @"currency": currency,
            @"priceString": formattedPrice,
            @"title": title,
            @"description":desc
        } forKey:productId];
        
        NSLog(@"item::%@(%@)[id=%@] price:%@", product.localizedTitle, product.localizedDescription, product.productIdentifier, formattedPrice);
    }
    
    if (request == self.request) {
        self.request = nil;
    }
    
    self.products = response.products;
    self.productsPriceDict = priceDict;
    [self.delegate handleIAPInitSuccess:priceDict];
    
    if (self.prePurchaseProductId) {
        [self buyProductWithId:self.prePurchaseProductId withExtra:self.prePurchaseExtra];
        self.prePurchaseProductId = nil;
        self.prePurchaseExtra = nil;
    }
}

- (void)paymentQueue:(SKPaymentQueue *)queue updatedTransactions:(NSArray<SKPaymentTransaction *> *)transactions {
    for (SKPaymentTransaction * transaction in transactions) {
        switch (transaction.transactionState) {
            case SKPaymentTransactionStatePurchasing:
                NSLog(@"用户正在购买");
                break;
            case SKPaymentTransactionStatePurchased:
                [self onPaymentTransactionPurchased:transaction];
                [self checkCleanPurchasingProduct:transaction];
                break;
            case SKPaymentTransactionStateFailed:
                [self finishTransactionInternal:transaction success:false];
                [self checkCleanPurchasingProduct:transaction];
                [self.delegate handleIAPPayError:ERROR_CODE_PAY_FAIL + 8 withMsg:transaction.error.localizedDescription];
                NSLog(@"购买失败 error: %@", transaction.error.localizedDescription);
                break;
            case SKPaymentTransactionStateRestored:
                NSLog(@"恢复购买");
                [self onPaymentTransactionPurchased:transaction];
                [self checkCleanPurchasingProduct:transaction];
                break;
            case SKPaymentTransactionStateDeferred:
                NSLog(@"最终状态未确定");
                [self checkCleanPurchasingProduct:transaction];
                break;
            default:
                break;
        }
    }
}

    // 恢复成功的回调 ✅
- (void)paymentQueue:(SKPaymentQueue *)queue paymentQueueRestoreCompletedTransactionsFinished {
    NSLog(@"restore completed:::: %@", queue.transactions.count);
    [self.delegate handleIAPRestoreTransactionSuccess:queue.transactions.count];
}

    // 恢复失败的回调 ❌
- (void)paymentQueue:(SKPaymentQueue *)queue restoreCompletedTransactionsFailedWithError:(NSError *)error {
    NSLog(@"restore error:::: %@", error);
    [self.delegate handleIAPRestoreTransactionFail:ERROR_CODE_PAY_FAIL withMsg:error.localizedDescription];
}

@end
