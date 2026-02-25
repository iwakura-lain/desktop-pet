/**
 * DesktopPetBridge.mm
 * Objective-C bridge for macOS desktop pet window management.
 *
 * TRANSPARENCY STRATEGY:
 * 1. Swizzle CAMetalLayer's -setOpaque: to always force NO, so Unity can
 *    never reset the Metal layer back to opaque after we patch it.
 * 2. Observe NSWindowDidOrderOnScreenNotification to patch the window as
 *    soon as it appears.
 * 3. Retry-loop via dispatch_after to handle the race between dylib load
 *    and Unity creating its Metal window.
 *
 * preserveFramebufferAlpha must be 1 in ProjectSettings.asset.
 */

#import <Cocoa/Cocoa.h>
#import <CoreGraphics/CoreGraphics.h>
#import <QuartzCore/CAMetalLayer.h>
#import <QuartzCore/QuartzCore.h>
#import <objc/runtime.h>
#import <Metal/Metal.h>

// ---------------------------------------------------------------------------
// Diagnostics: write to ~/Desktop/desktop-pet-diag.log
// ---------------------------------------------------------------------------
static void DiagLog(NSString* msg)
{
    NSString* path = [NSHomeDirectory() stringByAppendingPathComponent:@"Desktop/desktop-pet-diag.log"];
    NSString* line = [NSString stringWithFormat:@"%@  %@\n",
        [NSDateFormatter localizedStringFromDate:[NSDate date]
            dateStyle:NSDateFormatterNoStyle
            timeStyle:NSDateFormatterMediumStyle],
        msg];
    NSFileHandle* fh = [NSFileHandle fileHandleForWritingAtPath:path];
    if (!fh) {
        [@"" writeToFile:path atomically:NO encoding:NSUTF8StringEncoding error:nil];
        fh = [NSFileHandle fileHandleForWritingAtPath:path];
    }
    [fh seekToEndOfFile];
    [fh writeData:[line dataUsingEncoding:NSUTF8StringEncoding]];
    [fh closeFile];
}

// ---------------------------------------------------------------------------
// Swizzle CAMetalLayer -setOpaque: — intercept any attempt to set opaque=YES
// ---------------------------------------------------------------------------
@interface CAMetalLayer (ForceTransparent)
- (void)forceTransparent_setOpaque:(BOOL)opaque;
@end

@implementation CAMetalLayer (ForceTransparent)
- (void)forceTransparent_setOpaque:(BOOL)opaque
{
    // Always call through with NO, regardless of what Unity requests
    [self forceTransparent_setOpaque:NO];
}
@end

static void InstallCAMetalLayerSwizzle()
{
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        Class cls = [CAMetalLayer class];
        SEL original = @selector(setOpaque:);
        SEL replacement = @selector(forceTransparent_setOpaque:);
        Method origMethod = class_getInstanceMethod(cls, original);
        Method replMethod = class_getInstanceMethod(cls, replacement);
        if (origMethod && replMethod) {
            method_exchangeImplementations(origMethod, replMethod);
            DiagLog(@"[SWIZZLE] CAMetalLayer.setOpaque: swizzled OK");
        } else {
            DiagLog(@"[SWIZZLE] FAILED to swizzle CAMetalLayer.setOpaque:");
        }
    });
}

// ---------------------------------------------------------------------------
// Apply transparency to window + all Metal layers
// ---------------------------------------------------------------------------
static void ApplyTransparencyToWindow(NSWindow* win)
{
    if (!win) return;

    DiagLog([NSString stringWithFormat:@"[APPLY] win=%@ opaque_before=%d", win, (int)[win isOpaque]]);

    [win setOpaque:NO];
    [win setBackgroundColor:[NSColor clearColor]];
    [win setHasShadow:NO];

    DiagLog([NSString stringWithFormat:@"[APPLY] after setOpaque:NO => isOpaque=%d", (int)[win isOpaque]]);

    NSView* root = [win contentView];
    if (!root) return;

    // Recursively walk every view and fix its layer
    NSMutableArray* stack = [NSMutableArray arrayWithObject:root];
    while (stack.count > 0)
    {
        NSView* v = stack.lastObject;
        [stack removeLastObject];

        [v setWantsLayer:YES];
        v.layer.opaque = NO;
        v.layer.backgroundColor = CGColorGetConstantColor(kCGColorClear);

        if ([v.layer isKindOfClass:[CAMetalLayer class]])
        {
            CAMetalLayer* ml = (CAMetalLayer*)v.layer;
            DiagLog([NSString stringWithFormat:@"[METAL] found CAMetalLayer opaque_before=%d", (int)ml.isOpaque]);
            // Only set opaque=NO. Do NOT touch pixelFormat or framebufferOnly:
            // changing those during a CATransaction flush crashes the app.
            ml.opaque = NO;
            DiagLog([NSString stringWithFormat:@"[METAL] after patch: opaque=%d", (int)ml.isOpaque]);
        }

        for (NSView* child in v.subviews)
            [stack addObject:child];
    }
}

// Use +load on a dummy class to install swizzles at library load time
@interface UnityTransparencyInstaller : NSObject
@end

// Forward declaration
static NSWindow* GetUnityWindow();

// Try to apply transparency, retrying up to maxAttempts times with a short delay.
// This handles the race between dylib load and Unity creating its Metal window.
static void TryApplyTransparencyWithRetry(int attempt, int maxAttempts)
{
    NSWindow* win = GetUnityWindow();
    if (win)
    {
        // Defer layer mutations out of any active CATransaction flush
        dispatch_async(dispatch_get_main_queue(), ^{
            ApplyTransparencyToWindow(win);
        });
        return;
    }
    if (attempt >= maxAttempts) return;

    dispatch_after(
        dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.05 * NSEC_PER_SEC)),
        dispatch_get_main_queue(),
        ^{ TryApplyTransparencyWithRetry(attempt + 1, maxAttempts); }
    );
}

@implementation UnityTransparencyInstaller

+ (void)load
{
    DiagLog(@"[LOAD] UnityTransparencyInstaller +load called");
    // Install CAMetalLayer swizzle first — must happen before Unity creates
    // its Metal layer so any subsequent setOpaque:YES calls are intercepted.
    InstallCAMetalLayerSwizzle();

    // NSWindowDidOrderOnScreenNotification fires when any window becomes visible,
    // including click-through windows that never become key/main.
    // Use dispatch_async to defer layer mutations out of the CATransaction flush.
    [[NSNotificationCenter defaultCenter]
        addObserverForName:@"NSWindowDidOrderOnScreenNotification"
        object:nil
        queue:[NSOperationQueue mainQueue]
        usingBlock:^(NSNotification* n) {
            NSWindow* win = n.object;
            if (win) {
                dispatch_async(dispatch_get_main_queue(), ^{
                    ApplyTransparencyToWindow(win);
                });
            }
        }];

    // Also start a retry loop from launch in case the window is already visible.
    [[NSNotificationCenter defaultCenter]
        addObserverForName:NSApplicationDidFinishLaunchingNotification
        object:nil
        queue:[NSOperationQueue mainQueue]
        usingBlock:^(NSNotification* n) {
            // Retry up to 40 times (= 2s) at 50ms intervals
            TryApplyTransparencyWithRetry(0, 40);
        }];
}

@end

// ---------------------------------------------------------------------------
// Helper: get the Unity render window.
// Strategy: find the window with a CAMetalLayer first; if not found yet,
// fall back to the first visible, on-screen window (Unity's main window
// before the Metal layer is fully initialised).
// ---------------------------------------------------------------------------
static NSWindow* GetUnityWindow()
{
    NSArray<NSWindow*>* windows = [NSApplication sharedApplication].windows;

    // Pass 1: find the window that already has a CAMetalLayer
    for (NSWindow* win in windows)
    {
        NSView* root = [win contentView];
        if (!root) continue;
        NSMutableArray* stack = [NSMutableArray arrayWithObject:root];
        while (stack.count > 0)
        {
            NSView* v = stack.lastObject;
            [stack removeLastObject];
            if ([v.layer isKindOfClass:[CAMetalLayer class]])
                return win;
            for (NSView* child in v.subviews)
                [stack addObject:child];
        }
    }

    // Pass 2: fall back to the first visible on-screen window
    for (NSWindow* win in windows)
    {
        if ([win isVisible] && [win isOnActiveSpace])
            return win;
    }

    // Pass 3: last resort
    return [NSApplication sharedApplication].mainWindow;
}

// ---------------------------------------------------------------------------
// Window style — called from C# after Init (belt-and-suspenders)
// ---------------------------------------------------------------------------
static void ApplyWindowStyleNow()
{
    NSWindow* win = GetUnityWindow();
    DiagLog([NSString stringWithFormat:@"[STYLE] ApplyWindowStyleNow win=%@", win]);
    if (!win) return;

    ApplyTransparencyToWindow(win);
    [win setStyleMask:NSWindowStyleMaskBorderless];
    [win setLevel:NSFloatingWindowLevel];
    [win setIgnoresMouseEvents:NO];
    DiagLog([NSString stringWithFormat:@"[STYLE] done: styleMask=%lu level=%ld isOpaque=%d",
        (unsigned long)[win styleMask], (long)[win level], (int)[win isOpaque]]);
}

extern "C" void MacOS_ApplyWindowStyle()
{
    InstallCAMetalLayerSwizzle();  // ensure swizzle is active

    ApplyWindowStyleNow();

    // If window wasn't found yet, retry a few times with short delays.
    // This handles the case where MacOS_ApplyWindowStyle is called before
    // the NSWindow is fully on screen.
    for (int i = 1; i <= 5; i++)
    {
        dispatch_after(
            dispatch_time(DISPATCH_TIME_NOW, (int64_t)(i * 0.1 * NSEC_PER_SEC)),
            dispatch_get_main_queue(),
            ^{ ApplyWindowStyleNow(); }
        );
    }
}

// ---------------------------------------------------------------------------
// Click-through toggle
// ---------------------------------------------------------------------------
extern "C" void MacOS_SetIgnoreMouse(bool ignore)
{
    NSWindow* win = GetUnityWindow();
    if (win) [win setIgnoresMouseEvents:ignore ? YES : NO];
}

// ---------------------------------------------------------------------------
// Cursor position (screen coords, origin bottom-left like Unity)
// ---------------------------------------------------------------------------
extern "C" void MacOS_GetCursorPos(float* outX, float* outY)
{
    NSPoint p = [NSEvent mouseLocation];
    // NSEvent.mouseLocation is already in screen coords with Y up (bottom-left origin)
    // Unity also uses bottom-left origin for Screen, so no flip needed
    *outX = (float)p.x;
    *outY = (float)p.y;
}

// ---------------------------------------------------------------------------
// Mouse button state (0=left, 1=right)
// ---------------------------------------------------------------------------
extern "C" bool MacOS_IsMouseButtonDown(int button)
{
    NSUInteger buttons = [NSEvent pressedMouseButtons];
    if (button == 0) return (buttons & (1 << 0)) != 0;  // left
    if (button == 1) return (buttons & (1 << 1)) != 0;  // right
    return false;
}

// ---------------------------------------------------------------------------
// Move window (screen coords, origin bottom-left)
// ---------------------------------------------------------------------------
extern "C" void MacOS_MoveWindow(float x, float y, float w, float h)
{
    NSWindow* win = GetUnityWindow();
    if (!win) return;
    NSRect frame = NSMakeRect(x, y, w, h);
    [win setFrame:frame display:YES];
}

// ---------------------------------------------------------------------------
// Get window frame (screen coords, origin bottom-left)
// ---------------------------------------------------------------------------
extern "C" void MacOS_GetWindowRect(float* outX, float* outY, float* outW, float* outH)
{
    NSWindow* win = GetUnityWindow();
    if (!win) { *outX = *outY = *outW = *outH = 0; return; }
    NSRect f = [win frame];
    *outX = (float)f.origin.x;
    *outY = (float)f.origin.y;
    *outW = (float)f.size.width;
    *outH = (float)f.size.height;
}

// ---------------------------------------------------------------------------
// Screen size
// ---------------------------------------------------------------------------
extern "C" int MacOS_GetScreenWidth()
{
    return (int)[[NSScreen mainScreen] frame].size.width;
}

extern "C" int MacOS_GetScreenHeight()
{
    return (int)[[NSScreen mainScreen] frame].size.height;
}

// ---------------------------------------------------------------------------
// Menu bar status item (replaces Windows system tray)
// ---------------------------------------------------------------------------
static NSStatusItem* g_statusItem = nil;

// Callback function pointer set from C#
typedef void (*StatusItemCallback)(int action);  // 1=left click, 2=right click
static StatusItemCallback g_statusCallback = nullptr;

@interface StatusItemDelegate : NSObject
- (void)handleClick:(id)sender;
- (void)handleRightClick:(id)sender;
@end

@implementation StatusItemDelegate
- (void)handleClick:(id)sender {
    if (g_statusCallback) g_statusCallback(1);
}
- (void)handleRightClick:(id)sender {
    if (g_statusCallback) g_statusCallback(2);
}
@end

static StatusItemDelegate* g_delegate = nil;

extern "C" void MacOS_CreateStatusItem(const char* tooltip)
{
    if (g_statusItem) return;

    g_statusItem = [[NSStatusBar systemStatusBar]
                     statusItemWithLength:NSSquareStatusItemLength];

    // Draw a small white circle as icon
    NSImage* icon = [[NSImage alloc] initWithSize:NSMakeSize(16, 16)];
    [icon lockFocus];
    [[NSColor whiteColor] setFill];
    [[NSBezierPath bezierPathWithOvalInRect:NSMakeRect(2, 2, 12, 12)] fill];
    [icon unlockFocus];
    [icon setTemplate:YES];

    g_statusItem.button.image = icon;
    g_statusItem.button.toolTip = tooltip ? [NSString stringWithUTF8String:tooltip] : @"Desktop Pet";

    g_delegate = [[StatusItemDelegate alloc] init];
    g_statusItem.button.target = g_delegate;
    g_statusItem.button.action = @selector(handleClick:);

    // Allow right-click via NSButton sendActionOn
    [g_statusItem.button sendActionOn:NSEventMaskLeftMouseUp | NSEventMaskRightMouseUp];
}

extern "C" void MacOS_SetStatusItemCallback(StatusItemCallback callback)
{
    g_statusCallback = callback;
}

extern "C" void MacOS_RemoveStatusItem()
{
    if (g_statusItem)
    {
        [[NSStatusBar systemStatusBar] removeStatusItem:g_statusItem];
        g_statusItem = nil;
    }
}

// ---------------------------------------------------------------------------
// Window visibility (hide/show, used by tray hide-to-tray)
// ---------------------------------------------------------------------------
extern "C" void MacOS_SetWindowVisible(bool visible)
{
    NSWindow* win = GetUnityWindow();
    if (!win) return;
    if (visible)
        [win orderFront:nil];
    else
        [win orderOut:nil];
}
