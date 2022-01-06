//
//  IAPManagerProtocol.h
//  basketball-mobile
//
//  Created by macbook on 2021/10/12.
//

#import <StoreKit/StoreKit.h>

#pragma once

@protocol IAPDelegateProtocol
- (void) handleIAPInitError:(int)errorCode withMsg:(NSString*) message;
- (void) handleIAPInitSuccess:(NSDictionary*)priceDict;
- (void) handleIAPPayError:(int)errorCode withMsg:(NSString*)message;
- (void) handleIAPPaySuccess:(NSDictionary*) result;

@optional
- (void) invokePayResult:(NSString *)payId currency:(NSString *)name price:(int)amount success:(bool)success;
@end
