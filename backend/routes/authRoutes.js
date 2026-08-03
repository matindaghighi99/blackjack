const express = require('express');
const router = express.Router();

const authController = require('../controllers/authController');

// Placeholder guest login (no real security).
router.post('/guest', authController.guestLogin);

module.exports = router;
