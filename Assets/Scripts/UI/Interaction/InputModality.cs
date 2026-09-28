namespace BlackjackGame.UI.Interaction
{
    /// <summary>
    /// Whether the player is currently driving the UI with a keyboard or a pointer.
    ///
    /// Focus rings follow the web's :focus-visible rule: they appear for keyboard users and
    /// stay hidden for mouse and touch, so a clicked button never wears a ring it doesn't
    /// need. <see cref="KeyboardNavigator"/> updates this from raw input each frame.
    /// </summary>
    public static class InputModality
    {
        public static bool Keyboard { get; private set; }

        public static void NoteKeyboard() => Keyboard = true;

        public static void NotePointer() => Keyboard = false;
    }
}
