//
//  IAPManagerProtocol.h
//  basketball-mobile
//
//  Created by macbook on 2021/10/12.
//

#import <StoreKit/StoreKit.h>

#pragma once

@protocol IAPManagerProtocol <SKPaymentTransactionObserver, SKProductsRequestDelegate>
- (void) initWithProductConfigs:(NSArray<NSString *> *)configs;
- (void) initWithProductIds:(NSArray*) prductIds;
- (void) buyProductWithId:(NSString*) productId withExtra:(NSString *)extra;
- (void) finishTransaction:(NSString*) transactionId;
- (void) restoreCompletedTransactions;
- (void) setApplicationUserName:(NSString *) applicationUserName;
@end
