using Spectre.Console;

namespace PasswordBank.UI
{
    /// <summary>
    /// Defines the colors of the color palette used for PasswordBank's console UI.
    /// </summary>
    internal readonly struct ColorPalette
    {
        public static readonly Color PRIMARY       = Color.Green3;
        public static readonly Color SECONDARY     = Color.DarkOrange;
        public static readonly Color HIGHLIGHT     = Color.DarkOrange;
        public static readonly Color NEUTRAL       = Color.Gray;
        public static readonly Color NEUTRAL_LIGHT = Color.Gray70;
        public static readonly Color SUCCESS       = Color.Green3_1;
        public static readonly Color ERROR         = Color.Red;
    }

    /// <summary>
    /// Defines some Spectre.Console styles used for PasswordBank's console UI.
    /// </summary>
    internal readonly struct Styles
    {
        /// <summary>
        /// Style used throughout the application for highlighting the currently selected option.
        /// </summary>
        public static readonly Style HIGHLIGHT = new ()
        {
            Foreground = ColorPalette.HIGHLIGHT,
            Decoration = Decoration.None
        };

#if DEBUG
        /// <summary>
        /// [DEBUG ONLY] Settings for printing exception messages, used throughout the application.
        /// </summary>
        public static readonly ExceptionSettings EXCEPTION = new ()
        {
            Format = ExceptionFormats.ShortenPaths,
            Style = new ExceptionStyle
            {
                Exception     = new Style(ColorPalette.ERROR, null, Decoration.Bold),
                Message       = new Style(ColorPalette.ERROR),
                Method        = new Style(ColorPalette.SECONDARY),
                Path          = new Style(ColorPalette.SECONDARY),
                LineNumber    = new Style(ColorPalette.PRIMARY),
                ParameterType = new Style(ColorPalette.PRIMARY),
                Dimmed        = new Style(ColorPalette.NEUTRAL)
            }
        };
#endif
    }
}
