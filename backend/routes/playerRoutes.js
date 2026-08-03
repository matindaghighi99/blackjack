const express = require('express');
const router = express.Router();

const playerController = require('../controllers/playerController');

// Player profile
router.post('/', playerController.create);
router.get('/:id', playerController.getProfile);

// Chip balance
router.get('/:id/chips', playerController.getChips);
router.patch('/:id/chips', playerController.updateChips);

module.exports = router;
