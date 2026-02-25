/**
 * DesktopPetBridge.mm
 * Objective-C bridge for macOS desktop pet window management.
 *
 * TRANSPARENCY STRATEGY:
 * Setting NSWindow opaque=NO + backgroundColor=clear is sufficient for
 * Unity Metal to render with a transparent background, provided that
 * preserveFramebufferAlpha=1 is set in ProjectSettings.asset.
 *
 * We do NOT swizzle CAMetalLayer or walk the view hierarchy — those
 * operations during CATransaction flush callbacks crash the app.
 */

#import <Cocoa/Cocoa.h>
#import <objc/runtime.h>

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
// Apply transparency — NSWindow level only, no layer traversal
// ---------------------------------------------------------------------------
static void ApplyTransparencyToWindow(NSWindow* win)
{
    if (!win) return;
    DiagLog([NSString stringWithFormat:@"[APPLY] opaque_before=%d bg=%@ styleMask=%lu level=%ld",
        (int)[win isOpaque],
        [win backgroundColor],
        (unsigned long)[win styleMask],
        (long)[win level]]);
    [win setOpaque:NO];
    [win setBackgroundColor:[NSColor clearColor]];
    [win setHasShadow:NO];
    DiagLog([NSString stringWithFormat:@"[APPLY] opaque_after=%d hasShadow=%d",
        (int)[win isOpaque], (int)[win hasShadow]]);
}

// ---------------------------------------------------------------------------
// Helper: find the Unity render window
// ---------------------------------------------------------------------------
static NSWindow* GetUnityWindow()
{
    NSArray<NSWindow*>* windows = [NSApplication sharedApplication].windows;
    // Prefer a window that is visible and on screen
    for (NSWindow* win in windows) {
        if ([win isVisible] && [win isOnActiveSpace])
            return win;
    }
    return [NSApplication sharedApplication].mainWindow;
}

// ---------------------------------------------------------------------------
// Retry applying transparency until the window exists
// ---------------------------------------------------------------------------
static void TryApplyWithRetry(int attempt, int maxAttempts)
{
    NSWindow* win = GetUnityWindow();
    if (win) {
        ApplyTransparencyToWindow(win);
        return;
    }
    if (attempt >= maxAttempts) {
        DiagLog(@"[RETRY] gave up after max attempts");
        return;
    }
    dispatch_after(
        dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.1 * NSEC_PER_SEC)),
        dispatch_get_main_queue(),
        ^{ TryApplyWithRetry(attempt + 1, maxAttempts); }
    );
}

// ---------------------------------------------------------------------------
// +load: register for window-appears notification only
// ---------------------------------------------------------------------------
@interface UnityTransparencyInstaller : NSObject
@end

@implementation UnityTransparencyInstaller
+ (void)load
{
    DiagLog(@"[LOAD] +load called");
    [[NSNotificationCenter defaultCenter]
        addObserverForName:NSWindowDidBecomeKeyNotification
        object:nil
        queue:[NSOperationQueue mainQueue]
        usingBlock:^(NSNotification* n) {
            NSWindow* win = n.object;
            if (win) ApplyTransparencyToWindow(win);
        }];
    [[NSNotificationCenter defaultCenter]
        addObserverForName:NSApplicationDidFinishLaunchingNotification
        object:nil
        queue:[NSOperationQueue mainQueue]
        usingBlock:^(NSNotification* n) {
            TryApplyWithRetry(0, 30);
        }];
}
@end

// ---------------------------------------------------------------------------
// Window style — called from C# on startup
// ---------------------------------------------------------------------------
extern "C" void MacOS_ApplyWindowStyle()
{
    DiagLog(@"[STYLE] MacOS_ApplyWindowStyle called");
    NSWindow* win = GetUnityWindow();
    DiagLog([NSString stringWithFormat:@"[STYLE] GetUnityWindow=%@ windowCount=%lu",
        win, (unsigned long)[NSApplication sharedApplication].windows.count]);
    if (win) {
        ApplyTransparencyToWindow(win);
        [win setStyleMask:NSWindowStyleMaskBorderless];
        [win setLevel:NSFloatingWindowLevel];
        // Log contentView and layer state
        NSView* cv = [win contentView];
        DiagLog([NSString stringWithFormat:@"[STYLE] done isOpaque=%d styleMask=%lu level=%ld contentView=%@ wantsLayer=%d layer=%@",
            (int)[win isOpaque],
            (unsigned long)[win styleMask],
            (long)[win level],
            cv,
            (int)[cv wantsLayer],
            [cv layer]]);
    }
    // Also schedule retries in case the window isn't ready yet
    for (int i = 1; i <= 5; i++) {
        dispatch_after(
            dispatch_time(DISPATCH_TIME_NOW, (int64_t)(i * 0.2 * NSEC_PER_SEC)),
            dispatch_get_main_queue(),
            ^{
                NSWindow* w = GetUnityWindow();
                if (w) {
                    ApplyTransparencyToWindow(w);
                    DiagLog([NSString stringWithFormat:@"[RETRY %d] isOpaque=%d", i, (int)[w isOpaque]]);
                }
            }
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
    *outX = (float)p.x;
    *outY = (float)p.y;
}

// ---------------------------------------------------------------------------
// Mouse button state (0=left, 1=right)
// ---------------------------------------------------------------------------
extern "C" bool MacOS_IsMouseButtonDown(int button)
{
    NSUInteger buttons = [NSEvent pressedMouseButtons];
    if (button == 0) return (buttons & (1 << 0)) != 0;
    if (button == 1) return (buttons & (1 << 1)) != 0;
    return false;
}

// ---------------------------------------------------------------------------
// Move window (screen coords, origin bottom-left)
// ---------------------------------------------------------------------------
extern "C" void MacOS_MoveWindow(float x, float y, float w, float h)
{
    NSWindow* win = GetUnityWindow();
    if (!win) return;
    [win setFrame:NSMakeRect(x, y, w, h) display:YES];
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
// Menu bar status item
// ---------------------------------------------------------------------------
static NSStatusItem* g_statusItem = nil;
typedef void (*StatusItemCallback)(int action);
static StatusItemCallback g_statusCallback = nullptr;

@interface StatusItemDelegate : NSObject
- (void)handleClick:(id)sender;
@end

@implementation StatusItemDelegate
- (void)handleClick:(id)sender {
    if (g_statusCallback) g_statusCallback(1);
}
@end

static StatusItemDelegate* g_delegate = nil;

extern "C" void MacOS_CreateStatusItem(const char* tooltip)
{
    if (g_statusItem) return;
    g_statusItem = [[NSStatusBar systemStatusBar]
                     statusItemWithLength:NSSquareStatusItemLength];

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
    [g_statusItem.button sendActionOn:NSEventMaskLeftMouseUp | NSEventMaskRightMouseUp];
}

extern "C" void MacOS_SetStatusItemCallback(StatusItemCallback callback)
{
    g_statusCallback = callback;
}

extern "C" void MacOS_RemoveStatusItem()
{
    if (g_statusItem) {
        [[NSStatusBar systemStatusBar] removeStatusItem:g_statusItem];
        g_statusItem = nil;
    }
}

// ---------------------------------------------------------------------------
// Window visibility
// ---------------------------------------------------------------------------
extern "C" void MacOS_SetWindowVisible(bool visible)
{
    NSWindow* win = GetUnityWindow();
    if (!win) return;
    if (visible) [win orderFront:nil];
    else         [win orderOut:nil];
}
