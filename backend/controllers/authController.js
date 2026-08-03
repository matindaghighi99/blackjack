/**
 * Placeholder auth controller.
 *
 * SECURITY WARNING: This is NOT real authentication. It issues a fake token and does no
 * password checking. Replace with a real provider (Firebase Auth, Auth0, or JWT + hashed
 * credentials) before shipping. Included only so the client has an endpoint to call.
 */

const { players } = require('../config/db');
const { createPlayer } = require('../models/Player');

/** POST /api/auth/guest — issue a guest session and a (fake) token. */
function guestLogin(req, res) {
  const player = createPlayer({ displayName: req.body?.displayName || 'Guest' });
  players.set(player.playerId, player);

  // TODO: replace with a signed JWT from a real auth provider.
  const token = `dev-token-${player.playerId}`;

  res.status(201).json({
    token,
    player,
    note: 'Placeholder auth — do not use in production.',
  });
}

/** Middleware stub: accepts any "dev-token-*" bearer token. */
function requireAuth(req, res, next) {
  const header = req.headers.authorization || '';
  const token = header.startsWith('Bearer ') ? header.slice(7) : header;
  if (!token.startsWith('dev-token-')) {
    return res.status(401).json({ error: 'Missing or invalid token (placeholder auth)' });
  }
  next();
}

module.exports = { guestLogin, requireAuth };
