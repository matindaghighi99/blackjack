/**
 * Social Casino Blackjack — Stub Backend
 * --------------------------------------
 * A minimal Express API scaffold for player profiles and virtual chip balances.
 *
 * NOTE: This is a scaffold only. Auth is a placeholder (no real security), and data
 * lives in an in-memory store that resets on restart. Swap `config/db` for a real
 * database (MongoDB/Postgres/Firebase) and `controllers/authController` for real auth
 * before going anywhere near production.
 */
require('dotenv').config();

const express = require('express');
const cors = require('cors');
const morgan = require('morgan');

const db = require('./config/db');
const playerRoutes = require('./routes/playerRoutes');
const authRoutes = require('./routes/authRoutes');

const app = express();
const PORT = process.env.PORT || 3000;

// Seed a demo player so the endpoints return data out of the box.
db.seed();

// ---- Middleware ----
app.use(cors());
app.use(express.json());
app.use(morgan('dev'));

// ---- Health check ----
app.get('/', (_req, res) => {
  res.json({ service: 'blackjack-social-casino-backend', status: 'ok', version: '0.1.0' });
});

// ---- Routes ----
app.use('/api/auth', authRoutes);
app.use('/api/players', playerRoutes);

// ---- 404 + error handling ----
app.use((_req, res) => res.status(404).json({ error: 'Not found' }));
app.use((err, _req, res, _next) => {
  console.error(err);
  res.status(500).json({ error: 'Internal server error' });
});

app.listen(PORT, () => {
  console.log(`[backend] Blackjack API listening on http://localhost:${PORT}`);
});

module.exports = app;
