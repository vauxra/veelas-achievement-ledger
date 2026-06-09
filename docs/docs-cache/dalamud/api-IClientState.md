# api-IClientState

Source: https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IClientState

Fetched: 2026-06-09T02:42:42.570435+00:00

SHA256: `6b1ea7cc8bee3c6e2f8169e4145f6554de9e4e238d854203d5c5e7adfece320d`

---

Interface IClientState | Dalamud 

Skip to main content 

On this page 
Interface IClientState

This class represents the state of the game client at the time of access.

Assembly : Dalamud.dll ​ 
Declaration
public interface IClientState : IDalamudService 

Properties ​ 

ClientLanguage ​ 

Gets the language of the client.

Declaration
ClientLanguage ClientLanguage { get ; } 

TerritoryType ​ 

Gets the current Territory the player resides in.

Declaration
uint TerritoryType { get ; } 

MapId ​ 

Gets the current Map the player resides in.

Declaration
uint MapId { get ; } 

Instance ​ 

Gets the instance number of the current zone, used when multiple copies of an area are active.

Declaration
uint Instance { get ; } 

IsLoggedIn ​ 

Gets a value indicating whether a character is logged in.

Declaration
bool IsLoggedIn { get ; } 

IsPvP ​ 

Gets a value indicating whether the user is playing PvP.

Declaration
bool IsPvP { get ; } 

IsPvPExcludingDen ​ 

Gets a value indicating whether the user is playing PvP, excluding the Wolves' Den.

Declaration
bool IsPvPExcludingDen { get ; } 

IsGPosing ​ 

Gets a value indicating whether the client is currently in Group Pose (GPose) mode.

Declaration
bool IsGPosing { get ; } 

Methods ​ 

IsClientIdle(out ConditionFlag) ​ 

Opinionated check whether the client is currently "idle". This means a player is not logged in, or is not actively in combat
or doing anything that we may not want to disrupt.

Declaration
bool IsClientIdle ( out ConditionFlag blockingFlag ) 

Returns ​ 
System.Boolean : Returns true if the client is idle, false otherwise.

Parameters ​ 

Type Name Description 
Dalamud.Game.ClientState.Conditions.ConditionFlag blockingFlag An outvar containing the first observed condition blocking the "idle" state. 0 if idle. 

IsClientIdle() ​ 

Opinionated check whether the client is currently "idle". This means a player is not logged in, or is not actively in combat
or doing anything that we may not want to disrupt.

Declaration
bool IsClientIdle ( ) 

Returns ​ 
System.Boolean : Returns true if the client is idle, false otherwise.

Events ​ 

ZoneInit ​ 

Event that gets fired when the game initializes a zone.

Declaration
event Action < ZoneInitEventArgs > ZoneInit 

Event Type ​ 
System.Action<Dalamud.Game.ClientState.ZoneInitEventArgs> 

TerritoryChanged ​ 

Event that gets fired when the current Territory changes.

Declaration
event Action < uint > TerritoryChanged 

Event Type ​ 
System.Action<System.UInt32> 

MapIdChanged ​ 

Event that gets fired when the current Map changes.

Declaration
event Action < uint > MapIdChanged 

Event Type ​ 
System.Action<System.UInt32> 

InstanceChanged ​ 

Event that gets fired when the current zone Instance changes.

Declaration
event Action < uint > InstanceChanged 

Event Type ​ 
System.Action<System.UInt32> 

ClassJobChanged ​ 

Event that fires when a characters ClassJob changed.

Declaration
event IClientState . ClassJobChangeDelegate ? ClassJobChanged 

Event Type ​ 
Dalamud.Plugin.Services.IClientState.ClassJobChangeDelegate 

LevelChanged ​ 

Event that fires when any character level changes, including levels
for a not-currently-active ClassJob (e.g. PvP matches, DoH/DoL).

Declaration
event IClientState . LevelChangeDelegate ? LevelChanged 

Event Type ​ 
Dalamud.Plugin.Services.IClientState.LevelChangeDelegate 

Login ​ 

Event that fires when a character is logging in, and the local character object is available.

Declaration
event Action Login 

Event Type ​ 
System.Action 

Logout ​ 

Event that fires when a character is logging out.

Declaration
event IClientState . LogoutDelegate Logout 

Event Type ​ 
Dalamud.Plugin.Services.IClientState.LogoutDelegate 

EnterPvP ​ 

Event that fires when a character is entering PvP.

Declaration
event Action EnterPvP 

Event Type ​ 
System.Action 

LeavePvP ​ 

Event that fires when a character is leaving PvP.

Declaration
event Action LeavePvP 

Event Type ​ 
System.Action 

CfPop ​ 

Event that gets fired when a duty is ready.

Declaration
event Action < ContentFinderCondition > CfPop 

Event Type ​ 
System.Action<Lumina.Excel.Sheets.ContentFinderCondition> 

Properties ClientLanguage 
TerritoryType 
MapId 
Instance 
IsLoggedIn 
IsPvP 
IsPvPExcludingDen 
IsGPosing 

Methods IsClientIdle(out ConditionFlag) 
IsClientIdle() 

Events ZoneInit 
TerritoryChanged 
MapIdChanged 
InstanceChanged 
ClassJobChanged 
LevelChanged 
Login 
Logout 
EnterPvP 
LeavePvP 
CfPop
