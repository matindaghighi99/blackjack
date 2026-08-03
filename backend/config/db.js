/**
 * In-memory data store (placeholder).
 *
 * Replace this module with a real database driver. Every consumer only depends on the
 * small async interface below, so swapping in Mongo/Postgres/Firebase means rewriting
 * this file only — controllers stay unchanged.
 */

const players = new Map(); // playerId -> player object
const processedTransactions = new Set(); // store transaction ids already validated (anti-replay)

module.exports = {
  players,
  processedTransactions,

  /** Seeds one demo player so the endpoints return data out of the box. */
  seed() {
    if (players.size === 0) {
      players.set('demo-player', {
        playerId: 'demo-player',
        displayName: 'Demo Player',
        chips: 5000,
        level: 1,
        createdAt: new Date().toISOString(),
      });
    }
  },
};
