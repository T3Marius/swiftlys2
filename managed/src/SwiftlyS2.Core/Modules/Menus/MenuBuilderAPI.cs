using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;

namespace SwiftlyS2.Core.Menus;

internal sealed class MenuBuilderAPI : IMenuBuilderAPI
{
    /// <summary>
    /// Gets the design interface for this menu.
    /// </summary>
    public IMenuDesignAPI Design { get => design ??= new MenuDesignAPI(configuration, this, style => optionScrollStyle = style/*, style => optionTextStyle = style*/); }

    private readonly ISwiftlyCore core;
    private readonly MenuConfiguration configuration = new();
    private readonly List<IMenuOption> options = new();
    private MenuKeybindOverrides keybindOverrides = new();
    private MenuOptionScrollStyle optionScrollStyle = MenuOptionScrollStyle.CenterFixed;
    // private MenuOptionTextStyle optionTextStyle = MenuOptionTextStyle.TruncateEnd;
    private IMenuAPI? parent = null;
    private IMenuDesignAPI? design = null;

    public MenuBuilderAPI( ISwiftlyCore core )
    {
        this.core = core;
        options.Clear();
    }

    public IMenuBuilderAPI WithParent( IMenuAPI parent )
    {
        this.parent = parent;
        return this;
    }

    public IMenuBuilderAPI AddOption( IMenuOption option )
    {
        options.Add(option);
        return this;
    }

    public IMenuBuilderAPI PlaySound( bool playSound = false )
    {
        configuration.PlaySound = playSound;
        return this;
    }

    public IMenuBuilderAPI FreezePlayer( bool freeze = false )
    {
        configuration.FreezePlayer = freeze;
        return this;
    }

    public IMenuBuilderAPI AutoClose( float seconds = 0f )
    {
        configuration.AutoCloseAfter = seconds;
        return this;
    }

    public IMenuBuilderAPI OverrideSelectButton( KeyBind keyBind )
    {
        keybindOverrides = keybindOverrides with { Select = keyBind };
        return this;
    }

    public IMenuBuilderAPI OverrideMoveButton( KeyBind keyBind )
    {
        keybindOverrides = keybindOverrides with { Move = keyBind };
        return this;
    }

    public IMenuBuilderAPI OverrideMoveBackButton( KeyBind keyBind )
    {
        keybindOverrides = keybindOverrides with { MoveBack = keyBind };
        return this;
    }

    public IMenuBuilderAPI OverrideExitButton( KeyBind keyBind )
    {
        keybindOverrides = keybindOverrides with { Exit = keyBind };
        return this;
    }

    public IMenuAPI Build()
    {
        var menu = new MenuAPI(core, configuration, keybindOverrides, this, parent, optionScrollStyle/*, optionTextStyle*/);
        options.ForEach(option => menu.AddOption(option));
        return menu;
    }
}