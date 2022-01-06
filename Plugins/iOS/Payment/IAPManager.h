//
//  IAPManager.h
//  basketball
//
//  Created by macbook on 2021/10/12.
//

#ifndef IAPManager_h
#define IAPManager_h

#import "IAPDelegateProtocol.h"
#import "IAPManagerProtocol.h"

@interface IAPManager : NSObject <IAPManagerProtocol>
+ (bool) enablePay;
- (id) initWithDelegate:(NSObject<IAPDelegateProtocol> *)delegate preset:(NSString *)applicationUserName;
@end

#endif /* IAPManager_h */
