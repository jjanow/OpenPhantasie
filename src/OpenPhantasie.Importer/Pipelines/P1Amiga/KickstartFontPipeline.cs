namespace OpenPhantasie.Importer.Pipelines.P1Amiga;

/// <summary>
/// The Amiga screen font, topaz 8, from the player's Kickstart 1.3 (34.5) ROM (profile <c>kickstart-1.3</c>): I's
/// <c>graphics/fonts/amiga-topaz-8.json</c> and its preview PNG, and III's copy. Its own pipeline, so the font is there
/// whichever Amiga game is imported with the ROM, and re-importing a game without the ROM doesn't remove it.
/// </summary>
public sealed class KickstartFontPipeline : IImportPipeline
{
    public string ProfileId => "kickstart-1.3";
    public int RecipeVersion => 1;

    public void Run(ImportContext context)
    {
        if (!P1AmigaPipeline.WriteTopaz(context, context.Files))
            throw new InvalidDataException("the ROM holds no topaz 8 font");
    }
}
