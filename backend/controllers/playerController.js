/**
 * Player controller — profile + chip balance endpoints.
 * Backed by the in-memory store in config/db.js (placeholder persistence).
 */

const { players } = require('../config/db');
const { createPlayer, isValidChipAmount } = require('../models/Player');

/** POST /api/players — create a new player profile. */
function create(req, res) {
  const player = createPlayer({ displayName: req.body?.displayName });
  players.set(player.playerId, player);
  res.status(201).json(player);
}

/** GET /api/players/:id — fetch a player profile. */
function getProfile(req, res) {
  const player = players.get(req.params.id);
  if (!player) return res.status(404).json({ error: 'Player not found' });
  res.json(player);
}

/** GET /api/players/:id/chips — fetch just the chip balance. */
function getChips(req, res) {
  const player = players.get(req.params.id);
  if (!player) return res.status(404).json({ error: 'Player not found' });
  res.json({ playerId: player.playerId, chips: player.chips });
}

/**
 * PATCH /api/players/:id/chips — adjust chip balance by { delta } or set { chips }.
 * NOTE: no server-side authority or anti-cheat yet — trust-the-client placeholder.
 */
function updateChips(req, res) {
  const player = players.get(req.params.id);
  if (!player) return res.status(404).json({ error: 'Player not found' });

  const { delta, chips } = req.body ?? {};
  let next = player.chips;

  if (typeof chips === 'number') next = chips;
  else if (typeof delta === 'number') next = player.chips + delta;
  else return res.status(400).json({ error: 'Provide { delta } or { chips }' });

  if (!isValidChipAmount(next)) {
    return res.status(400).json({ error: 'Resulting balance must be a non-negative number' });
  }

  player.chips = next;
  res.json({ playerId: player.playerId, chips: player.chips });
}

module.exports = { create, getProfile, getChips, updateChips };
