#if !UNITY_EDITOR && UNITY_IOS

using System.Runtime.InteropServices;

namespace PluginSet.Apple
{
    public static class AppleIosHelper
    {
#if ENABLE_APPLE_LOGIN
        [DllImport("__Internal")]
        public static extern bool Plugin_Apple_IsEnableLogin();
        
        [DllImport("__Internal")]
        public static extern void Plugin_Apple_Login_With_AppleId();
        
        [DllImport("__Internal")]
        public static extern void Plugin_Apple_Login_With_Password();
#endif
        
        [DllImport("__Internal")]
        public static extern void Plugin_Apple_Store_Review();

#if ENABLE_APPLE_PURCHASE
        [DllImport("__Internal")]
        public static extern bool Plugin_Apple_Pay_Enable();
        
        [DllImport("__Internal")]
        public static extern void Plugin_Apple_Pay_Init_With_Products(string prodcuts);
        [DllImport("__Internal")]
        public static extern void Plugin_Apple_Buy_Prodcut_With_Product_Id(string productId,string extra);
        [DllImport("__Internal")]
        public static extern void Plugin_Apple_Finish_Transaction(string transactionId);
        [DllImport("__Internal")]
        public static extern void Plugin_Apple_Restore_Transactions();
#endif
    }
}
#endif
