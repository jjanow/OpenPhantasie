using OpenPhantasie.Client;

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
_ = UiText.Table; // T1.5: the default game's text for the Client and Core until a game is started
using var game = new Game1(testLevel, testGame, p4Map, p4Town, titlePreview, p4Ending);
game.Run();
return 0;
