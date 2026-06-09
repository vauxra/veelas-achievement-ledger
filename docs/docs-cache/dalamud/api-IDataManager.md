# api-IDataManager

Source: https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IDataManager

Fetched: 2026-06-09T02:42:42.570435+00:00

SHA256: `4e6e3b06efc564e8c026f50d970fd1cf9e232bcd2785c77e8a7103d9e2e9aad3`

---

Interface IDataManager | Dalamud 

Skip to main content 

On this page 
Interface IDataManager

This class provides data for Dalamud-internal features, but can also be used by plugins if needed.

Assembly : Dalamud.dll ​ 
Declaration
public interface IDataManager : IDalamudService 

Properties ​ 

Language ​ 

Gets the current game client language.

Declaration
ClientLanguage Language { get ; } 

GameData ​ 

Gets a Lumina object which gives access to any excel/game data.

Declaration
GameData GameData { get ; } 

Excel ​ 

Gets an Lumina.Excel.ExcelModule object which gives access to any of the game's sheet data.

Declaration
ExcelModule Excel { get ; } 

HasModifiedGameDataFiles ​ 

Gets a value indicating whether the game data files have been modified by another third-party tool.

Declaration
bool HasModifiedGameDataFiles { get ; } 

Methods ​ 

GetExcelSheet<T>(ClientLanguage?, string?) ​ 

Get an Lumina.Excel.ExcelSheet 1` with the given Excel sheet row type.

Declaration
ExcelSheet < T > GetExcelSheet < T > ( ClientLanguage ? language = null , string ? name = null ) where T : struct , IExcelRow < T > 

Returns ​ 
Lumina.Excel.ExcelSheet<<T>> : The Lumina.Excel.ExcelSheet 1`, giving access to game rows.

Parameters ​ 

Type Name Description 
System.Nullable<Dalamud.Game.ClientLanguage> language Language of the sheet to get. Leave null or empty to use the default language. 
System.String name Explicitly provide the name of the sheet to get. Leave null to use <code class="typeparamref">T</code>'s sheet name. Explicit names are necessary for quest/dungeon/cutscene sheets. 

Type Parameters ​ 

Name Description 
T The excel sheet type to get. 

Exceptions ​ 
Lumina.Excel.Exceptions.SheetNameEmptyException 

Sheet name was not specified neither via <code class="typeparamref">T</code>'s Lumina.Excel.SheetAttribute.Name nor <code class="paramref">name</code>.
Lumina.Excel.Exceptions.SheetAttributeMissingException 

<code class="typeparamref">T</code> does not have a valid Lumina.Excel.SheetAttribute .
Lumina.Excel.Exceptions.SheetNotFoundException 

Sheet does not exist.
Lumina.Excel.Exceptions.MismatchedColumnHashException 

Sheet had a mismatched column hash.
Lumina.Excel.Exceptions.UnsupportedLanguageException 

Sheet does not support <code class="paramref">language</code> nor Lumina.Data.Language.None .
System.NotSupportedException 

Sheet was not a Lumina.Data.Structs.Excel.ExcelVariant.Default .

Remarks ​ 
If the sheet type you want has subrows, use Dalamud.Plugin.Services.IDataManager.GetSubrowExcelSheet``1(System.Nullable&lt;Dalamud.Game.ClientLanguage&gt;,System.String) instead.

GetSubrowExcelSheet<T>(ClientLanguage?, string?) ​ 

Get a Lumina.Excel.SubrowExcelSheet 1` with the given Excel sheet row type.

Declaration
SubrowExcelSheet < T > GetSubrowExcelSheet < T > ( ClientLanguage ? language = null , string ? name = null ) where T : struct , IExcelSubrow < T > 

Returns ​ 
Lumina.Excel.SubrowExcelSheet<<T>> : The Lumina.Excel.SubrowExcelSheet 1`, giving access to game rows.

Parameters ​ 

Type Name Description 
System.Nullable<Dalamud.Game.ClientLanguage> language Language of the sheet to get. Leave null or empty to use the default language. 
System.String name Explicitly provide the name of the sheet to get. Leave null to use <code class="typeparamref">T</code>'s sheet name. Explicit names are necessary for quest/dungeon/cutscene sheets. 

Type Parameters ​ 

Name Description 
T The excel sheet type to get. 

Exceptions ​ 
Lumina.Excel.Exceptions.SheetNameEmptyException 

Sheet name was not specified neither via <code class="typeparamref">T</code>'s Lumina.Excel.SheetAttribute.Name nor <code class="paramref">name</code>.
Lumina.Excel.Exceptions.SheetAttributeMissingException 

<code class="typeparamref">T</code> does not have a valid Lumina.Excel.SheetAttribute .
Lumina.Excel.Exceptions.SheetNotFoundException 

Sheet does not exist.
Lumina.Excel.Exceptions.MismatchedColumnHashException 

Sheet had a mismatched column hash.
Lumina.Excel.Exceptions.UnsupportedLanguageException 

Sheet does not support <code class="paramref">language</code> nor Lumina.Data.Language.None .
System.NotSupportedException 

Sheet was not a Lumina.Data.Structs.Excel.ExcelVariant.Subrows .

Remarks ​ 
If the sheet type you want has only rows, use Dalamud.Plugin.Services.IDataManager.GetExcelSheet``1(System.Nullable&lt;Dalamud.Game.ClientLanguage&gt;,System.String) instead.

GetFile(string) ​ 

Get a Lumina.Data.FileResource with the given path.

Declaration
FileResource ? GetFile ( string path ) 

Returns ​ 
Lumina.Data.FileResource : The Lumina.Data.FileResource of the file.

Parameters ​ 

Type Name Description 
System.String path The path inside of the game files. 

GetFile<T>(string) ​ 

Get a Lumina.Data.FileResource with the given path, of the given type.

Declaration
T ? GetFile < T > ( string path ) where T : FileResource 

Returns ​ 
<T> : The Lumina.Data.FileResource of the file.

Parameters ​ 

Type Name Description 
System.String path The path inside of the game files. 

Type Parameters ​ 

Name Description 
T The type of resource. 

GetFileAsync<T>(string, CancellationToken) ​ 

Get a Lumina.Data.FileResource with the given path, of the given type.

Declaration
Task < T > GetFileAsync < T > ( string path , CancellationToken cancellationToken ) where T : FileResource 

Returns ​ 
System.Threading.Tasks.Task<<T>> : A System.Threading.Tasks.Task 1 containing the Lumina.Data.FileResource` of the file on success.

Parameters ​ 

Type Name Description 
System.String path The path inside of the game files. 
System.Threading.CancellationToken cancellationToken Cancellation token. 

Type Parameters ​ 

Name Description 
T The type of resource. 

FileExists(string) ​ 

Check if the file with the given path exists within the game's index files.

Declaration
bool FileExists ( string path ) 

Returns ​ 
System.Boolean : True if the file exists.

Parameters ​ 

Type Name Description 
System.String path The path inside of the game files. 

Properties Language 
GameData 
Excel 
HasModifiedGameDataFiles 

Methods GetExcelSheet<T>(ClientLanguage?, string?) 
GetSubrowExcelSheet<T>(ClientLanguage?, string?) 
GetFile(string) 
GetFile<T>(string) 
GetFileAsync<T>(string, CancellationToken) 
FileExists(string)
