//
//  IAPManager.mm
//  basketball-mobile
//
//  Created by macbook on 2021/10/12.
//

#import "IAPManager.h"
#import <StoreKit/StoreKit.h>
#import "NSDictionary+SafeUseDictionary.h"

// 仅在非 ARC 模式下需要手动 retain/release
#if !__has_feature(objc_arc)
#define IAP_NEEDS_RETAIN 1
#else
#define IAP_NEEDS_RETAIN 0
#endif

#define KEY_APPSTORE_PRODUCT_DICT  @"AppStoreProductDict"
#define KEY_APPSOTRE_CUSTOM_ORDER  @"AppStoreCustomOrder"

#define ERROR_CODE_PAY_FAIL (1000)

// 错误码（保持与历史兼容，仅追加新增项）
// +0  无 IAP 权限
// +1  初始化未完成
// +2  正在支付中
// +3  还有未处理订单
// +4  未找到对应商品
// +5  appStoreReceiptURL 为空
// +6  receipt 文件不存在
// +7  receipt 数据为空
// +8  StoreKit Failed
// +9  transactionId / productId 为空
// +10 productId 入参为空
// +11 SKProductsRequest 失败
// +12 等待中的购买因初始化失败被取消

#define IF_NIL_EMPTY(s) ((s) ? (s) : @"")

@interface IAPManager()
@property(nonatomic, weak)   id<IAPDelegateProtocol> delegate;
@property(nonatomic, copy)   NSString * applicationUserName;

@property(nonatomic, strong) SKProductsRequest    * request;
@property(nonatomic, strong) NSMutableSet<NSString *> * requestProductIds;
@property(nonatomic, strong) NSArray<SKProduct *> * products;
@property(nonatomic, strong) NSDictionary         * productsPriceDict;

@property(nonatomic, copy)   NSString * prePurchaseProductId;
@property(nonatomic, copy)   NSString * prePurchaseExtra;

@property(nonatomic, strong) NSMutableDictionary * customExtras;
@property(nonatomic, copy)   NSString * purchasingProductId;

// transactionId -> SKPaymentTransaction
@property(nonatomic, strong) NSMutableDictionary<NSString *, SKPaymentTransaction *> * unFinishedTransactions;
// transactionId -> productId
@property(nonatomic, strong) NSMutableDictionary<NSString *, NSString *> * unFinishedProducts;

@property(nonatomic, assign) BOOL hasInvokePayResult;
@property(nonatomic, assign) BOOL hasHandleRestoreTransactionFail;

- (void) initProducts;
@end


@implementation IAPManager

#pragma mark - Lifecycle

+ (bool) enablePay
{
    return [SKPaymentQueue canMakePayments];
}

- (id) initWithDelegate:(NSObject<IAPDelegateProtocol> *)delegate preset:(NSString *)applicationUserName
{
    if (self = [super init]) {
        self.delegate = delegate;
        self.hasInvokePayResult              = [delegate respondsToSelector:@selector(invokePayResult:currency:price:success:)];
        self.hasHandleRestoreTransactionFail = [delegate respondsToSelector:@selector(handleIAPRestoreTransactionFail:withMsg:)];
        self.requestProductIds = [[NSMutableSet alloc] init];
        [self setApplicationUserName:applicationUserName];
        [[SKPaymentQueue defaultQueue] addTransactionObserver:self];
    }
    return self;
}

- (void) dealloc {
    [[SKPaymentQueue defaultQueue] removeTransactionObserver:self];
    if (_request) {
        _request.delegate = nil;
        [_request cancel];
        _request = nil;
    }
#if IAP_NEEDS_RETAIN
    if (_unFinishedProducts)     { [_unFinishedProducts release];     _unFinishedProducts = nil; }
    if (_unFinishedTransactions) { [_unFinishedTransactions release]; _unFinishedTransactions = nil; }
    if (_customExtras)           { [_customExtras release];           _customExtras = nil; }
    [super dealloc];
#endif
}

#pragma mark - User Settings

- (void) setApplicationUserName:(NSString *)applicationUserName {
    _applicationUserName = [applicationUserName copy];
    self.purchasingProductId = nil; // 切换用户清空"正在购买"状态
    [self loadCustomOrderDict:_applicationUserName ?: @"null"];
}

- (void) loadCustomOrderDict:(NSString *)userName
{
#if IAP_NEEDS_RETAIN
    if (_customExtras) {
        [_customExtras release];
        _customExtras = nil;
    }
#endif
    NSString * saveKey = [NSString stringWithFormat:@"%@_%@", KEY_APPSOTRE_CUSTOM_ORDER, IF_NIL_EMPTY(userName)];
    NSDictionary * dict = [[NSUserDefaults standardUserDefaults] objectForKey:saveKey];
    self.customExtras = dict ? [NSMutableDictionary dictionaryWithDictionary:dict]
                             : [NSMutableDictionary dictionary];
#if IAP_NEEDS_RETAIN
    [_customExtras retain];
#endif
}

- (void) saveCustomOrderDict
{
    if (!self.customExtras) return;
    NSString * userName = self.applicationUserName ?: @"null";
    NSString * saveKey = [NSString stringWithFormat:@"%@_%@", KEY_APPSOTRE_CUSTOM_ORDER, userName];
    [[NSUserDefaults standardUserDefaults] setObject:self.customExtras forKey:saveKey];
    [[NSUserDefaults standardUserDefaults] synchronize];
}

#pragma mark - Delegate Wrappers (统一兜底，避免 nil message 进入字典字面量崩溃)

- (void) reportInitError:(int)code msg:(NSString *)msg
{
    [self.delegate handleIAPInitError:code withMsg:IF_NIL_EMPTY(msg)];
}

- (void) reportPayError:(int)code msg:(NSString *)msg
{
    [self.delegate handleIAPPayError:code withMsg:IF_NIL_EMPTY(msg)];
}

- (void) reportRestoreError:(int)code msg:(NSString *)msg
{
    if (self.hasHandleRestoreTransactionFail) {
        [self.delegate handleIAPRestoreTransactionFail:code withMsg:IF_NIL_EMPTY(msg)];
    }
}

- (NSMutableDictionary *) ensureUnFinishedProducts {
    if (!self.unFinishedProducts) {
        self.unFinishedProducts = [NSMutableDictionary new];
#if IAP_NEEDS_RETAIN
        [_unFinishedProducts retain];
#endif
    }
    return self.unFinishedProducts;
}

- (NSMutableDictionary *) ensureUnFinishedTransactions {
    if (!self.unFinishedTransactions) {
        self.unFinishedTransactions = [NSMutableDictionary new];
#if IAP_NEEDS_RETAIN
        [_unFinishedTransactions retain];
#endif
    }
    return self.unFinishedTransactions;
}

#pragma mark - Init Products

- (void) initWithProductIds:(NSArray *)productIds {
    if (![SKPaymentQueue canMakePayments]) {
        [self reportInitError:ERROR_CODE_PAY_FAIL + 0 msg:@"您的手机没有打开程序内付费购买"];
        return;
    }
    
    BOOL dirty = NO;
    for (NSString * productId in productIds) {
        if (productId.length == 0) continue;
        if ([self.requestProductIds containsObject:productId]) continue;
        dirty = YES;
        [self.requestProductIds addObject:productId];
    }
    
    // 没有新增且没有正在进行的请求 -> 直接返回当前已知列表
    if (!dirty && !self.request) {
        [self.delegate handleIAPInitSuccess:self.productsPriceDict ?: @{}];
        return;
    }
    
    [self initProducts];
}

- (void) initProducts
{
    self.products = nil;
    
    if (self.request) {
        self.request.delegate = nil;
        [self.request cancel];
        self.request = nil;
    }
    
    if (self.requestProductIds.count == 0) return;
    
    self.request = [[SKProductsRequest alloc] initWithProductIdentifiers:self.requestProductIds];
    self.request.delegate = self;
    [self.request start];
}

- (SKProduct *) findProduct:(NSString *)productId
{
    if (productId.length == 0) return nil;
    for (SKProduct * product in self.products) {
        if ([product.productIdentifier isEqualToString:productId]) {
            return product;
        }
    }
    return nil;
}

#pragma mark - Buy

- (void) buyProductWithId:(NSString*) productId withExtra:(NSString *)extra
{
    if (productId.length == 0) {
        [self reportPayError:ERROR_CODE_PAY_FAIL + 10 msg:@"productId is empty"];
        return;
    }
    
    if (![SKPaymentQueue canMakePayments]) {
        [self reportPayError:ERROR_CODE_PAY_FAIL + 0 msg:@"您的手机没有打开程序内付费购买"];
        return;
    }
    
    NSString * safeExtra = extra ?: @"";
    
    if (![self.requestProductIds containsObject:productId]) {
        self.prePurchaseProductId = productId;
        self.prePurchaseExtra = safeExtra;
        [self.requestProductIds addObject:productId];
        [self initProducts];
        return;
    }
    
    if (self.request) {
        self.prePurchaseProductId = productId;
        self.prePurchaseExtra = safeExtra;
        return;
    }
    
    if (self.products == nil) {
        [self reportPayError:ERROR_CODE_PAY_FAIL + 1 msg:@"初始化未完成"];
        return;
    }
    
    if (self.purchasingProductId) {
        [self reportPayError:ERROR_CODE_PAY_FAIL + 2 msg:@"正在支付中，请稍后重试"];
        return;
    }
    
    if (self.unFinishedProducts && [[self.unFinishedProducts allValues] containsObject:productId]) {
        [self reportPayError:ERROR_CODE_PAY_FAIL + 3 msg:@"还有未处理的订单信息"];
        return;
    }
    
    SKProduct * findProduct = [self findProduct:productId];
    if (findProduct == nil) {
        [self reportPayError:ERROR_CODE_PAY_FAIL + 4 msg:@"未找到相应的商品"];
        return;
    }
    
    self.purchasingProductId = productId;
    [self.customExtras setObject:safeExtra forKey:productId];
    [self saveCustomOrderDict];
    
    SKMutablePayment * payment = [SKMutablePayment paymentWithProduct:findProduct];
    payment.applicationUsername = self.applicationUserName;
    [[SKPaymentQueue defaultQueue] addPayment:payment];
}

#pragma mark - Restore / Finish

- (void) restoreCompletedTransactions {
    if (self.applicationUserName.length > 0) {
        [[SKPaymentQueue defaultQueue] restoreCompletedTransactionsWithApplicationUsername:self.applicationUserName];
    } else {
        [[SKPaymentQueue defaultQueue] restoreCompletedTransactions];
    }
}

- (void) finishTransaction:(NSString*) transactionId {
    if (transactionId.length == 0) return;
    
    SKPaymentTransaction * transaction = self.unFinishedTransactions[transactionId];
    [self.unFinishedProducts removeObjectForKey:transactionId];
    [self.unFinishedTransactions removeObjectForKey:transactionId];
    
    if (transaction) {
        [self finishTransactionInternal:transaction success:true];
    }
}

- (void) finishTransactionInternal:(SKPaymentTransaction *)transaction success:(bool)success
{
    if (!transaction) return;
    
    NSString * productId = transaction.payment.productIdentifier;
    if (productId.length > 0 && self.customExtras && [self.customExtras hasKey:productId]) {
        [self.customExtras removeObjectForKey:productId];
        [self saveCustomOrderDict];
    }
    
    if (self.hasInvokePayResult) {
        SKProduct * product = [self findProduct:productId];
        int price = product ? product.price.intValue : 0;
        NSString * currency = (product && product.priceLocale.currencyCode) ? product.priceLocale.currencyCode : @"unknow";
        NSString * tId = transaction.transactionIdentifier;
        if (tId.length == 0) {
            int nowSecond = (int)[[NSDate date] timeIntervalSince1970];
            tId = [NSString stringWithFormat:@"unknow_%@_%d", IF_NIL_EMPTY(productId), nowSecond];
        }
        [self.delegate invokePayResult:tId
                              currency:currency
                                 price:price
                               success:success];
    }
    
    [[SKPaymentQueue defaultQueue] finishTransaction:transaction];
}

#pragma mark - Transaction Handling

- (void) onPaymentTransactionPurchased:(SKPaymentTransaction *) transaction {
    NSLog(@"onPaymentTransactionPurchased >>>>> self.user=%@ trans.user=%@ trans.id=%@ state=%ld",
          self.applicationUserName,
          transaction.payment.applicationUsername,
          transaction.transactionIdentifier,
          (long)transaction.transactionState);
    
    // 1) 跨用户订单：保留在队列里，等正确用户登录后再处理
    if (self.applicationUserName.length > 0) {
        NSString * transUserName = transaction.payment.applicationUsername;
        if (transUserName.length > 0 && ![self.applicationUserName isEqualToString:transUserName]) {
            NSLog(@"这是另一个用户的订单，当前用户名：%@，订单用户名：%@", self.applicationUserName, transUserName);
            return;
        }
    }
    
    // 2) 业务订单号：Restored 使用 originalTransaction 的 id
    SKPaymentTransaction * idSource = transaction;
    if (transaction.transactionState == SKPaymentTransactionStateRestored && transaction.originalTransaction) {
        idSource = transaction.originalTransaction;
    }
    NSString * transactionId = idSource.transactionIdentifier;
    NSString * productId     = transaction.payment.productIdentifier;
    
    if (transactionId.length == 0 || productId.length == 0) {
        // 不调用 finishTransaction，避免丢单；下次启动 StoreKit 会再次派发
        [self reportPayError:ERROR_CODE_PAY_FAIL + 9 msg:@"Empty transactionId/productId"];
        return;
    }
    
    // 3) 幂等：同一个 transactionId 重复回调时只刷新 transaction 引用，不再重复派发
    if (self.unFinishedTransactions[transactionId]) {
        self.unFinishedTransactions[transactionId] = transaction;
        return;
    }
    
    // 4) Receipt 读取失败一律不要 finish，避免用户付款丢单；下次启动会重试
    NSURL * appStoreReceiptUrl = [[NSBundle mainBundle] appStoreReceiptURL];
    if (!appStoreReceiptUrl) {
        [self reportPayError:ERROR_CODE_PAY_FAIL + 5 msg:@"Empty appStore receipt url"];
        return;
    }
    if (![[NSFileManager defaultManager] fileExistsAtPath:appStoreReceiptUrl.path]) {
        [self reportPayError:ERROR_CODE_PAY_FAIL + 6 msg:@"Receipt file not found"];
        return;
    }
    NSData * receiptData = [[NSFileManager defaultManager] contentsAtPath:appStoreReceiptUrl.path];
    if (receiptData.length == 0) {
        [self reportPayError:ERROR_CODE_PAY_FAIL + 7 msg:@"Receipt data is nil"];
        return;
    }
    
    // 5) 记录并回调
    [self ensureUnFinishedProducts];
    [self ensureUnFinishedTransactions];
    self.unFinishedTransactions[transactionId] = transaction;
    self.unFinishedProducts[transactionId]     = productId;
    
    NSString * customExtra = self.customExtras ? [self.customExtras optString:productId default:@""] : @"";
    NSString * receiptBase64 = [receiptData base64EncodedStringWithOptions:0] ?: @"";
    
    NSDictionary * result = @{
        @"transactionId": transactionId,
        @"productId":     productId,
        @"extra":         IF_NIL_EMPTY(customExtra),
        @"receipt":       receiptBase64
    };
    [self.delegate handleIAPPaySuccess:result];
}

- (void) checkCleanPurchasingProduct:(SKPaymentTransaction *)transaction
{
    if (self.applicationUserName.length > 0) {
        NSString * userName = transaction.payment.applicationUsername;
        if (userName.length > 0 && ![userName isEqualToString:self.applicationUserName]) {
            return;
        }
    }
    
    if ([transaction.payment.productIdentifier isEqualToString:self.purchasingProductId]) {
        self.purchasingProductId = nil;
    }
}

#pragma mark - SKProductsRequestDelegate

- (void)productsRequest:(nonnull SKProductsRequest *)request didReceiveResponse:(nonnull SKProductsResponse *)response {
    
    NSMutableDictionary * priceDict = [NSMutableDictionary new];
    NSNumberFormatter * numberFormatter = [NSNumberFormatter new];
    [numberFormatter setFormatterBehavior:NSNumberFormatterBehavior10_4];
    [numberFormatter setNumberStyle:NSNumberFormatterCurrencyStyle];
    
    for (SKProduct * product in response.products) {
        NSString * productId = product.productIdentifier;
        if (productId.length == 0) continue;
        
        [numberFormatter setLocale:product.priceLocale];
        NSString * formattedPrice = [numberFormatter stringForObjectValue:product.price] ?: @"";
        NSDecimalNumber * price = product.price ?: [NSDecimalNumber zero];
        NSString * currency = product.priceLocale.currencyCode ?: @"unknow";
        NSString * title = IF_NIL_EMPTY(product.localizedTitle);
        NSString * desc  = IF_NIL_EMPTY(product.localizedDescription);
        
        [priceDict setObject:@{
            @"productId":   productId,
            @"price":       price,
            @"currency":    currency,
            @"priceString": formattedPrice,
            @"title":       title,
            @"description": desc
        } forKey:productId];
        
        NSLog(@"item::%@(%@)[id=%@] price:%@", title, desc, productId, formattedPrice);
    }
    
    // 无效的 productId 从已请求集合中移除，允许下次重新请求
    if (response.invalidProductIdentifiers.count > 0) {
        NSLog(@"invalid product identifiers: %@", response.invalidProductIdentifiers);
        for (NSString * invalidId in response.invalidProductIdentifiers) {
            [self.requestProductIds removeObject:invalidId];
        }
    }
    
    // 仅在有有效结果时持久化，避免一次失败覆盖掉之前的缓存
    if (priceDict.count > 0) {
        NSString * saveKey = [NSString stringWithFormat:@"%@_%@", KEY_APPSTORE_PRODUCT_DICT, self.applicationUserName ?: @"null"];
        [[NSUserDefaults standardUserDefaults] setObject:priceDict forKey:saveKey];
        [[NSUserDefaults standardUserDefaults] synchronize];
    }
    
    if (request == self.request) {
        self.request = nil;
    }
    
    self.products = response.products;
    self.productsPriceDict = priceDict;
    
    [self.delegate handleIAPInitSuccess:priceDict];
    
    // 处理"先购买、后初始化完成"的延后购买
    if (self.prePurchaseProductId.length > 0) {
        NSString * pendingId = self.prePurchaseProductId;
        NSString * pendingExtra = self.prePurchaseExtra ?: @"";
        self.prePurchaseProductId = nil;
        self.prePurchaseExtra = nil;
        [self buyProductWithId:pendingId withExtra:pendingExtra];
    }
}

- (void)request:(SKRequest *)request didFailWithError:(NSError *)error {
    NSLog(@"productsRequest didFailWithError: %@", error);
    
    if (request == self.request) {
        self.request = nil;
    }
    
    [self reportInitError:ERROR_CODE_PAY_FAIL + 11 msg:error.localizedDescription];
    
    // 取消挂起的购买，避免永久不消费
    if (self.prePurchaseProductId.length > 0) {
        [self reportPayError:ERROR_CODE_PAY_FAIL + 12 msg:@"products request failed"];
        self.prePurchaseProductId = nil;
        self.prePurchaseExtra = nil;
    }
}

#pragma mark - SKPaymentTransactionObserver

- (void)paymentQueue:(SKPaymentQueue *)queue updatedTransactions:(NSArray<SKPaymentTransaction *> *)transactions {
    for (SKPaymentTransaction * transaction in transactions) {
        switch (transaction.transactionState) {
            case SKPaymentTransactionStatePurchasing:
                NSLog(@"用户正在购买 productId=%@", transaction.payment.productIdentifier);
                break;
            case SKPaymentTransactionStatePurchased:
                [self onPaymentTransactionPurchased:transaction];
                [self checkCleanPurchasingProduct:transaction];
                break;
            case SKPaymentTransactionStateFailed: {
                NSString * errMsg = transaction.error.localizedDescription ?: @"transaction failed";
                NSLog(@"购买失败 error: %@", errMsg);
                [self finishTransactionInternal:transaction success:false];
                [self checkCleanPurchasingProduct:transaction];
                [self reportPayError:ERROR_CODE_PAY_FAIL + 8 msg:errMsg];
                break;
            }
            case SKPaymentTransactionStateRestored:
                NSLog(@"恢复购买 originalId=%@", transaction.originalTransaction.transactionIdentifier);
                [self onPaymentTransactionPurchased:transaction];
                [self checkCleanPurchasingProduct:transaction];
                break;
            case SKPaymentTransactionStateDeferred:
                NSLog(@"最终状态未确定 productId=%@", transaction.payment.productIdentifier);
                [self checkCleanPurchasingProduct:transaction];
                break;
            default:
                break;
        }
    }
}

// 恢复成功的回调 ✅
- (void)paymentQueue:(SKPaymentQueue *)queue paymentQueueRestoreCompletedTransactionsFinished:(id) identifier {
    NSLog(@"restore completed:::: %@", queue.transactions.count);
    [self.delegate handleIAPRestoreTransactionSuccess:queue.transactions.count];
}

// 恢复失败的回调 ❌
- (void)paymentQueue:(SKPaymentQueue *)queue restoreCompletedTransactionsFailedWithError:(NSError *)error {
    NSLog(@"restore error:::: %@", error);
    [self reportRestoreError:ERROR_CODE_PAY_FAIL msg:error.localizedDescription];
}

@end
