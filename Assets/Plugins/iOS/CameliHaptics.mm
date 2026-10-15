// Light haptic tap for Cameli Garden (Haptics.Tap in Assets/Scripts/Cameli/Game/Haptics.cs).
#import <UIKit/UIKit.h>

extern "C" void CameliHapticsLightTap(void)
{
    static UIImpactFeedbackGenerator *generator = nil;
    if (generator == nil)
        generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
    [generator impactOccurred];
    [generator prepare]; // keeps the Taptic Engine ready, so the next tap has no delay
}
