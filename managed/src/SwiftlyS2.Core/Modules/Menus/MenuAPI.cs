using System.Collections.Concurrent;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Core.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace SwiftlyS2.Core.Menus;

internal sealed class MenuAPI : IMenuAPI
{
    /// <summary>
    /// Configuration settings for this menu.
    /// </summary>
    public MenuConfiguration Configuration { get; init; }

    /// <summary>
    /// Keybind overrides for this menu.
    /// </summary>
    public MenuKeybindOverrides KeybindOverrides { get; init; }

    /// <summary>
    /// The scroll style for this menu options.
    /// </summary>
    public MenuOptionScrollStyle OptionScrollStyle { get; init; }

    /// <summary>
    /// The text overflow style for menu options.
    /// </summary>
    public MenuOptionTextStyle OptionTextStyle { get; init; }

    /// <summary>
    /// The builder used to construct and configure this menu.
    /// </summary>
    public IMenuBuilderAPI? Builder { get; init; }

    /// <summary>
    /// The parent menu in a hierarchical menu structure, or null if this is a top-level menu.
    /// </summary>
    public IMenuAPI? Parent { get; init; }

    /// <summary>
    /// Read-only collection of all options in this menu.
    /// </summary>
    public IReadOnlyList<IMenuOption> Options {
        get {
            lock (optionsLock)
            {
                return options.AsReadOnly();
            }
        }
    }

    // /// <summary>
    // /// Fired before a player navigates to a different menu option.
    // /// </summary>
    // public event EventHandler<MenuEventArgs>? BeforeSelectionMove;

    // /// <summary>
    // /// Fired after a player navigates to a different menu option.
    // /// </summary>
    // public event EventHandler<MenuEventArgs>? AfterSelectionMove;

    /// <summary>
    /// Fired when the selection pointer is hovering over an option.
    /// </summary>
    public event EventHandler<MenuEventArgs>? OptionHovering;

    // /// <summary>
    // /// Fired when an option is about to enter the visible viewport.
    // /// </summary>
    // public event EventHandler<MenuEventArgs>? OptionEntering;

    // /// <summary>
    // /// Fired when an option is about to leave the visible viewport.
    // /// </summary>
    // public event EventHandler<MenuEventArgs>? OptionLeaving;

    private readonly ISwiftlyCore core;
    private readonly List<IMenuOption> options = new();
    // TODO: Replace with `Lock` when framework is upgraded to .NET 10 for better lock performance
    private readonly object optionsLock = new(); // Lock for synchronizing modifications to the `options`
    private readonly ConcurrentDictionary<IPlayer, int> selectedOptionIndex = new(); // Stores the currently selected option index for each player
    // NOTE: Menu selection movement is entirely driven by changes to `desiredOptionIndex` (independent of any other variables)
    private readonly ConcurrentDictionary<IPlayer, int> desiredOptionIndex = new(); // Stores the desired option index for each player
    private int maxOptions = 0;
    // private readonly ConcurrentDictionary<IPlayer, int> selectedDisplayLine = new(); // Stores the currently selected display line index for each player (some options may span multiple lines)
    // private int maxDisplayLines = 0;
    // private readonly ConcurrentDictionary<IPlayer, IReadOnlyList<IMenuOption>> visibleOptionsCache = new();
    private readonly ConcurrentDictionary<IPlayer, CancellationTokenSource> autoCloseCancelTokens = new();

    // [SetsRequiredMembers]
    public MenuAPI( ISwiftlyCore core, MenuConfiguration configuration, MenuKeybindOverrides keybindOverrides, IMenuBuilderAPI? builder = null, IMenuAPI? parent = null, MenuOptionScrollStyle optionScrollStyle = MenuOptionScrollStyle.CenterFixed, MenuOptionTextStyle optionTextStyle = MenuOptionTextStyle.TruncateEnd )
    {
        this.core = core;
        Configuration = configuration;
        KeybindOverrides = keybindOverrides;
        OptionScrollStyle = optionScrollStyle;
        OptionTextStyle = optionTextStyle;
        Builder = builder;
        Parent = parent;

        options.Clear();
        selectedOptionIndex.Clear();
        desiredOptionIndex.Clear();
        // selectedDisplayLine.Clear();
        autoCloseCancelTokens.Clear();
        // visibleOptionsCache.Clear();

        maxOptions = 0;
        // maxDisplayLines = 0;

        core.Event.OnTick += OnTick;
    }

    ~MenuAPI()
    {
        core.PlayerManager
            .GetAllPlayers()
            .ToList()
            .ForEach(player => CloseForPlayer(player));

        options.Clear();
        selectedOptionIndex.Clear();
        desiredOptionIndex.Clear();
        // selectedDisplayLine.Clear();
        autoCloseCancelTokens.Clear();
        // visibleOptionsCache.Clear();

        maxOptions = 0;
        // maxDisplayLines = 0;

        core.Event.OnTick -= OnTick;
    }

    private void OnTick()
    {
        var playerStates = core.PlayerManager
            .GetAllPlayers()
            .Where(player => player.IsValid && !player.IsFakeClient)
            .Select(player => (
                Player: player,
                DesiredIndex: desiredOptionIndex.TryGetValue(player, out var desired) ? desired : -1,
                SelectedIndex: selectedOptionIndex.TryGetValue(player, out var selected) ? selected : -1
            ))
            .Where(state => state.DesiredIndex >= 0 && state.SelectedIndex >= 0 && state.DesiredIndex != state.SelectedIndex)
            .ToList();

        var maxVisibleItems = Math.Clamp(
            Configuration.MaxVisibleItems switch {
                < 1 => core.MenusAPI.Configuration.ItemsPerPage,
                var value => value
            },
            1,
            5
        );

        var centerIndex = maxVisibleItems / 2;

        foreach (var (player, desiredIndex, selectedIndex) in playerStates)
        {
            // TODO
            // BeforeSelectionMove?.Invoke(this, new MenuEventArgs { Player = player, Options = visibleOptionsCache.TryGetValue(player, out var options) ? options : null });

            // TODO
            OptionHovering?.Invoke(this, new MenuEventArgs { Player = player, Options = null });

            // TODO
            // AfterSelectionMove?.Invoke(this, new MenuEventArgs { Player = player, Options = null });
        }

        playerStates
            .ForEach(state => selectedOptionIndex.TryUpdate(state.Player, state.DesiredIndex, state.SelectedIndex));
    }

    public void ShowForPlayer( IPlayer player )
    {
        _ = selectedOptionIndex.AddOrUpdate(player, 0, ( _, _ ) => 0);
        _ = desiredOptionIndex.AddOrUpdate(player, 0, ( _, _ ) => 0);
        // _ = selectedDisplayLine.AddOrUpdate(player, 0, ( _, _ ) => 0);

        if (!player.IsValid || player.IsFakeClient)
        {
            return;
        }

        SetFreezeState(player, Configuration.FreezePlayer);

        if (Configuration.AutoCloseAfter > 0)
        {
            _ = autoCloseCancelTokens.AddOrUpdate(
                player,
                _ => core.Scheduler.DelayBySeconds(Configuration.AutoCloseAfter, () => CloseForPlayer(player)),
                ( _, oldToken ) =>
                {
                    oldToken.Cancel();
                    return core.Scheduler.DelayBySeconds(Configuration.AutoCloseAfter, () => CloseForPlayer(player));
                }
            );
        }
    }

    public void CloseForPlayer( IPlayer player )
    {
        var keyExists = selectedOptionIndex.TryRemove(player, out _) || desiredOptionIndex.TryRemove(player, out _)/* || selectedDisplayLine.TryRemove(player, out _)*/;

        if (!player.IsValid || player.IsFakeClient)
        {
            return;
        }

        if (keyExists)
        {
            NativePlayer.ClearCenterMenuRender(player.PlayerID);
        }

        SetFreezeState(player, false);

        if (autoCloseCancelTokens.TryRemove(player, out var token))
        {
            token.Cancel();
        }
    }

    public void AddOption( IMenuOption option )
    {
        lock (optionsLock)
        {
            option.Click += OnOptionClick;
            options.Add(option);
            maxOptions = options.Count;
            // maxDisplayLines = options.Sum(option => option.LineCount);
        }
    }

    public bool RemoveOption( IMenuOption option )
    {
        lock (optionsLock)
        {
            var result = options.Remove(option);
            maxOptions = options.Count;
            // maxDisplayLines = options.Sum(option => option.LineCount);
            return result;
        }
    }

    public bool MoveToOption( IPlayer player, IMenuOption option )
    {
        lock (optionsLock)
        {
            var index = options.IndexOf(option);
            return index >= 0 && index < maxOptions && desiredOptionIndex.TryGetValue(player, out var oldIndex) && desiredOptionIndex.TryUpdate(player, Math.Clamp(index, 0, maxOptions - 1), oldIndex);
        }
    }

    public bool MoveToOptionIndex( IPlayer player, int index )
    {
        return index >= 0 && index < maxOptions && desiredOptionIndex.TryGetValue(player, out var oldIndex) && desiredOptionIndex.TryUpdate(player, Math.Clamp(index, 0, maxOptions - 1), oldIndex);
    }

    public IMenuOption? GetCurrentOption( IPlayer player )
    {
        return selectedOptionIndex.TryGetValue(player, out var index) ? options[index] : null;
    }

    public int GetCurrentOptionIndex( IPlayer player )
    {
        return selectedOptionIndex.TryGetValue(player, out var index) ? index : -1;
    }

    // public int GetCurrentOptionDisplayLine( IPlayer player )
    // {
    //     return selectedDisplayLine.TryGetValue(player, out var line) ? line : -1;
    // }

    private static void SetFreezeState( IPlayer player, bool freeze )
    {
        if (!player.IsValid || player.IsFakeClient || !(player.PlayerPawn?.IsValid ?? false))
        {
            return;
        }

        var moveType = freeze ? MoveType_t.MOVETYPE_NONE : MoveType_t.MOVETYPE_WALK;
        player.PlayerPawn.MoveType = moveType;
        player.PlayerPawn.ActualMoveType = moveType;
        player.PlayerPawn.MoveTypeUpdated();
    }

    private ValueTask OnOptionClick( object? sender, MenuOptionClickEventArgs args )
    {
        if (args.CloseMenu)
        {
            CloseForPlayer(args.Player);
        }

        return ValueTask.CompletedTask;
    }
}