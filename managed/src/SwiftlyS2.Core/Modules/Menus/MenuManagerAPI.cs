using System.Globalization;
using System.Collections.Concurrent;
using SwiftlyS2.Core.Natives;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Sounds;
using SwiftlyS2.Shared.Players;

namespace SwiftlyS2.Core.Menus;

internal class MenuManagerAPI : IMenuManagerAPI
{
    /// <summary>
    /// Global Configuration settings for all menus.
    /// </summary>
    public MenuManagerConfiguration Configuration { get; init; }

    /// <summary>
    /// Fired when a menu is closed for a player.
    /// </summary>
    public event EventHandler<MenuManagerEventArgs>? MenuClosed;

    /// <summary>
    /// Fired when a menu is opened for a player.
    /// </summary>
    public event EventHandler<MenuManagerEventArgs>? MenuOpened;

    private readonly ISwiftlyCore core;
    private readonly ConcurrentDictionary<IPlayer, IMenuAPI> openMenus = new();
    private readonly SoundEvent useSound = new();
    private readonly SoundEvent exitSound = new();
    private readonly SoundEvent scrollSound = new();

    private static readonly Dictionary<string, KeyKind> StringToKeyKind = new() {
        ["mouse1"] = KeyKind.Mouse1,
        ["mouse2"] = KeyKind.Mouse2,
        ["space"] = KeyKind.Space,
        ["ctrl"] = KeyKind.Ctrl,
        ["w"] = KeyKind.W,
        ["a"] = KeyKind.A,
        ["s"] = KeyKind.S,
        ["d"] = KeyKind.D,
        ["e"] = KeyKind.E,
        ["esc"] = KeyKind.Esc,
        ["r"] = KeyKind.R,
        ["alt"] = KeyKind.Alt,
        ["shift"] = KeyKind.Shift,
        ["weapon1"] = KeyKind.Weapon1,
        ["weapon2"] = KeyKind.Weapon2,
        ["grenade1"] = KeyKind.Grenade1,
        ["grenade2"] = KeyKind.Grenade2,
        ["tab"] = KeyKind.Tab,
        ["f"] = KeyKind.F,
    };

    public MenuManagerAPI( ISwiftlyCore core )
    {
        this.core = core;

        var settings = NativeEngineHelpers.GetMenuSettings().Trim().Split('\x01');
        Configuration = new MenuManagerConfiguration {
            NavigationPrefix = settings[0],
            InputMode = settings[1],
            ButtonsUse = settings[2],
            ButtonsScroll = settings[3],
            ButtonsScrollBack = settings[4],
            ButtonsExit = settings[5],
            SoundUseName = settings[6],
            SoundUseVolume = float.Parse(settings[7], CultureInfo.InvariantCulture),
            SoundScrollName = settings[8],
            SoundScrollVolume = float.Parse(settings[9], CultureInfo.InvariantCulture),
            SoundExitName = settings[10],
            SoundExitVolume = float.Parse(settings[11], CultureInfo.InvariantCulture),
            ItemsPerPage = int.Parse(settings[12]),
        };

        scrollSound.Name = Configuration.SoundScrollName;
        scrollSound.Volume = Configuration.SoundScrollVolume;
        useSound.Name = Configuration.SoundUseName;
        useSound.Volume = Configuration.SoundUseVolume;
        exitSound.Name = Configuration.SoundExitName;
        exitSound.Volume = Configuration.SoundExitVolume;

        openMenus.Clear();

        core.Event.OnClientKeyStateChanged += KeyStateChange;
        core.Event.OnClientDisconnected += OnClientDisconnected;
        core.Event.OnMapUnload += OnMapUnload;
    }

    ~MenuManagerAPI()
    {
        CloseAllMenus();

        core.Event.OnClientKeyStateChanged -= KeyStateChange;
        core.Event.OnClientDisconnected -= OnClientDisconnected;
        core.Event.OnMapUnload -= OnMapUnload;
    }

    private void KeyStateChange( IOnClientKeyStateChangedEvent @event )
    {
        var player = core.PlayerManager.GetPlayer(@event.PlayerId);
        var menu = GetCurrentMenu(player);

        if (menu is null || !player.IsValid || player.IsFakeClient || !@event.Pressed)
        {
            return;
        }

        if (Configuration.InputMode == "button")
        {
            var scrollKey = menu.KeybindOverrides.Move ?? StringToKeyKind.GetValueOrDefault(Configuration.ButtonsScroll);
            var scrollBackKey = menu.KeybindOverrides.MoveBack ?? StringToKeyKind.GetValueOrDefault(Configuration.ButtonsScrollBack);
            var exitKey = menu.KeybindOverrides.Exit ?? StringToKeyKind.GetValueOrDefault(Configuration.ButtonsExit);
            var useKey = menu.KeybindOverrides.Select ?? StringToKeyKind.GetValueOrDefault(Configuration.ButtonsUse);

            new Dictionary<string, KeyKind> { ["Scroll"] = scrollKey, ["ScrollBack"] = scrollBackKey, ["Exit"] = exitKey, ["Use"] = useKey }
                .GroupBy(kvp => kvp.Value)
                .Where(g => g.Count() > 1 && @event.Key.HasFlag(g.Key))
                .ToList()
                .ForEach(group =>
                {
                    Spectre.Console.AnsiConsole.WriteException(
                        new InvalidOperationException($"Duplicate key binding detected in menu '{menu.Title}': Key '{group.Key}' is used by: {string.Join(", ", group.Select(kvp => kvp.Key))}")
                    );
                });

            if (@event.Key == scrollKey)
            {
                // TODO
                // menu.MoveSelection(player, 1);

                if (menu.Configuration.PlaySound)
                {
                    scrollSound.Recipients.AddRecipient(@event.PlayerId);
                    scrollSound.Emit();
                    scrollSound.Recipients.RemoveRecipient(@event.PlayerId);
                }
            }
            else if (@event.Key == scrollBackKey)
            {
                // TODO
                // menu.MoveSelection(player, -1);

                if (menu.Configuration.PlaySound)
                {
                    scrollSound.Recipients.AddRecipient(@event.PlayerId);
                    scrollSound.Emit();
                    scrollSound.Recipients.RemoveRecipient(@event.PlayerId);
                }
            }
            else if (@event.Key == exitKey)
            {
                CloseMenuForPlayer(player, menu);

                if (menu.Configuration.PlaySound)
                {
                    exitSound.Recipients.AddRecipient(@event.PlayerId);
                    exitSound.Emit();
                    exitSound.Recipients.RemoveRecipient(@event.PlayerId);
                }
            }
            else if (@event.Key == useKey)
            {
                // TODO
                // var option = menu.GetCurrentOption(player);
                // if (option is SliderMenuButton || option is ChoiceMenuOption)
                // {
                //     menu.UseSlideOption(player, true);
                // }
                // else
                // {
                //     menu.UseSelection(player);
                // }

                if (menu.Configuration.PlaySound/* && (option?.PlaySound ?? false)*/)
                {
                    useSound.Recipients.AddRecipient(@event.PlayerId);
                    useSound.Emit();
                    useSound.Recipients.RemoveRecipient(@event.PlayerId);
                }
            }
        }
        else if (Configuration.InputMode == "wasd")
        {
            if (@event.Key == KeyKind.W)
            {
                // TODO
                // menu.MoveSelection(player, -1);

                if (menu.Configuration.PlaySound)
                {
                    scrollSound.Recipients.AddRecipient(@event.PlayerId);
                    scrollSound.Emit();
                    scrollSound.Recipients.RemoveRecipient(@event.PlayerId);
                }
            }
            else if (@event.Key == KeyKind.S)
            {
                // TODO
                // menu.MoveSelection(player, 1);

                if (menu.Configuration.PlaySound)
                {
                    scrollSound.Recipients.AddRecipient(@event.PlayerId);
                    scrollSound.Emit();
                    scrollSound.Recipients.RemoveRecipient(@event.PlayerId);
                }
            }
            else if (@event.Key == KeyKind.A)
            {
                CloseMenuForPlayer(player, menu);
                if (menu.Configuration.PlaySound)
                {
                    exitSound.Recipients.AddRecipient(@event.PlayerId);
                    exitSound.Emit();
                    exitSound.Recipients.RemoveRecipient(@event.PlayerId);
                }
            }
            else if (@event.Key == KeyKind.D)
            {
                // TODO
                // var option = menu.GetCurrentOption(player);
                // if (option is SliderMenuButton || option is ChoiceMenuOption)
                // {
                //     menu.UseSlideOption(player, true);
                // }
                // else
                // {
                //     menu.UseSelection(player);
                // }

                if (menu.Configuration.PlaySound/* && (option?.PlaySound ?? false)*/)
                {
                    useSound.Recipients.AddRecipient(@event.PlayerId);
                    useSound.Emit();
                    useSound.Recipients.RemoveRecipient(@event.PlayerId);
                }
            }
        }
    }

    private void OnClientDisconnected( IOnClientDisconnectedEvent @event )
    {
        var player = core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player != null)
        {
            openMenus
                .Where(kvp => kvp.Key == player)
                .ToList()
                .ForEach(kvp => CloseMenuForPlayer(player, kvp.Value));
        }
    }

    private void OnMapUnload( IOnMapUnloadEvent _ )
    {
        CloseAllMenus();
    }

    public IMenuAPI CreateMenu( string? title = null, bool hideTitle = false )
    {
        // TODO
        // return new MenuAPI { Title = title, MenuManager = this, MaxVisibleOptions = Settings.ItemsPerPage, core = core };
        return null!;
    }

    public IMenuAPI? GetCurrentMenu( IPlayer player )
    {
        return openMenus.TryGetValue(player, out var menu) ? menu : null;
    }

    public void OpenMenu( IMenuAPI menu )
    {
        core.PlayerManager
            .GetAllPlayers()
            .ToList()
            .ForEach(player => OpenMenuForPlayer(player, menu));
    }

    public void OpenMenuForPlayer( IPlayer player, IMenuAPI menu )
    {
        if (GetCurrentMenu(player) != null)
        {
            CloseMenuForPlayer(player, GetCurrentMenu(player)!);
        }

        _ = openMenus.AddOrUpdate(player, menu, ( _, _ ) => menu);
        menu.ShowForPlayer(player);
        MenuOpened?.Invoke(this, new MenuManagerEventArgs { Player = player, Menu = menu });
    }

    public void CloseMenu( IMenuAPI menu )
    {
        core.PlayerManager
            .GetAllPlayers()
            .ToList()
            .ForEach(player => CloseMenuForPlayer(player, menu));
    }

    public void CloseMenuForPlayer( IPlayer player, IMenuAPI menu )
    {
        if (openMenus.TryRemove(player, out _))
        {
            menu.CloseForPlayer(player);
            MenuClosed?.Invoke(this, new MenuManagerEventArgs { Player = player, Menu = menu });

            if (menu.Parent != null)
            {
                OpenMenuForPlayer(player, menu.Parent);
            }
        }
    }

    public void CloseAllMenus()
    {
        openMenus.ToList().ForEach(kvp =>
        {
            var currentMenu = kvp.Value;
            while (currentMenu != null)
            {
                currentMenu.CloseForPlayer(kvp.Key);
                MenuClosed?.Invoke(this, new MenuManagerEventArgs { Player = kvp.Key, Menu = currentMenu });
                currentMenu = currentMenu.Parent;
            }
            _ = openMenus.TryRemove(kvp.Key, out _);
        });
    }
}