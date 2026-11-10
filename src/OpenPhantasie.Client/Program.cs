using OpenPhantasie.Client;
using OpenPhantasie.Client.Updates;
using OpenPhantasie.Data;

// s174: a finished self-update left the replaced files beside the new ones (.update-old); they go now.
if (AppVersion.IsRelease)
    UpdateInstaller.CleanUp(UpdateInstaller.InstallRoot(AppContext.BaseDirectory));

// s163: the player's import before the switches are checked (--test-game asks whether the game is playable, which in
// a build without built-in GameData means imported); Game1.Initialize sets the same root again.
GameDataLocator.UseImport(Path.Combine(ClientSettings.DataDirectory, "import"));
var testLevel = TestParty.ParseArgs(args, out var error);
var testGame = TestParty.ParseGame(args, out var gameError);
error ??= gameError;
var p4Map = P4Preview.ParseArgs(args, out var p4Error);
var p4Town = P4Preview.ParseTownArgs(args, out var p4TownError);
error ??= p4Error;
error ??= p4TownError;
var p4Ending = P4Preview.ParseEndingArgs(args, out var p4EndingError);
error ??= p4EndingError;
var titlePreview = TitlePreview.ParseArgs(args, out var titleError);
error ??= titleError;
if (error is not null)
{
    Console.Error.WriteLine(error);
    return 2;
}
// s180: a signal that ends the Client says so on stderr (the exit line, Game1.OnExiting, covers the game's own closes).
// Never a reason not to start: a platform without these signals just goes without the line.
System.Runtime.InteropServices.PosixSignalRegistration? onTerm = null, onHup = null;
try
{
    onTerm = System.Runtime.InteropServices.PosixSignalRegistration.Create(System.Runtime.InteropServices.PosixSignal.SIGTERM,
        _ => Console.Error.WriteLine("OpenPhantasie: SIGTERM"));
    onHup = System.Runtime.InteropServices.PosixSignalRegistration.Create(System.Runtime.InteropServices.PosixSignal.SIGHUP,
        _ => Console.Error.WriteLine("OpenPhantasie: SIGHUP"));
}
catch (Exception e) when (e is PlatformNotSupportedException or System.ComponentModel.Win32Exception or IOException)
{
}
_ = UiText.Table; // T1.5: the default game's text for the Client and Core until a game is started
using var game = new Game1(testLevel, testGame, p4Map, p4Town, titlePreview, p4Ending);
game.Run();
onTerm?.Dispose();
onHup?.Dispose();
return 0;
