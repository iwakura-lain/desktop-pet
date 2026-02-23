/**
 * DesktopPetBridge.mm
 * Objective-C bridge for macOS desktop pet window management.
 * Provides transparent window, click-through toggle, always-on-top,
 * cursor position, mouse button state, and menu bar status item.
 *
 * Called from C# via [DllImport("__Internal")] on macOS builds.
 *
 * TRANSPARENCY STRATEGY:
 * Unity's Metal framebuffer pixel format is fixed at creation time.
 * We use +load to swizzle CAMetalLayer's init so every layer Unity
 * creates is born with opaque=NO and a transparent pixel format.
 * This runs before any Unity C# code, before the first frame renders.
 */

#import <Cocoa/Cocoa.h>
#import <CoreGraphics/CoreGraphics.h>
#import <QuartzCore/CAMetalLayer.h>
#import <QuartzCore/QuartzCore.h>
#import <objc/runtime.h>

// ---------------------------------------------------------------------------
// Early-init: swizzle CAMetalLayer so every layer Unity creates is transparent
// ---------------------------------------------------------------------------
@interface CAMetalLayer (UnityTransparency)
+ (void)load;
- (instancetype)unity_init;
@end

@implementation CAMetalLayer (UnityTransparency)

+ (void)load
{
    // Swizzle -init so every CAMetalLayer starts transparent
    Method original = class_getInstanceMethod([CAMetalLayer class], @selector(init));
    Method swizzled = class_getInstanceMethod([CAMetalLayer class], @selector(unity_init));
    if (original && swizzled)
        method_exchangeImplementations(original, swizzled);
}

- (instancetype)unity_init
{
    // Call original init (now named unity_init due to swizzle)
    self = [self unity_init];
    if (self)
    {
        self.opaque = NO;
        self.backgroundColor = CGColorGetConstantColor(kCGColorClear);
        // MTLPixelFormatBGRA8Unorm = 80, supports alpha channel
        self.pixelFormat = MTLPixelFormatBGRA8Unorm;
        self.framebufferOnly = NO;
    }
    return self;
}

@end

// ---------------------------------------------------------------------------
// Helper: get the main Unity window
// ---------------------------------------------------------------------------
static NSWindow* GetUnityWindow()
{
    return [[NSApplication sharedApplication] mainWindow];
}

// ---------------------------------------------------------------------------
// Helper: recursively make all layers in view hierarchy transparent
// ---------------------------------------------------------------------------
static void MakeMetalLayerTransparent(NSView* view)
{
    if (!view) return;
    [view setWantsLayer:YES];
    CALayer* layer = view.layer;
    if (layer)
    {
        layer.opaque = NO;
        layer.backgroundColor = CGColorGetConstantColor(kCGColorClear);
        if ([layer isKindOfClass:[CAMetalLayer class]])
        {
            CAMetalLayer* ml = (CAMetalLayer*)layer;
            ml.opaque = NO;
            ml.pixelFormat = MTLPixelFormatBGRA8Unorm;
            ml.framebufferOnly = NO;
        }
    }
    for (NSView* sub in view.subviews)
        MakeMetalLayerTransparent(sub);
}

// ---------------------------------------------------------------------------
// Window style — transparent, borderless, always on top
// ---------------------------------------------------------------------------
extern "C" void MacOS_ApplyWindowStyle()
{
    NSWindow* win = GetUnityWindow();
    if (!win) return;

    [win setOpaque:NO];
    [win setBackgroundColor:[NSColor clearColor]];
    [win setHasShadow:NO];
    [win setStyleMask:NSWindowStyleMaskBorderless];
    [win setLevel:NSFloatingWindowLevel];
    [win setIgnoresMouseEvents:NO];

    // Belt-and-suspenders: also fix any layers already created
    MakeMetalLayerTransparent([win contentView]);
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

    g_statusItem = [[[NSStatusBar systemStatusBar]
                     statusItemWithLength:NSSquareStatusItemLength] retain];

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
        [g_statusItem release];
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
