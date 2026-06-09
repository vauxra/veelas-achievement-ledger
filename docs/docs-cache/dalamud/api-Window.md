# api-Window

Source: https://dalamud.dev/api/Dalamud.Interface.Windowing/Classes/Window

Fetched: 2026-06-09T02:42:42.570435+00:00

SHA256: `98aef90b0e7161d60d3a06b10d2af4a9bb4a7e4faf5d59eb47e9a1f7db8f2b54`

---

Class Window | Dalamud 

Skip to main content 

On this page 
Class Window

Assembly : Dalamud.dll ​ 
Declaration
public abstract class Window : IWindow 

Implements: 

Dalamud.Interface.Windowing.IWindow 

Properties ​ 

Namespace ​ 

Gets or sets the namespace of the window.

Declaration
public string ? Namespace { get ; set ; } 

WindowName ​ 

Gets or sets the name of the window.
If you have multiple windows with the same name, you will need to
append an unique ID to it by specifying it after "###" behind the window title.

Declaration
public string WindowName { get ; set ; } 

IsFocused ​ 

Gets or sets a value indicating whether the window is focused.

Declaration
public bool IsFocused { get ; set ; } 

RespectCloseHotkey ​ 

Gets or sets a value indicating whether this window is to be closed with a hotkey, like Escape, and keep game addons open in turn if it is closed.

Declaration
public bool RespectCloseHotkey { get ; set ; } 

DisableWindowSounds ​ 

Gets or sets a value indicating whether this window should not generate sound effects when opening and closing.

Declaration
public bool DisableWindowSounds { get ; set ; } 

OnOpenSfxId ​ 

Gets or sets a value representing the sound effect id to be played when the window is opened.

Declaration
public uint OnOpenSfxId { get ; set ; } 

OnCloseSfxId ​ 

Gets or sets a value representing the sound effect id to be played when the window is closed.

Declaration
public uint OnCloseSfxId { get ; set ; } 

DisableFadeInFadeOut ​ 

Gets or sets a value indicating whether this window should not fade in and out, regardless of the users'
preference.

Declaration
public bool DisableFadeInFadeOut { get ; set ; } 

Position ​ 

Gets or sets the position of this window.

Declaration
public Vector2 ? Position { get ; set ; } 

PositionCondition ​ 

Gets or sets the condition that defines when the position of this window is set.

Declaration
public ImGuiCond PositionCondition { get ; set ; } 

Size ​ 

Gets or sets the size of the window. The size provided will be scaled by the global scale.

Declaration
public Vector2 ? Size { get ; set ; } 

SizeCondition ​ 

Gets or sets the condition that defines when the size of this window is set.

Declaration
public ImGuiCond SizeCondition { get ; set ; } 

SizeConstraints ​ 

Gets or sets the size constraints of the window. The size constraints provided will be scaled by the global scale.

Declaration
public WindowSizeConstraints ? SizeConstraints { get ; set ; } 

Collapsed ​ 

Gets or sets a value indicating whether this window is collapsed.

Declaration
public bool ? Collapsed { get ; set ; } 

CollapsedCondition ​ 

Gets or sets the condition that defines when the collapsed state of this window is set.

Declaration
public ImGuiCond CollapsedCondition { get ; set ; } 

Flags ​ 

Gets or sets the window flags.

Declaration
public ImGuiWindowFlags Flags { get ; set ; } 

ForceMainWindow ​ 

Gets or sets a value indicating whether this ImGui window will be forced to stay inside the main game window.

Declaration
public bool ForceMainWindow { get ; set ; } 

BgAlpha ​ 

Gets or sets this window's background alpha value.

Declaration
public float ? BgAlpha { get ; set ; } 

ShowCloseButton ​ 

Gets or sets a value indicating whether this ImGui window should display a close button in the title bar.

Declaration
public bool ShowCloseButton { get ; set ; } 

AllowPinning ​ 

Gets or sets a value indicating whether this window should offer to be pinned via the window's titlebar context menu.

Declaration
public bool AllowPinning { get ; set ; } 

AllowClickthrough ​ 

Gets or sets a value indicating whether this window should offer to be made click-through via the window's titlebar context menu.

Declaration
public bool AllowClickthrough { get ; set ; } 

AllowBackgroundBlur ​ 

Gets or sets a value indicating whether this window should apply a blur effect to the background behind it when drawn, if the user has enabled this feature globally.

Declaration
public bool AllowBackgroundBlur { get ; set ; } 

IsPinned ​ 

Gets or sets a value indicating whether this window is pinned.

Declaration
public bool IsPinned { get ; set ; } 

IsClickthrough ​ 

Gets or sets a value indicating whether this window is click-through.

Declaration
public bool IsClickthrough { get ; set ; } 

TitleBarButtons ​ 

Gets or sets a list of available title bar buttons.

If Dalamud.Interface.Windowing.IWindow.AllowPinning or Dalamud.Interface.Windowing.IWindow.AllowClickthrough are set to true, and this features is not
disabled globally by the user, an internal title bar button to manage these is added when drawing, but it will
not appear in this collection. If you wish to remove this button, set both of these values to false.

Declaration
public List < TitleBarButton > TitleBarButtons { get ; set ; } 

IsOpen ​ 

Gets or sets a value indicating whether this window will stay open.

Declaration
public bool IsOpen { get ; set ; } 

RequestFocus ​ 

Gets or sets a value indicating whether this window will request focus from the window system next frame.

Declaration
public bool RequestFocus { get ; set ; } 

IsTopMost ​ 

Gets or sets a value indicating whether this window is top-most.
Only applies to windows that are not on the main viewport.

Declaration
public bool IsTopMost { get ; set ; } 

Methods ​ 

Toggle() ​ 

Toggle window is open state.

Declaration
public void Toggle ( ) 

BringToFront() ​ 

Bring this window to the front.

Declaration
public void BringToFront ( ) 

PreOpenCheck() ​ 

Code to always be executed before the open-state of the window is checked.

Declaration
public virtual void PreOpenCheck ( ) 

DrawConditions() ​ 

Additional conditions for the window to be drawn, regardless of its open-state.

Declaration
public virtual bool DrawConditions ( ) 

Returns ​ 
System.Boolean : True if the window should be drawn, false otherwise.

Remarks ​ 
Not being drawn due to failing this condition will not change focus or trigger OnClose.
This is checked before PreDraw, but after Update.

PreDraw() ​ 

Code to be executed before conditionals are applied and the window is drawn.

Declaration
public virtual void PreDraw ( ) 

PostDraw() ​ 

Code to be executed after the window is drawn.

Declaration
public virtual void PostDraw ( ) 

Draw() ​ 

Code to be executed every time the window renders.

Declaration
public abstract void Draw ( ) 

Remarks ​ 
In this method, implement your drawing code.
You do NOT need to ImGui.Begin your window.

OnOpen() ​ 

Code to be executed when the window is opened.

Declaration
public virtual void OnOpen ( ) 

OnClose() ​ 

Code to be executed when the window is closed.

Declaration
public virtual void OnClose ( ) 

OnSafeToRemove() ​ 

Code to be executed when the window is safe to be disposed or removed from the window system.
Doing so in Dalamud.Interface.Windowing.IWindow.OnClose() may result in animations not playing correctly.

Declaration
public virtual void OnSafeToRemove ( ) 

Update() ​ 

Code to be executed every frame, even when the window is collapsed.

Declaration
public virtual void Update ( ) 

Implements ​ 

Dalamud.Interface.Windowing.IWindow 

Properties Namespace 
WindowName 
IsFocused 
RespectCloseHotkey 
DisableWindowSounds 
OnOpenSfxId 
OnCloseSfxId 
DisableFadeInFadeOut 
Position 
PositionCondition 
Size 
SizeCondition 
SizeConstraints 
Collapsed 
CollapsedCondition 
Flags 
ForceMainWindow 
BgAlpha 
ShowCloseButton 
AllowPinning 
AllowClickthrough 
AllowBackgroundBlur 
IsPinned 
IsClickthrough 
TitleBarButtons 
IsOpen 
RequestFocus 
IsTopMost 

Methods Toggle() 
BringToFront() 
PreOpenCheck() 
DrawConditions() 
PreDraw() 
PostDraw() 
Draw() 
OnOpen() 
OnClose() 
OnSafeToRemove() 
Update() 

Implements
