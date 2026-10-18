// Cihazın BÖLGE ayarındaki ülke kodu ("TR", "US"…). DeviceCountry.cs DllImport ile çağırır.
// Dönen C dizgisi malloc'lanır; IL2CPP marshaler'ı serbest bırakır.
#import <Foundation/Foundation.h>

extern "C" const char* TF_GetCountryCode()
{
    NSString* code = [[NSLocale currentLocale] objectForKey:NSLocaleCountryCode];
    if (code == nil || code.length == 0) return NULL;
    const char* s = [code UTF8String];
    char* copy = (char*)malloc(strlen(s) + 1);
    strcpy(copy, s);
    return copy;
}
