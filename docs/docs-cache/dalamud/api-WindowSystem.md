# api-WindowSystem

Source: https://dalamud.dev/api/Dalamud.Interface.Windowing/Classes/WindowSystem

Fetched: 2026-06-09T02:42:42.570435+00:00

SHA256: `f97fb8d4af485132f3c14f128c56e4d5f9813c303545e72f5c3de2e8800d53d7`

---

Class WindowSystem | Dalamud 

Skip to main content 

On this page 
Class WindowSystem

Assembly : Dalamud.dll ​ 
Declaration
public class WindowSystem : IWindowSystem 

Implements: 

Dalamud.Interface.Windowing.IWindowSystem 

Properties ​ 

HasAnyWindowSystemFocus ​ 

Gets a value indicating whether any Dalamud.Interface.Windowing.WindowSystem contains any Dalamud.Interface.Windowing.IWindow 
that has focus and is not marked to be excluded from consideration.

Declaration
public static bool HasAnyWindowSystemFocus { get ; } 

FocusedWindowSystemNamespace ​ 

Gets the name of the currently focused window system that is redirecting normal escape functionality.

Declaration
public static string FocusedWindowSystemNamespace { get ; } 

TimeSinceLastAnyFocus ​ 

Gets the timespan since the last time any window was focused.

Declaration
public static TimeSpan TimeSinceLastAnyFocus { get ; } 

Windows ​ 

Gets a read-only list of all Dalamud.Interface.Windowing.IWindow s in this Dalamud.Interface.Windowing.WindowSystem .

Declaration
public IReadOnlyList < IWindow > Windows { get ; } 

HasAnyFocus ​ 

Gets a value indicating whether any window in this Dalamud.Interface.Windowing.WindowSystem has focus and is
not marked to be excluded from consideration.

Declaration
public bool HasAnyFocus { get ; } 

Namespace ​ 

Gets or sets the name/ID-space of this Dalamud.Interface.Windowing.WindowSystem .

Declaration
public string ? Namespace { get ; set ; } 

Methods ​ 

AddWindow(IWindow) ​ 

Add a window to this Dalamud.Interface.Windowing.WindowSystem .
The window system doesn't own your window, it just renders it
You need to store a reference to it to use it later.

Declaration
public void AddWindow ( IWindow window ) 

Parameters ​ 

Type Name Description 
Dalamud.Interface.Windowing.IWindow window The window to add. 

RemoveWindow(IWindow) ​ 

Remove a window from this Dalamud.Interface.Windowing.WindowSystem .
Will not dispose your window, if it is disposable.

Declaration
public void RemoveWindow ( IWindow window ) 

Parameters ​ 

Type Name Description 
Dalamud.Interface.Windowing.IWindow window The window to remove. 

RemoveAllWindows() ​ 

Remove all windows from this Dalamud.Interface.Windowing.WindowSystem .
Will not dispose your windows, if they are disposable.

Declaration
public void RemoveAllWindows ( ) 

Draw() ​ 

Draw all registered windows using ImGui.

Declaration
public void Draw ( ) 

Implements ​ 

Dalamud.Interface.Windowing.IWindowSystem 

Properties HasAnyWindowSystemFocus 
FocusedWindowSystemNamespace 
TimeSinceLastAnyFocus 
Windows 
HasAnyFocus 
Namespace 

Methods AddWindow(IWindow) 
RemoveWindow(IWindow) 
RemoveAllWindows() 
Draw() 

Implements
