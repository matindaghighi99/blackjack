/**
 * Player model (plain-object factory + validation).
 *
 * Kept framework-agnostic so it can back an in-memory store now and an ORM/ODM schema
 * later without changing the controllers.
 */

const { randomUUID } = require('crypto');

const DEFAULT_STARTING_CHIPS = 5000;

function createPlayer({ displayName = 'Guest', chips = DEFAULT_STARTING_CHIPS } = {}) {
  return {
    playerId: randomUUID(),
    displayName,
    chips,
    level: 1,
    createdAt: new Date().toISOString(),
  };
}

/** Basic guard so we never persist a negative/NaN balance. */
function isValidChipAmount(amount) {
  return Number.isFinite(amount) && amount >= 0;
}

module.exports = { createPlayer, isValidChipAmount, DEFAULT_STARTING_CHIPS };
