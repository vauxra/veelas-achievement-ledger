# api-IUnlockState

Source: https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IUnlockState

Fetched: 2026-06-09T02:42:42.570435+00:00

SHA256: `fa5115e5f5984e5e225af7270089af24ae7e13c079f1a672af9acad382d3efeb`

---

Interface IUnlockState | Dalamud 

Skip to main content 

On this page 
Interface IUnlockState

Interface for determining unlock state of various content in the game.

Assembly : Dalamud.dll ​ 
Declaration
public interface IUnlockState : IDalamudService 

Properties ​ 

IsAchievementListLoaded ​ 

Gets a value indicating whether the full Achievements list was received.

Declaration
bool IsAchievementListLoaded { get ; } 

IsTitleListLoaded ​ 

Gets a value indicating whether the full Titles list was received.

Declaration
bool IsTitleListLoaded { get ; } 

Methods ​ 

IsAchievementComplete(Achievement) ​ 

Determines whether the specified Achievement is completed.

Requires that the player requested the Achievements list (can be chcked with Dalamud.Plugin.Services.IUnlockState.IsAchievementListLoaded ).

Declaration
bool IsAchievementComplete ( Achievement row ) 

Returns ​ 
System.Boolean : true if completed; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Achievement row The Achievement row to check. 

IsActionUnlocked(Action) ​ 

Determines whether the specified Action is unlocked.

Declaration
bool IsActionUnlocked ( Action row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Action row The Action row to check. 

IsAdventureComplete(Adventure) ​ 

Determines whether the specified Adventure is completed.

Declaration
bool IsAdventureComplete ( Adventure row ) 

Returns ​ 
System.Boolean : true if completed; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Adventure row The Adventure row to check. 

IsAetherCurrentCompFlgSetUnlocked(AetherCurrentCompFlgSet) ​ 

Determines whether the specified AetherCurrentCompFlgSet is unlocked.

Declaration
bool IsAetherCurrentCompFlgSetUnlocked ( AetherCurrentCompFlgSet row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.AetherCurrentCompFlgSet row The AetherCurrentCompFlgSet row to check. 

IsAetherCurrentUnlocked(AetherCurrent) ​ 

Determines whether the specified AetherCurrent is unlocked.

Declaration
bool IsAetherCurrentUnlocked ( AetherCurrent row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.AetherCurrent row The AetherCurrent row to check. 

IsAozActionUnlocked(AozAction) ​ 

Determines whether the specified AozAction (Blue Mage Action) is unlocked.

Declaration
bool IsAozActionUnlocked ( AozAction row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.AozAction row The AozAction row to check. 

IsBannerBgUnlocked(BannerBg) ​ 

Determines whether the specified BannerBg (Portrait Backgrounds) is unlocked.

Declaration
bool IsBannerBgUnlocked ( BannerBg row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.BannerBg row The BannerBg row to check. 

IsBannerConditionUnlocked(BannerCondition) ​ 

Determines whether the specified BannerCondition is unlocked.

Declaration
bool IsBannerConditionUnlocked ( BannerCondition row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.BannerCondition row The BannerCondition row to check. 

IsBannerDecorationUnlocked(BannerDecoration) ​ 

Determines whether the specified BannerDecoration (Portrait Accents) is unlocked.

Declaration
bool IsBannerDecorationUnlocked ( BannerDecoration row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.BannerDecoration row The BannerDecoration row to check. 

IsBannerFacialUnlocked(BannerFacial) ​ 

Determines whether the specified BannerFacial (Portrait Expressions) is unlocked.

Declaration
bool IsBannerFacialUnlocked ( BannerFacial row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.BannerFacial row The BannerFacial row to check. 

IsBannerFrameUnlocked(BannerFrame) ​ 

Determines whether the specified BannerFrame (Portrait Frames) is unlocked.

Declaration
bool IsBannerFrameUnlocked ( BannerFrame row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.BannerFrame row The BannerFrame row to check. 

IsBannerTimelineUnlocked(BannerTimeline) ​ 

Determines whether the specified BannerTimeline (Portrait Poses) is unlocked.

Declaration
bool IsBannerTimelineUnlocked ( BannerTimeline row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.BannerTimeline row The BannerTimeline row to check. 

IsBuddyActionUnlocked(BuddyAction) ​ 

Determines whether the specified BuddyAction (Action of the players Chocobo Companion) is unlocked.

Declaration
bool IsBuddyActionUnlocked ( BuddyAction row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.BuddyAction row The BuddyAction row to check. 

IsBuddyEquipUnlocked(BuddyEquip) ​ 

Determines whether the specified BuddyEquip (Equipment of the players Chocobo Companion) is unlocked.

Declaration
bool IsBuddyEquipUnlocked ( BuddyEquip row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.BuddyEquip row The BuddyEquip row to check. 

IsCharaMakeCustomizeUnlocked(CharaMakeCustomize) ​ 

Determines whether the specified CharaMakeCustomize (Hairstyles and Face Paint patterns) is unlocked.

Declaration
bool IsCharaMakeCustomizeUnlocked ( CharaMakeCustomize row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.CharaMakeCustomize row The CharaMakeCustomize row to check. 

IsChocoboTaxiStandUnlocked(ChocoboTaxiStand) ​ 

Determines whether the specified ChocoboTaxiStand (Chocobokeeps of the Chocobo Porter service) is unlocked.

Declaration
bool IsChocoboTaxiStandUnlocked ( ChocoboTaxiStand row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.ChocoboTaxiStand row The ChocoboTaxiStand row to check. 

IsClassJobUnlocked(ClassJob) ​ 

Determines whether the specified ClassJob is unlocked.

Declaration
bool IsClassJobUnlocked ( ClassJob row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.ClassJob row The ClassJob row to check. 

IsCompanionUnlocked(Companion) ​ 

Determines whether the specified Companion (Minions) is unlocked.

Declaration
bool IsCompanionUnlocked ( Companion row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Companion row The Companion row to check. 

IsCraftActionUnlocked(CraftAction) ​ 

Determines whether the specified CraftAction is unlocked.

Declaration
bool IsCraftActionUnlocked ( CraftAction row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.CraftAction row The CraftAction row to check. 

IsCSBonusContentTypeUnlocked(CSBonusContentType) ​ 

Determines whether the specified CSBonusContentType is unlocked.

Declaration
bool IsCSBonusContentTypeUnlocked ( CSBonusContentType row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.CSBonusContentType row The CSBonusContentType row to check. 

IsEmoteUnlocked(Emote) ​ 

Determines whether the specified Emote is unlocked.

Declaration
bool IsEmoteUnlocked ( Emote row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Emote row The Emote row to check. 

IsEmjVoiceNpcUnlocked(EmjVoiceNpc) ​ 

Determines whether the specified EmjVoiceNpc (Doman Mahjong Characters) is unlocked.

Declaration
bool IsEmjVoiceNpcUnlocked ( EmjVoiceNpc row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.EmjVoiceNpc row The EmjVoiceNpc row to check. 

IsEmjCostumeUnlocked(EmjCostume) ​ 

Determines whether the specified EmjCostume (Doman Mahjong Character Costume) is unlocked.

Declaration
bool IsEmjCostumeUnlocked ( EmjCostume row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.EmjCostume row The EmjCostume row to check. 

IsGeneralActionUnlocked(GeneralAction) ​ 

Determines whether the specified GeneralAction is unlocked.

Declaration
bool IsGeneralActionUnlocked ( GeneralAction row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.GeneralAction row The GeneralAction row to check. 

IsGlassesUnlocked(Glasses) ​ 

Determines whether the specified Glasses is unlocked.

Declaration
bool IsGlassesUnlocked ( Glasses row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Glasses row The Glasses row to check. 

IsGlassesStyleUnlocked(GlassesStyle) ​ 

Determines whether the specified GlassesStyle is unlocked.

Declaration
bool IsGlassesStyleUnlocked ( GlassesStyle row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.GlassesStyle row The GlassesStyle row to check. 

IsHowToUnlocked(HowTo) ​ 

Determines whether the specified HowTo is unlocked.

Declaration
bool IsHowToUnlocked ( HowTo row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.HowTo row The HowTo row to check. 

IsInstanceContentUnlocked(InstanceContent) ​ 

Determines whether the specified InstanceContent is unlocked.

Declaration
bool IsInstanceContentUnlocked ( InstanceContent row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.InstanceContent row The InstanceContent row to check. 

IsItemUnlockable(Item) ​ 

Determines whether the specified Item is considered unlockable.

Declaration
bool IsItemUnlockable ( Item row ) 

Returns ​ 
System.Boolean : true if unlockable; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Item row The Item row to check. 

IsItemUnlocked(Item) ​ 

Determines whether the specified Item is unlocked.

Declaration
bool IsItemUnlocked ( Item row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Item row The Item row to check. 

IsLeveCompleted(Leve) ​ 

Determines whether the specified Leve is completed.

Declaration
bool IsLeveCompleted ( Leve row ) 

Returns ​ 
System.Boolean : true if completed; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Leve row The Leve row to check. 

IsMcGuffinUnlocked(McGuffin) ​ 

Determines whether the specified McGuffin is unlocked.

Declaration
bool IsMcGuffinUnlocked ( McGuffin row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.McGuffin row The McGuffin row to check. 

IsMJILandmarkUnlocked(MJILandmark) ​ 

Determines whether the specified MJILandmark (Island Sanctuary landmark) is unlocked.

Declaration
bool IsMJILandmarkUnlocked ( MJILandmark row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.MJILandmark row The MJILandmark row to check. 

IsMKDLoreUnlocked(MKDLore) ​ 

Determines whether the specified MKDLore (Occult Record) is unlocked.

Declaration
bool IsMKDLoreUnlocked ( MKDLore row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.MKDLore row The MKDLore row to check. 

IsMountUnlocked(Mount) ​ 

Determines whether the specified Mount is unlocked.

Declaration
bool IsMountUnlocked ( Mount row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Mount row The Mount row to check. 

IsNotebookDivisionUnlocked(NotebookDivision) ​ 

Determines whether the specified NotebookDivision (Categories in Crafting/Gathering Log) is unlocked.

Declaration
bool IsNotebookDivisionUnlocked ( NotebookDivision row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.NotebookDivision row The NotebookDivision row to check. 

IsOrchestrionUnlocked(Orchestrion) ​ 

Determines whether the specified Orchestrion roll is unlocked.

Declaration
bool IsOrchestrionUnlocked ( Orchestrion row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Orchestrion row The Orchestrion row to check. 

IsOrnamentUnlocked(Ornament) ​ 

Determines whether the specified Ornament (Fashion Accessories) is unlocked.

Declaration
bool IsOrnamentUnlocked ( Ornament row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Ornament row The Ornament row to check. 

IsPerformUnlocked(Perform) ​ 

Determines whether the specified Perform (Performance Instruments) is unlocked.

Declaration
bool IsPerformUnlocked ( Perform row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Perform row The Perform row to check. 

IsPublicContentUnlocked(PublicContent) ​ 

Determines whether the specified PublicContent is unlocked.

Declaration
bool IsPublicContentUnlocked ( PublicContent row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.PublicContent row The PublicContent row to check. 

IsQuestCompleted(Quest) ​ 

Determines whether the specified Quest is completed.

Declaration
bool IsQuestCompleted ( Quest row ) 

Returns  ​ 
System.Boolean : true if completed; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Quest row The Quest row to check. 

IsRecipeUnlocked(Recipe) ​ 

Determines whether the specified Recipe is unlocked.

Declaration
bool IsRecipeUnlocked ( Recipe row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Recipe row The Recipe row to check. 

IsRowRefUnlocked(RowRef) ​ 

Determines whether the underlying RowRef type is unlocked.

Declaration
bool IsRowRefUnlocked ( RowRef rowRef ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.RowRef rowRef The RowRef to check. 

IsRowRefUnlocked<T>(RowRef<T>) ​ 

Determines whether the underlying RowRef type is unlocked.

Declaration
bool IsRowRefUnlocked < T > ( RowRef < T > rowRef ) where T : struct , IExcelRow < T > 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.RowRef<<T>> rowRef The RowRef to check. 

Type Parameters ​ 

Name Description 
T The type of the Excel row. 

IsSecretRecipeBookUnlocked(SecretRecipeBook) ​ 

Determines whether the specified SecretRecipeBook (Master Recipe Books) is unlocked.

Declaration
bool IsSecretRecipeBookUnlocked ( SecretRecipeBook row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.SecretRecipeBook row The SecretRecipeBook row to check. 

IsTitleUnlocked(Title) ​ 

Determines whether the specified Title is unlocked.

Requires that the player requested the Titles list (can be chcked with Dalamud.Plugin.Services.IUnlockState.IsTitleListLoaded ).

Declaration
bool IsTitleUnlocked ( Title row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Title row The Title row to check. 

IsTraitUnlocked(Trait) ​ 

Determines whether the specified Trait is unlocked.

Declaration
bool IsTraitUnlocked ( Trait row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.Trait row The Trait row to check. 

IsTripleTriadCardUnlocked(TripleTriadCard) ​ 

Determines whether the specified TripleTriadCard is unlocked.

Declaration
bool IsTripleTriadCardUnlocked ( TripleTriadCard row ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
Lumina.Excel.Sheets.TripleTriadCard row The TripleTriadCard row to check. 

IsUnlockLinkUnlocked(uint) ​ 

Determines whether the specified unlock link is unlocked or quest is completed.

Declaration
bool IsUnlockLinkUnlocked ( uint unlockLink ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
System.UInt32 unlockLink The unlock link id or quest id (quest ids in this case are over 65536). 

IsUnlockLinkUnlocked(uint, byte)   ​ 

Determines whether the specified unlock link is unlocked or quest is completed.

Declaration
bool IsUnlockLinkUnlocked ( uint unlockLink , byte minimumQuestSequence ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
System.UInt32 unlockLink The unlock link id or quest id (quest ids in this case are over 65536). 
System.Byte minimumQuestSequence The minimum progression (sequence) of a quest. 

IsUnlockLinkUnlocked(ushort) ​ 

Determines whether the specified unlock link is unlocked.

Declaration
bool IsUnlockLinkUnlocked ( ushort unlockLink ) 

Returns ​ 
System.Boolean : true if unlocked; otherwise, false .

Parameters ​ 

Type Name Description 
System.UInt16 unlockLink The unlock link id. 

Events ​ 

Unlock ​ 

Event triggered when something was unlocked.

Declaration
event IUnlockState . UnlockDelegate ? Unlock 

Event Type ​ 
Dalamud.Plugin.Services.IUnlockState.UnlockDelegate 

Properties IsAchievementListLoaded 
IsTitleListLoaded 

Methods IsAchievementComplete(Achievement) 
IsActionUnlocked(Action) 
IsAdventureComplete(Adventure) 
IsAetherCurrentCompFlgSetUnlocked(AetherCurrentCompFlgSet) 
IsAetherCurrentUnlocked(AetherCurrent) 
IsAozActionUnlocked(AozAction) 
IsBannerBgUnlocked(BannerBg) 
IsBannerConditionUnlocked(BannerCondition) 
IsBannerDecorationUnlocked(BannerDecoration) 
IsBannerFacialUnlocked(BannerFacial) 
IsBannerFrameUnlocked(BannerFrame) 
IsBannerTimelineUnlocked(BannerTimeline) 
IsBuddyActionUnlocked(BuddyAction) 
IsBuddyEquipUnlocked(BuddyEquip) 
IsCharaMakeCustomizeUnlocked(CharaMakeCustomize) 
IsChocoboTaxiStandUnlocked(ChocoboTaxiStand) 
IsClassJobUnlocked(ClassJob) 
IsCompanionUnlocked(Companion) 
IsCraftActionUnlocked(CraftAction) 
IsCSBonusContentTypeUnlocked(CSBonusContentType) 
IsEmoteUnlocked(Emote) 
IsEmjVoiceNpcUnlocked(EmjVoiceNpc) 
IsEmjCostumeUnlocked(EmjCostume) 
IsGeneralActionUnlocked(GeneralAction) 
IsGlassesUnlocked(Glasses) 
IsGlassesStyleUnlocked(GlassesStyle) 
IsHowToUnlocked(HowTo) 
IsInstanceContentUnlocked(InstanceContent) 
IsItemUnlockable(Item) 
IsItemUnlocked(Item) 
IsLeveCompleted(Leve) 
IsMcGuffinUnlocked(McGuffin) 
IsMJILandmarkUnlocked(MJILandmark) 
IsMKDLoreUnlocked(MKDLore) 
IsMountUnlocked(Mount) 
IsNotebookDivisionUnlocked(NotebookDivision) 
IsOrchestrionUnlocked(Orchestrion) 
IsOrnamentUnlocked(Ornament) 
IsPerformUnlocked(Perform) 
IsPublicContentUnlocked(PublicContent) 
IsQuestCompleted(Quest) 
IsRecipeUnlocked(Recipe) 
IsRowRefUnlocked(RowRef) 
IsRowRefUnlocked<T>(RowRef<T>) 
IsSecretRecipeBookUnlocked(SecretRecipeBook) 
IsTitleUnlocked(Title) 
IsTraitUnlocked(Trait) 
IsTripleTriadCardUnlocked(TripleTriadCard) 
IsUnlockLinkUnlocked(uint) 
IsUnlockLinkUnlocked(uint, byte) 
IsUnlockLinkUnlocked(ushort) 

Events Unlock
