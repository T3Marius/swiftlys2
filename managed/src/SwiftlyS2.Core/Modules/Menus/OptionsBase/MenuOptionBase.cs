using System.Text;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace SwiftlyS2.Core.Menus.OptionsBase;

/// <summary>
/// Provides a base implementation for menu options with event-driven behavior.
/// </summary>
public abstract partial class MenuOptionBase : IMenuOption
{
    private string text = string.Empty;
    private float maxWidth = 26f;
    private MenuOptionTextStyle textStyle = MenuOptionTextStyle.TruncateEnd;
    private bool visible = true;
    private bool enabled = true;

    protected MenuOptionBase()
    {
        scrollOffsets.Clear();

        updateCancellationTokenSource = new CancellationTokenSource();
        var token = updateCancellationTokenSource.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    dynamicText = ApplyHorizontalStyle(text);
                    await Task.Delay(100, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                }
            }
        }, token);
    }

    ~MenuOptionBase()
    {
        updateCancellationTokenSource?.Cancel();
        updateCancellationTokenSource?.Dispose();
        scrollOffsets.Clear();
    }

    // /// <summary>
    // /// Gets or sets the menu that this option belongs to.
    // /// </summary>
    // public IMenuAPI? Menu { get; init; }

    /// <summary>
    /// Gets the number of lines this option requests to occupy in the menu.
    /// </summary>
    public virtual int LineCount => 1;

    /// <summary>
    /// Gets or sets the text content displayed for this menu option.
    /// </summary>
    /// <remarks>
    /// This is a global property. Changing it will affect what all players see.
    /// </remarks>
    public string Text {
        get => text;
        set {
            if (text == value)
            {
                return;
            }

            text = value;

            dynamicText = string.Empty;
            scrollOffsets.Clear();

            TextChanged?.Invoke(this, new MenuOptionEventArgs { Player = null!, Option = this });
        }
    }

    /// <summary>
    /// The maximum display width for menu option text in relative units.
    /// </summary>
    public float MaxWidth {
        get => maxWidth;
        set {
            if (maxWidth == value)
            {
                return;
            }

            if (value < 1f)
            {
                Spectre.Console.AnsiConsole.WriteException(new ArgumentOutOfRangeException(nameof(MaxWidth), $"MaxWidth: value {value:F3} is out of range."));
            }

            maxWidth = Math.Max(value, 1f);

            dynamicText = string.Empty;
            scrollOffsets.Clear();
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this option is visible in the menu.
    /// </summary>
    /// <remarks>
    /// This is a global property. Changing it will affect what all players see.
    /// </remarks>
    public bool Visible {
        get => visible;
        set {
            if (visible == value)
            {
                return;
            }

            visible = value;
            VisibilityChanged?.Invoke(this, new MenuOptionEventArgs { Player = null!, Option = this });
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this option can be interacted with.
    /// </summary>
    /// <remarks>
    /// This is a global property. Changing it will affect what all players see.
    /// </remarks>
    public bool Enabled {
        get => enabled;
        set {
            if (enabled == value)
            {
                return;
            }

            enabled = value;
            EnabledChanged?.Invoke(this, new MenuOptionEventArgs { Player = null!, Option = this });
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the menu should be closed after handling the click.
    /// </summary>
    public bool CloseAfterClick { get; init; } = false;

    /// <summary>
    /// Gets or sets an object that contains data about this option.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Gets or sets the text size for this option.
    /// </summary>
    public MenuOptionTextSize TextSize { get; set; } = MenuOptionTextSize.Medium;

    /// <summary>
    /// Gets or sets the text overflow style for this option.
    /// </summary>
    public MenuOptionTextStyle TextStyle {
        get => textStyle;
        set {
            if (textStyle == value)
            {
                return;
            }

            textStyle = value;

            dynamicText = string.Empty;
            scrollOffsets.Clear();
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether a sound should play when this option is selected.
    /// </summary>
    public bool PlaySound { get; set; } = true;

    /// <summary>
    /// Occurs when the visibility of the option changes.
    /// </summary>
    public event EventHandler<MenuOptionEventArgs>? VisibilityChanged;

    /// <summary>
    /// Occurs when the enabled state of the option changes.
    /// </summary>
    public event EventHandler<MenuOptionEventArgs>? EnabledChanged;

    /// <summary>
    /// Occurs when the text of the option changes.
    /// </summary>
    public event EventHandler<MenuOptionEventArgs>? TextChanged;

    /// <summary>
    /// Occurs before a click is processed, allowing validation and cancellation.
    /// </summary>
    public event EventHandler<MenuOptionValidatingEventArgs>? Validating;

    /// <summary>
    /// Occurs when the option is clicked by a player.
    /// </summary>
    public event AsyncEventHandler<MenuOptionClickEventArgs>? Click;

    // /// <summary>
    // /// Occurs when a player's cursor enters this option.
    // /// </summary>
    // public event EventHandler<MenuOptionEventArgs>? Hover;

    /// <summary>
    /// Occurs before HTML markup is assembled, allowing customization of the text content.
    /// </summary>
    public event EventHandler<MenuOptionFormattingEventArgs>? BeforeFormat;

    /// <summary>
    /// Occurs after HTML markup is assembled, allowing customization of the final HTML output.
    /// </summary>
    public event EventHandler<MenuOptionFormattingEventArgs>? AfterFormat;

    /// <summary>
    /// Determines whether this option is visible to the specified player.
    /// </summary>
    /// <param name="player">The player to check visibility for.</param>
    /// <returns>True if the option is visible to the player; otherwise, false.</returns>
    public virtual bool GetVisible( IPlayer player ) => Visible;

    /// <summary>
    /// Determines whether this option is enabled for the specified player.
    /// </summary>
    /// <param name="player">The player to check enabled state for.</param>
    /// <returns>True if the option is enabled for the player; otherwise, false.</returns>
    public virtual bool GetEnabled( IPlayer player ) => Enabled;

    // /// <summary>
    // /// Gets the text to display for this option for the specified player.
    // /// </summary>
    // /// <param name="player">The player requesting the text.</param>
    // /// <returns>The text to display.</returns>
    // public virtual string GetText( IPlayer player ) => Text;

    // /// <summary>
    // /// Gets the formatted HTML markup for this option.
    // /// </summary>
    // /// <param name="player">The player to format for.</param>
    // /// <returns>The formatted HTML string.</returns>
    // public virtual string GetFormattedHtmlText( IPlayer player )
    // {
    //     var args = new MenuOptionFormattingEventArgs {
    //         Player = player,
    //         Option = this,
    //         CustomText = null
    //     };

    //     BeforeFormat?.Invoke(this, args);

    //     var displayText = args.CustomText ?? GetText(player);
    //     var isEnabled = GetEnabled(player);
    //     var sizeClass = GetSizeClass(TextSize);

    //     var colorStyle = isEnabled ? "" : " color='grey'";
    //     var result = $"<font class='{sizeClass}'{colorStyle}>{displayText}</font>";

    //     args.CustomText = result;
    //     AfterFormat?.Invoke(this, args);

    //     return args.CustomText;
    // }

    /// <summary>
    /// Gets the display text for this option as it should appear to the specified player.
    /// </summary>
    /// <param name="player">The player requesting the display text.</param>
    /// <param name="displayLine">The display line index of the option.</param>
    /// <returns>The formatted display text for the option.</returns>
    /// <remarks>
    /// When a menu option occupies multiple lines, MenuAPI may only need to display a specific line of that option.
    /// <list type="bullet">
    /// <item>When <c>LineCount=1</c>: The <c>displayLine</c> parameter is not needed; return the HTML-formatted string directly.</item>
    /// <item>When <c>LineCount>=2</c>: Check the <c>displayLine</c> parameter:
    ///   <list type="bullet">
    ///   <item><c>displayLine=0</c>: Return all content</item>
    ///   <item><c>displayLine=1</c>: Return only the first line content</item>
    ///   <item><c>displayLine=2</c>: Return only the second line content</item>
    ///   <item>And so on...</item>
    ///   </list>
    /// </item>
    /// </list>
    /// Note: MenuAPI ensures that the <c>displayLine</c> parameter will not exceed the option's <c>LineCount</c>.
    /// </remarks>
    public virtual string GetDisplayText( IPlayer player, int displayLine = 0 )
    {
        var args = new MenuOptionFormattingEventArgs {
            Player = player,
            Option = this,
            CustomText = null
        };

        BeforeFormat?.Invoke(this, args);

        var displayText = args.CustomText ?? dynamicText;

        if (displayLine > 0)
        {
            var lines = BrTagRegex().Split(displayText);
            if (displayLine <= lines.Length)
            {
                displayText = lines[displayLine - 1];
            }
        }

        var isEnabled = GetEnabled(player);
        var sizeClass = GetSizeClass(TextSize);

        var colorStyle = isEnabled ? string.Empty : " color='grey'";
        var result = $"<font class='{sizeClass}'{colorStyle}>{displayText}</font>";

        args.CustomText = result;
        AfterFormat?.Invoke(this, args);

        return args.CustomText;
    }

    /// <summary>
    /// Validates whether the specified player can interact with this option.
    /// </summary>
    /// <param name="player">The player to validate.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is true if validation succeeds; otherwise, false.</returns>
    public virtual ValueTask<bool> OnValidatingAsync( IPlayer player )
    {
        if (Validating == null)
        {
            return ValueTask.FromResult(true);
        }

        var args = new MenuOptionValidatingEventArgs {
            Player = player,
            Option = this,
            Cancel = false
        };

        Validating?.Invoke(this, args);
        return ValueTask.FromResult(!args.Cancel);
    }

    // /// <summary>
    // /// Handles the click action for this option.
    // /// </summary>
    // /// <param name="player">The player who clicked the option.</param>
    // /// <param name="closeMenu">Whether to close the menu after handling the click.</param>
    // /// <returns>A task that represents the asynchronous operation.</returns>
    // public virtual async ValueTask OnClickAsync( IPlayer player, bool closeMenu = false )
    // {
    //     if (!await OnValidatingAsync(player))
    //     {
    //         return;
    //     }

    //     if (Click != null)
    //     {
    //         var args = new MenuOptionClickEventArgs {
    //             Player = player,
    //             Option = this,
    //             CloseMenu = closeMenu
    //         };

    //         await Click.Invoke(this, args);

    //         // if (args.CloseMenu)
    //         // {
    //         //     Menu?.CloseForPlayer(player);
    //         // }
    //     }
    // }

    /// <summary>
    /// Handles the click action for this option.
    /// </summary>
    /// <param name="player">The player who clicked the option.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async ValueTask OnClickAsync( IPlayer player )
    {
        if (!visible || !enabled)
        {
            return;
        }

        if (!await OnValidatingAsync(player))
        {
            return;
        }

        if (Click != null)
        {
            var args = new MenuOptionClickEventArgs {
                Player = player,
                Option = this,
                CloseMenu = CloseAfterClick
            };

            await Click.Invoke(this, args);
        }
    }

    // /// <summary>
    // /// Raises the <see cref="Hover"/> event.
    // /// </summary>
    // /// <param name="player">The player whose cursor entered the option.</param>
    // protected virtual void OnHover( IPlayer player )
    // {
    //     Hover?.Invoke(this, new MenuOptionEventArgs { Player = player, Option = this });
    // }

    protected static string GetSizeClass( MenuOptionTextSize size )
    {
        return size switch {
            MenuOptionTextSize.ExtraSmall => "fontSize-xs",
            MenuOptionTextSize.Small => "fontSize-s",
            MenuOptionTextSize.SmallMedium => "fontSize-sm",
            MenuOptionTextSize.Medium => "fontSize-m",
            MenuOptionTextSize.MediumLarge => "fontSize-ml",
            MenuOptionTextSize.Large => "fontSize-l",
            MenuOptionTextSize.ExtraLarge => "fontSize-xl",
            _ => "fontSize-m"
        };
    }

    [GeneratedRegex(@"<[/\\]*br[/\\]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BrTagRegex();

    [GeneratedRegex("<.*?>")]
    private static partial Regex HtmlTagRegex();

}

public partial class MenuOptionBase
{
    private string dynamicText = string.Empty;
    private readonly CancellationTokenSource updateCancellationTokenSource;
    private readonly ConcurrentDictionary<string, int> scrollOffsets = new();

    protected string ApplyHorizontalStyle( string text )
    {
        return string.IsNullOrWhiteSpace(text)
            ? text
            : Helper.EstimateTextWidth(StripHtmlTags(text)) <= MaxWidth
                ? text
                : TextStyle switch {
                    MenuOptionTextStyle.TruncateEnd => TruncateTextEnd(text, MaxWidth),
                    MenuOptionTextStyle.TruncateBothEnds => TruncateTextBothEnds(text, MaxWidth),
                    MenuOptionTextStyle.ScrollLeftFade => ScrollTextWithFade(text, MaxWidth, true),
                    MenuOptionTextStyle.ScrollRightFade => ScrollTextWithFade(text, MaxWidth, false),
                    MenuOptionTextStyle.ScrollLeftLoop => ScrollTextWithLoop($"{text.TrimEnd()} ", MaxWidth, true),
                    MenuOptionTextStyle.ScrollRightLoop => ScrollTextWithLoop($" {text.TrimStart()}", MaxWidth, false),
                    _ => text
                };
    }

    private string ScrollTextWithFade( string text, float maxWidth, bool scrollLeft )
    {
        // Prepare scroll data and validate
        var (plainChars, segments, targetCharCount) = PrepareScrollData(text, maxWidth);
        if (plainChars is null)
        {
            return text;
        }
        if (targetCharCount == 0)
        {
            return string.Empty;
        }

        // Update scroll offset (allow scrolling beyond end for complete fade-out)
        var offset = UpdateScrollOffset(StripHtmlTags(text), scrollLeft, plainChars.Length + 1);

        // Calculate visible character range
        var (skipStart, skipEnd) = scrollLeft
            ? (offset, Math.Max(0, plainChars.Length - offset - targetCharCount))
            : (Math.Max(0, plainChars.Length - targetCharCount - offset), offset);

        // Build output with proper HTML tag tracking
        StringBuilder result = new();
        List<string> outputTags = [], activeTags = [];
        var (charIdx, started) = (0, false);

        foreach (var (content, isTag) in segments)
        {
            if (isTag)
            {
                // Track active opening and closing tags
                UpdateTagState(content, activeTags);

                // Output tags within visible window
                if (started)
                {
                    result.Append(content);
                    ProcessOpenTag(content, outputTags);
                }
            }
            else
            {
                // Process characters within scroll window
                foreach (var ch in content)
                {
                    if (charIdx >= skipStart && charIdx < plainChars.Length - skipEnd)
                    {
                        // Apply active tags at start of output
                        if (!started)
                        {
                            started = true;
                            activeTags.ForEach(tag => { result.Append(tag); ProcessOpenTag(tag, outputTags); });
                        }
                        result.Append(ch);
                    }
                    charIdx++;
                }
            }
        }

        CloseOpenTags(result, outputTags);
        return result.ToString();
    }

    private string ScrollTextWithLoop( string text, float maxWidth, bool scrollLeft )
    {
        // Prepare scroll data and validate
        var (plainChars, segments, targetCharCount) = PrepareScrollData(text, maxWidth);
        if (plainChars is null)
        {
            return text;
        }
        if (targetCharCount == 0)
        {
            return string.Empty;
        }

        // Update scroll offset for circular wrapping
        var offset = UpdateScrollOffset(StripHtmlTags(text), scrollLeft, plainChars.Length);

        // Build character-to-tags mapping for circular access
        Dictionary<int, List<string>> charToActiveTags = [];
        List<string> currentActiveTags = [];
        var currentCharIdx = 0;

        foreach (var (content, isTag) in segments)
        {
            if (isTag)
            {
                // Track active opening and closing tags
                UpdateTagState(content, currentActiveTags);
            }
            else
            {
                // Map each character to its active tags
                foreach (var ch in content)
                {
                    charToActiveTags[currentCharIdx] = [.. currentActiveTags];
                    currentCharIdx++;
                }
            }
        }

        // Build output in circular order with dynamic tag management
        StringBuilder result = new();
        List<string> outputTags = [];
        List<string>? previousTags = null;

        for (int i = 0; i < targetCharCount; i++)
        {
            // Calculate circular character index
            var charIndex = scrollLeft
                ? (offset + i) % plainChars.Length
                : (plainChars.Length - offset + i) % plainChars.Length;
            var currentTags = charToActiveTags.GetValueOrDefault(charIndex, []);

            // Close tags that are no longer active
            if (previousTags is not null)
            {
                for (int j = previousTags.Count - 1; j >= 0; j--)
                {
                    if (!currentTags.Contains(previousTags[j]))
                    {
                        var prevTagName = previousTags[j][1..^1].Split(' ')[0];
                        result.Append($"</{prevTagName}>");
                        var idx = outputTags.FindLastIndex(t => t.Equals(prevTagName, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0)
                        {
                            outputTags.RemoveAt(idx);
                        }
                    }
                }
            }

            // Open new tags that are now active
            foreach (var tag in currentTags)
            {
                if (previousTags is null || !previousTags.Contains(tag))
                {
                    result.Append(tag);
                    var tagName = tag[1..^1].Split(' ')[0];
                    outputTags.Add(tagName);
                }
            }

            result.Append(plainChars[charIndex]);
            previousTags = currentTags;
        }

        CloseOpenTags(result, outputTags);
        return result.ToString();
    }

    private static string TruncateTextEnd( string text, float maxWidth, string suffix = "..." )
    {
        // Reserve space for suffix
        var targetWidth = maxWidth - Helper.EstimateTextWidth(suffix);
        if (targetWidth <= 0)
        {
            return suffix;
        }

        var segments = ParseHtmlSegments(text);
        StringBuilder result = new();
        List<string> openTags = [];
        var (currentWidth, reachedLimit) = (0f, false);

        foreach (var (content, isTag) in segments)
        {
            switch (isTag, reachedLimit)
            {
                // Preserve HTML tags before reaching limit
                case (true, false):
                    result.Append(content);
                    ProcessOpenTag(content, openTags);
                    break;

                // Process plain text characters until width limit
                case (false, false):
                    foreach (var ch in content)
                    {
                        var charWidth = Helper.GetCharWidth(ch);
                        if (currentWidth + charWidth > targetWidth)
                        {
                            reachedLimit = true;
                            break;
                        }
                        result.Append(ch);
                        currentWidth += charWidth;
                    }
                    break;
            }
        }

        if (reachedLimit)
        {
            result.Append(suffix);
        }

        CloseOpenTags(result, openTags);
        return result.ToString();
    }

    private static string TruncateTextBothEnds( string text, float maxWidth )
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Check if text fits without truncation
        var plainText = StripHtmlTags(text);
        if (Helper.EstimateTextWidth(plainText) <= maxWidth)
        {
            return text;
        }

        // Extract all plain text characters from segments
        var segments = ParseHtmlSegments(text);
        var plainChars = segments
            .Where(s => !s.IsTag)
            .SelectMany(s => s.Content)
            .ToArray();

        if (plainChars.Length == 0)
        {
            return text;
        }

        // Calculate how many characters can fit
        var targetCharCount = CalculateTargetCharCount(plainChars, maxWidth);
        if (targetCharCount == 0)
        {
            return string.Empty;
        }

        // Calculate range to keep from middle
        var skipFromStart = Math.Max(0, (plainChars.Length - targetCharCount) / 2);
        var skipFromEnd = plainChars.Length - skipFromStart - targetCharCount;

        StringBuilder result = new();
        List<string> outputOpenTags = [];
        List<string> pendingOpenTags = [];
        var (plainCharIndex, hasStartedOutput) = (0, false);

        foreach (var (content, isTag) in segments)
        {
            switch (isTag, hasStartedOutput)
            {
                // Process tags after output has started
                case (true, true):
                    result.Append(content);
                    ProcessOpenTag(content, outputOpenTags);
                    break;

                // Queue opening tags before output starts
                case (true, false) when !content.StartsWith("</") && !content.StartsWith("<!") && !content.EndsWith("/>"):
                    pendingOpenTags.Add(content);
                    break;

                // Process plain text, keeping only middle portion
                case (false, _):
                    foreach (var ch in content)
                    {
                        if (plainCharIndex >= skipFromStart && plainCharIndex < plainChars.Length - skipFromEnd)
                        {
                            // Start output and apply pending tags
                            if (!hasStartedOutput)
                            {
                                hasStartedOutput = true;
                                pendingOpenTags.ForEach(tag =>
                                {
                                    result.Append(tag);
                                    ProcessOpenTag(tag, outputOpenTags);
                                });
                            }
                            result.Append(ch);
                        }
                        plainCharIndex++;
                    }
                    break;
            }
        }

        CloseOpenTags(result, outputOpenTags);
        return result.ToString();
    }

    /// <summary>
    /// Removes all HTML tags from the given text.
    /// </summary>
    /// <param name="text">The text containing HTML tags.</param>
    /// <returns>The text with all HTML tags removed.</returns>
    private static string StripHtmlTags( string text )
    {
        return string.IsNullOrEmpty(text) ? text : HtmlTagRegex().Replace(text, string.Empty);
    }

    /// <summary>
    /// Parses text into segments, separating HTML tags from plain text content.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>A list of segments where each segment is either a tag or plain text content.</returns>
    private static List<(string Content, bool IsTag)> ParseHtmlSegments( string text )
    {
        var tagMatches = HtmlTagRegex().Matches(text);
        if (tagMatches.Count == 0)
        {
            return [(text, false)];
        }

        List<(string Content, bool IsTag)> segments = [];
        var currentIndex = 0;

        foreach (Match match in tagMatches)
        {
            if (match.Index > currentIndex)
            {
                segments.Add((text[currentIndex..match.Index], false));
            }
            segments.Add((match.Value, true));
            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < text.Length)
        {
            segments.Add((text[currentIndex..], false));
        }

        return segments;
    }

    /// <summary>
    /// Processes an HTML tag and updates the list of currently open tags.
    /// Adds opening tags to the list and removes matching closing tags.
    /// </summary>
    /// <param name="tag">The HTML tag to process.</param>
    /// <param name="openTags">The list of currently open tag names.</param>
    private static void ProcessOpenTag( string tag, List<string> openTags )
    {
        var tagName = tag switch {
            ['<', '/', .. var rest] => new string(rest).TrimEnd('>').Split(' ', 2)[0],
            ['<', '!', ..] => null,
            [.. var chars] when chars[^1] == '/' && chars[^2] == '>' => null,
            ['<', .. var rest] => new string(rest).TrimEnd('>').Split(' ', 2)[0],
            _ => null
        };

        if (tagName is null)
        {
            return;
        }

        if (tag.StartsWith("</"))
        {
            var index = openTags.FindLastIndex(t => t.Equals(tagName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) openTags.RemoveAt(index);
        }
        else
        {
            openTags.Add(tagName);
        }
    }

    /// <summary>
    /// Appends closing tags for all currently open tags in reverse order.
    /// </summary>
    /// <param name="result">The StringBuilder to append closing tags to.</param>
    /// <param name="openTags">The list of currently open tag names.</param>
    private static void CloseOpenTags( StringBuilder result, List<string> openTags )
    {
        openTags.AsEnumerable().Reverse().ToList().ForEach(tag => result.Append($"</{tag}>"));
    }

    /// <summary>
    /// Calculates how many characters can fit within the specified width.
    /// </summary>
    /// <param name="plainChars">The characters to measure.</param>
    /// <param name="maxWidth">The maximum width allowed.</param>
    /// <returns>The number of characters that fit within the width.</returns>
    private static int CalculateTargetCharCount( ReadOnlySpan<char> plainChars, float maxWidth )
    {
        var currentWidth = 0f;
        var count = 0;

        foreach (var ch in plainChars)
        {
            var charWidth = Helper.GetCharWidth(ch);
            if (currentWidth + charWidth > maxWidth) break;
            currentWidth += charWidth;
            count++;
        }

        return count;
    }

    /// <summary>
    /// Updates and returns the scroll offset for the given text.
    /// The offset increments based on tick count and wraps around at the specified length.
    /// </summary>
    /// <param name="plainText">The plain text being scrolled.</param>
    /// <param name="scrollLeft">Whether scrolling left or right.</param>
    /// <param name="wrapLength">The length at which the offset wraps around.</param>
    /// <returns>The current scroll offset, or -1 if the text is not being scrolled.</returns>
    private int UpdateScrollOffset( string plainText, bool scrollLeft, int wrapLength )
    {
        var newOffset = -1;
        var key = $"{plainText}_{scrollLeft}";

        if (scrollOffsets.TryGetValue(key, out var offset))
        {
            newOffset = (offset + 1) % wrapLength;
            _ = scrollOffsets.AddOrUpdate(key, newOffset, ( _, _ ) => newOffset);
        }

        return newOffset;
    }

    /// <summary>
    /// Updates the list of active tags based on the given HTML tag content.
    /// Adds opening tags and removes matching closing tags.
    /// </summary>
    /// <param name="content">The HTML tag content to process.</param>
    /// <param name="activeTags">The list of currently active tags.</param>
    private static void UpdateTagState( string content, List<string> activeTags )
    {
        if (!content.StartsWith("</") && !content.StartsWith("<!") && !content.EndsWith("/>"))
        {
            activeTags.Add(content);
        }
        else if (content.StartsWith("</"))
        {
            var tagName = content[2..^1].Split(' ')[0];
            var index = activeTags.FindLastIndex(t => t[1..^1].Split(' ')[0].Equals(tagName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                activeTags.RemoveAt(index);
            }
        }
    }

    /// <summary>
    /// Prepares data required for text scrolling by extracting plain characters and parsing segments.
    /// </summary>
    /// <param name="text">The text to prepare for scrolling.</param>
    /// <param name="maxWidth">The maximum width available for display.</param>
    /// <returns>A tuple containing plain characters array, HTML segments, and target character count.</returns>
    private static (char[]? PlainChars, List<(string Content, bool IsTag)> Segments, int TargetCharCount) PrepareScrollData( string text, float maxWidth )
    {
        var plainText = StripHtmlTags(text);
        if (Helper.EstimateTextWidth(plainText) <= maxWidth)
        {
            return (null, [], 0);
        }

        var segments = ParseHtmlSegments(text);
        var plainChars = segments.Where(s => !s.IsTag).SelectMany(s => s.Content).ToArray();

        if (plainChars.Length == 0)
        {
            return (null, segments, 0);
        }

        var targetCharCount = CalculateTargetCharCount(plainChars, maxWidth);
        return (plainChars, segments, targetCharCount);
    }
}