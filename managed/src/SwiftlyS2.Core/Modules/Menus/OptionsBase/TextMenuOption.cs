using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace SwiftlyS2.Core.Menus.OptionsBase;

public class TextMenuOption : MenuOptionBase
{
    public TextMenuOption()
    {
    }

    public TextMenuOption( string text )
    {
        Text = text;
    }

    public TextMenuOption( string text, bool enabled )
    {
        Text = text;
        Enabled = enabled;
    }

    public TextMenuOption( string text, MenuOptionTextSize textSize )
    {
        Text = text;
        TextSize = textSize;
    }

    public TextMenuOption( string text, bool enabled, MenuOptionTextSize textSize )
    {
        Text = text;
        Enabled = enabled;
        TextSize = textSize;
    }

    public override string GetDisplayText( IPlayer player, int displayLine = 0 )
    {
        return base.GetDisplayText(player, displayLine);
    }
}