/**
 * DesktopPetBridge.mm
 * Objective-C bridge for macOS desktop pet window management.
 * Provides transparent window, click-through toggle, always-on-top,
 * cursor position, mouse button state, and menu bar status item.
 *
 * Called from C# via [DllImport("__Internal")] on macOS builds.
 */

#import <Cocoa/Cocoa.h>
#import <CoreGraphics/CoreGraphics.h>
#import <objc/runtime.h>

// ---------------------------------------------------------------------------
// Helper: get the main Unity window
// ---------------------------------------------------------------------------
static NSWindow* GetUnityWindow()
{
    return [[NSApplication sharedApplication] mainWindow];
}

// ---------------------------------------------------------------------------
// Window style — transparent, borderless, always on top
// ---------------------------------------------------------------------------
extern "C" void MacOS_ApplyWindowStyle()
{
    NSWindow* win = GetUnityWindow();
    if (!win) return;

    // Transparent background
    [win setOpaque:NO];
    [win setBackgroundColor:[NSColor clearColor]];
    [win setHasShadow:NO];

    // Borderless
    [win setStyleMask:NSWindowStyleMaskBorderless];

    // Always on top (floating above normal windows)
    [win setLevel:NSFloatingWindowLevel];

    // Allow the window to receive mouse events by default
    [win setIgnoresMouseEvents:NO];

    // Make the Metal/OpenGL view itself transparent so Unity's clear color
    // (RGBA 0,0,0,0) shows through to the desktop instead of painting black.
    NSView* contentView = [win contentView];
    if (contentView)
    {
        [contentView setWantsLayer:YES];
        contentView.layer.backgroundColor = CGColorGetConstantColor(kCGColorClear);
        contentView.layer.opaque = NO;

        // Also make every subview (the actual MTKView) transparent
        for (NSView* sub in contentView.subviews)
        {
            [sub setWantsLayer:YES];
            sub.layer.backgroundColor = CGColorGetConstantColor(kCGColorClear);
            sub.layer.opaque = NO;
        }
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
