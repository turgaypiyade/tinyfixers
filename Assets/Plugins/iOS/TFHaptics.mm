// Kısa dokunsal geri bildirim (Taptic). Haptics.cs DllImport ile çağırır.
// style: 0 = hafif, 1 = orta, 2 = sert.
// Birincil yol Core Haptics (CHHapticEngine): view'a ve iOS 17.5'te kullanımdan kalkan view'sız
// UIImpactFeedbackGenerator init'ine bağlı değil (2026-10-28: iPhone 17 Pro Max / iOS 26'da eski yol titremedi).
// Core Haptics yoksa view'a bağlı UIImpactFeedbackGenerator'a düşer. Taptic olmayan cihazda (iPad) sessizdir.
// [TFHaptics] logları Xcode konsolunda hangi yolun çalıştığını gösterir.
#import <UIKit/UIKit.h>
#import <CoreHaptics/CoreHaptics.h>

extern "C" UIView* UnityGetGLView(void);

static CHHapticEngine* tfEngine = nil;
static BOOL tfEngineFailed = NO;
static UIImpactFeedbackGenerator* tfGenerators[3];
// Son oynatıcı KALICI tutulur: yerel değişken fonksiyon dönünce serbest kalıp titreşimi kesebiliyor.
static id tfLastPlayer = nil;
// Three reusable transient players: no pattern/player construction per broken cell.
static id tfImpactPlayers[3];
static id tfLastRumble = nil;

static CHHapticEngine* TFEngine(void)
{
    if (tfEngine != nil || tfEngineFailed) return tfEngine;
    if (@available(iOS 13.0, *))
    {
        if (!CHHapticEngine.capabilitiesForHardware.supportsHaptics)
        {
            NSLog(@"[TFHaptics] Core Haptics desteklenmiyor; UIFeedbackGenerator kullanılacak");
            tfEngineFailed = YES;
            return nil;
        }
        NSError* error = nil;
        CHHapticEngine* engine = [[CHHapticEngine alloc] initAndReturnError:&error];
        if (engine == nil)
        {
            NSLog(@"[TFHaptics] CHHapticEngine oluşturulamadı: %@", error);
            tfEngineFailed = YES;
            return nil;
        }
        engine.playsHapticsOnly = YES;          // oyunun ses oturumuna dokunma
        engine.autoShutdownEnabled = YES;       // boştayken motoru kapat (pil)
        engine.resetHandler = ^{
            dispatch_async(dispatch_get_main_queue(), ^{
                for (int index = 0; index < 3; index++) tfImpactPlayers[index] = nil;
                tfLastPlayer = nil;
                tfLastRumble = nil;
                NSError* restartError = nil;
                [tfEngine startAndReturnError:&restartError];
            });
        };
        tfEngine = engine;
        NSLog(@"[TFHaptics] Core Haptics hazır");
    }
    else
    {
        tfEngineFailed = YES;
    }
    return tfEngine;
}

static BOOL TFPlayCoreHaptics(int style)
{
    if (@available(iOS 13.0, *))
    {
        CHHapticEngine* engine = TFEngine();
        if (engine == nil) return NO;

        int index = style < 0 ? 0 : (style > 2 ? 2 : style);
        NSError* error = nil;
        if (![engine startAndReturnError:&error]) return NO;
        id<CHHapticPatternPlayer> player = tfImpactPlayers[index];
        if (player == nil)
        {
            float intensity = index == 2 ? 1.0f : index == 1 ? 0.75f : 0.45f;
            float sharpness = index == 2 ? 0.55f : index == 1 ? 0.5f : 0.65f;
            CHHapticEventParameter* i = [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity value:intensity];
            CHHapticEventParameter* s = [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness value:sharpness];
            CHHapticEvent* event = [[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticTransient parameters:@[i, s] relativeTime:0];
            CHHapticPattern* pattern = [[CHHapticPattern alloc] initWithEvents:@[event] parameters:@[] error:&error];
            if (pattern == nil) return NO;
            player = [engine createPlayerWithPattern:pattern error:&error];
            if (player == nil) return NO;
            tfImpactPlayers[index] = player;
        }
        if (![player startAtTime:CHHapticTimeImmediate error:&error])
        {
            tfImpactPlayers[index] = nil; // engine reset/interruption: rebuild on the next eligible impact
            return NO;
        }
        tfLastPlayer = player;
        return YES;
    }
    return NO;
}

static void TFPlayFeedbackGenerator(int style)
{
    int idx = style < 0 ? 0 : (style > 2 ? 2 : style);
    UIImpactFeedbackStyle s = idx == 2 ? UIImpactFeedbackStyleHeavy
                            : idx == 1 ? UIImpactFeedbackStyleMedium
                                       : UIImpactFeedbackStyleLight;
    UIView* view = UnityGetGLView();
    if (tfGenerators[idx] == nil)
    {
        if (@available(iOS 17.5, *))
            tfGenerators[idx] = view != nil ? [UIImpactFeedbackGenerator feedbackGeneratorWithStyle:s forView:view]
                                            : [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
        else
            tfGenerators[idx] = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
        [tfGenerators[idx] prepare];
    }
    UIImpactFeedbackGenerator* generator = tfGenerators[idx];
    if (@available(iOS 17.5, *))
    {
        if (view != nil) [generator impactOccurredAtLocation:CGPointMake(CGRectGetMidX(view.bounds), CGRectGetMidY(view.bounds))];
        else [generator impactOccurred];
    }
    else
    {
        [generator impactOccurred];
    }
    [generator prepare];
}

extern "C" void TF_HapticImpact(int style)
{
    void (^fire)(void) = ^{
        if (!TFPlayCoreHaptics(style))
            TFPlayFeedbackGenerator(style);
    };
    if ([NSThread isMainThread]) fire();
    else dispatch_async(dispatch_get_main_queue(), fire);
}

// Sürekli titreşim (roket kombosu boyunca). Core Haptics yoksa tek sert vuruşa düşer.
extern "C" void TF_HapticContinuous(float intensity, float duration)
{
    void (^fire)(void) = ^{
        if (@available(iOS 13.0, *))
        {
            CHHapticEngine* engine = TFEngine();
            if (engine != nil)
            {
                CHHapticEventParameter* i = [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity value:intensity];
                CHHapticEventParameter* s = [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness value:0.3f];
                CHHapticEvent* event = [[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticContinuous
                                                                     parameters:@[i, s]
                                                                   relativeTime:0
                                                                       duration:duration];
                NSError* error = nil;
                CHHapticPattern* pattern = [[CHHapticPattern alloc] initWithEvents:@[event] parameters:@[] error:&error];
                id<CHHapticPatternPlayer> player = pattern != nil && [engine startAndReturnError:&error]
                    ? [engine createPlayerWithPattern:pattern error:&error] : nil;
                if (player != nil && [player startAtTime:CHHapticTimeImmediate error:&error])
                {
                    tfLastRumble = player;
                    return;
                }
                NSLog(@"[TFHaptics] sürekli titreşim hatası: %@", error);
            }
        }
        TFPlayFeedbackGenerator(2);
    };
    if ([NSThread isMainThread]) fire();
    else dispatch_async(dispatch_get_main_queue(), fire);
}

// Settings-off / board teardown: stop active patterns immediately, retain reusable players.
extern "C" void TF_HapticCancel(void)
{
    void (^cancel)(void) = ^{
        if (@available(iOS 13.0, *))
        {
            for (int index = 0; index < 3; index++)
            {
                id<CHHapticPatternPlayer> player = tfImpactPlayers[index];
                [player stopAtTime:CHHapticTimeImmediate error:nil];
            }
            id<CHHapticPatternPlayer> rumble = tfLastRumble;
            [rumble stopAtTime:CHHapticTimeImmediate error:nil];
            tfLastRumble = nil;
        }
    };
    if ([NSThread isMainThread]) cancel();
    else dispatch_async(dispatch_get_main_queue(), cancel);
}
