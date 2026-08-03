/**
 * IAP receipt validation controller.
 *
 * Verifies a purchase receipt is genuine before the client is allowed to grant chips, and
 * records the transaction id to block replays (the same receipt can't be cashed twice).
 *
 * ⚠️ The Apple/Google verification functions below are STUBS. They return "valid" in dev
 * (gated by IAP_ALLOW_UNVERIFIED) so the flow is testable without store credentials.
 * Before production, implement the real calls:
 *   - Apple:  POST the receipt to https://buy.itunes.apple.com/verifyReceipt
 *             (fall back to the sandbox URL on status 21007) using your app shared secret.
 *   - Google: call the Play Developer API
 *             purchases.products.get(packageName, productId, purchaseToken) with a
 *             service-account credential.
 */

const { processedTransactions } = require('../config/db');

const ALLOW_UNVERIFIED = process.env.IAP_ALLOW_UNVERIFIED === 'true' || process.env.NODE_ENV !== 'production';

async function verifyApple(_receipt) {
  // TODO: real App Store verifyReceipt call with shared secret + sandbox fallback.
  return { ok: ALLOW_UNVERIFIED, reason: ALLOW_UNVERIFIED ? 'dev-accept' : 'apple verification not implemented' };
}

async function verifyGoogle(_productId, _receipt) {
  // TODO: real Google Play Developer API purchases.products.get call.
  return { ok: ALLOW_UNVERIFIED, reason: ALLOW_UNVERIFIED ? 'dev-accept' : 'google verification not implemented' };
}

/** POST /api/iap/validate  { playerId, platform, productId, transactionId, receipt } */
async function validate(req, res) {
  const { platform, productId, transactionId, receipt } = req.body ?? {};

  if (!productId || !transactionId) {
    return res.status(400).json({ valid: false, message: 'productId and transactionId are required' });
  }

  // Anti-replay: a transaction id may only be validated (and thus granted) once.
  if (processedTransactions.has(transactionId)) {
    return res.status(409).json({ valid: false, message: 'Transaction already processed' });
  }

  let result;
  switch (platform) {
    case 'Apple':
      result = await verifyApple(receipt);
      break;
    case 'GooglePlay':
      result = await verifyGoogle(productId, receipt);
      break;
    case 'Fake': // editor / mock builds
      result = { ok: ALLOW_UNVERIFIED, reason: 'fake platform (dev)' };
      break;
    default:
      return res.status(400).json({ valid: false, message: `Unsupported platform '${platform}'` });
  }

  if (!result.ok) {
    return res.status(200).json({ valid: false, message: `Rejected: ${result.reason}` });
  }

  processedTransactions.add(transactionId);
  return res.status(200).json({ valid: true, message: `Validated (${result.reason})` });
}

module.exports = { validate };
