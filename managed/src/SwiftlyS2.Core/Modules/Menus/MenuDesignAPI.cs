using SwiftlyS2.Shared.Menus;

namespace SwiftlyS2.Core.Menus;

internal sealed class MenuDesignAPI : IMenuDesignAPI
{
    private readonly MenuConfiguration configuration;
    private readonly IMenuBuilderAPI builder;

    public MenuDesignAPI( MenuConfiguration configuration, IMenuBuilderAPI builder )
    {
        this.configuration = configuration;
        this.builder = builder;
    }

    public IMenuBuilderAPI SetMenuTitle( string? title = null )
    {
        configuration.Title = title ?? "Menu";
        return builder;
    }

    public IMenuBuilderAPI HideMenuTitle( bool hide = false )
    {
        configuration.HideTitle = hide;
        return builder;
    }

    public IMenuBuilderAPI MaxVisibleItems( int count = 5 )
    {
        if (count < 1 || count > 5)
        {
            Spectre.Console.AnsiConsole.WriteException(new ArgumentOutOfRangeException(nameof(count), $"MaxVisibleItems: value {count} is out of range [1, 5]."));
        }
        configuration.MaxVisibleItems = Math.Clamp(count, 1, 5);
        return builder;
    }
}