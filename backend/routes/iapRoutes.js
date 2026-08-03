const express = require('express');
const router = express.Router();

const iapController = require('../controllers/iapController');

// Validate a store receipt before the client grants chips.
router.post('/validate', iapController.validate);

module.exports = router;
