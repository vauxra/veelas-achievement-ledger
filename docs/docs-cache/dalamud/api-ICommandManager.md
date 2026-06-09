# api-ICommandManager

Source: https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/ICommandManager

Fetched: 2026-06-09T02:42:42.570435+00:00

SHA256: `d7a2c8fb246c9f8616184d8635b7cc62f62761744dc9eb31423a93cd626e7638`

---

Interface ICommandManager | Dalamud 

Skip to main content 

On this page 
Interface ICommandManager

This class manages registered in-game slash commands.

Assembly : Dalamud.dll ​ 
Declaration
public interface ICommandManager : IDalamudService 

Properties ​ 

Commands ​ 

Gets a read-only list of all registered commands.

Declaration
ReadOnlyDictionary < string , IReadOnlyCommandInfo > Commands { get ; } 

Methods ​ 

ProcessCommand(string) ​ 

Process a command in full.

Declaration
bool ProcessCommand ( string content ) 

Returns ​ 
System.Boolean : True if the command was found and dispatched.

Parameters ​ 

Type Name Description 
System.String content The full command string. 

DispatchCommand(string, string, IReadOnlyCommandInfo) ​ 

Dispatch the handling of a command.

Declaration
void DispatchCommand ( string command , string argument , IReadOnlyCommandInfo info ) 

Parameters ​ 

Type Name Description 
System.String command The command to dispatch. 
System.String argument The provided arguments. 
Dalamud.Game.Command.IReadOnlyCommandInfo info A Dalamud.Game.Command.CommandInfo object describing this command. 

AddHandler(string, CommandInfo) ​ 

Add a command handler, which you can use to add your own custom commands to the in-game chat.

Declaration
bool AddHandler ( string command , CommandInfo info ) 

Returns ​ 
System.Boolean : If adding was successful.

Parameters ​ 

Type Name Description 
System.String command The command to register. 
Dalamud.Game.Command.CommandInfo info A Dalamud.Game.Command.CommandInfo object describing the command. 

RemoveHandler(string) ​ 

Remove a command from the command handlers.

Declaration
bool RemoveHandler ( string command ) 

Returns ​ 
System.Boolean : If the removal was successful.

Parameters ​ 

Type Name Description 
System.String command The command to remove. 

Properties Commands 

Methods ProcessCommand(string) 
DispatchCommand(string, string, IReadOnlyCommandInfo) 
AddHandler(string, CommandInfo) 
RemoveHandler(string)
