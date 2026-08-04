using BlackjackGame.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// One purchasable row in the chip store: artwork, amount, optional bonus, price.
    ///
    /// Replaces the single-label row this screen used to have, where the whole pack was
    /// squeezed into one string ("Stack — 60,000 chips (+5,000 bonus)  $4.99") that
    /// overflowed its button at both ends.
    /// </summary>
    public sealed class StorePackRow : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _chipImage;
        [SerializeField] private TMP_Text _amountLabel;
        [SerializeField] private TMP_Text _bonusLabel;
        [SerializeField] private TMP_Text _priceLabel;
        [Tooltip("Shown only on the pack with the best bonus ratio.")]
        [SerializeField] private GameObject _bestValueBadge;

        public Button Button => _button;

        /// <summary>Fills the row in from a pack definition.</summary>
        public void Bind(ChipPack pack, Sprite chipArt, bool bestValue)
        {
            if (_chipImage != null && chipArt != null) _chipImage.sprite = chipArt;

            if (_amountLabel != null)
                _amountLabel.text = (pack.ChipAmount + pack.BonusChips).ToString("N0");

            if (_bonusLabel != null)
            {
                bool hasBonus = pack.BonusChips > 0;
                _bonusLabel.gameObject.SetActive(hasBonus);
                if (hasBonus) _bonusLabel.text = $"+{pack.BonusChips:N0} BONUS";
            }

            if (_priceLabel != null) _priceLabel.text = pack.PriceLabel;
            if (_bestValueBadge != null) _bestValueBadge.SetActive(bestValue);
        }
    }
}
