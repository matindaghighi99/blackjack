using System;

namespace BlackjackGame.UI.Feedback
{
    /// <summary>
    /// Presentation moments a sound (or haptic) layer would want to hook. Raised by the
    /// components that animate them, at the frame the thing actually happens on screen —
    /// a card's deal cue fires when it leaves the shoe, not when the engine drew it.
    ///
    /// There is deliberately no shuffle cue: the engine shuffles a fresh shoe internally
    /// and does not expose that moment, and the UI does not invent one.
    /// </summary>
    public enum UiCue
    {
        ButtonClick,
        ChipPlace,
        ChipClear,
        /// <summary>A stake (and any winnings) sliding back to the player.</summary>
        ChipCollect,
        /// <summary>A losing stake swept away to the dealer.</summary>
        ChipLose,
        CardDeal,
        CardFlip,
        Blackjack,
        Win,
        Loss,
        Push,
        Bust,
        /// <summary>An action refused, e.g. a bet the balance cannot cover.</summary>
        Denied,
    }

    /// <summary>
    /// Sound-ready event hub. The project ships without audio, so nothing listens yet; an
    /// audio component only needs to subscribe to <see cref="Raised"/> and map cues to clips.
    /// </summary>
    public static class UiCues
    {
        public static event Action<UiCue> Raised;

        public static void Raise(UiCue cue) => Raised?.Invoke(cue);
    }
}
