# Dynamic KHQR Generator - Source Analysis Notes

Analysis of the read-only Node.js reference project (the `payway` service) and how its
Dynamic KHQR Generator was understood and re-implemented in this ASP.NET Core project.

This document contains **no secrets**. Configuration keys are named only; every value
(merchant id, API key, public key, bank account number, password, token, link id,
telegram/messaging secrets, etc.) is intentionally omitted.

## 1. Call chain of the Dynamic KHQR Generator (browser form to QR result)

The Dynamic KHQR Generator lives in the dashboard page. The exact chain is:

1. **Form submit.** `public/js/dashboard.js` binds the form `#dynamicKhqrGenForm`
   (`const dynKhqrForm = document.getElementById('dynamicKhqrGenForm')`). On submit it
   reads the input fields: `#dynKhqrPaywayLink`, `#dynKhqrMerchantName`,
   `#dynKhqrDonorName`, `#dynKhqrAmount`, `#dynKhqrCurrency`, `#dynKhqrExpiry`. The
   merchant name falls back to a hardcoded default when the field is empty, the amount
   defaults to `1.00`, currency defaults to `USD`, and expiry defaults to `15` minutes.
2. **Fetch.** It POSTs JSON to `/api/v1/payway/generate-khqr` with headers
   `Content-Type: application/json` and `x-requested-by: payway-dashboard`. Body keys:
   `payment_link`, `merchant_name`, `donor_name`, `amount`, `currency`,
   `expires_in_minutes`.
3. **Route.** `src/routes/api.js` handles `POST /payway/generate-khqr` behind the
   `authenticateApiKey` middleware (the browser relies on the dashboard session/api key;
   `x-requested-by` marks dashboard traffic). It resolves the merchant context via
   `getPayWay(req)`, generates a transaction id with `payway.generateTranId('PW')`, then
   calls `payway.generateQr({...})`.
4. **`generateQr` (src/services/payway.js)** decides the mode in this order:
   - **(0) direct `aba_data`** (only used by `/create-tran`, not this form) via
     `generateFromAbaData`.
   - **(1) live PayWay link** when a `link.payway.com.kh/...` link is present, via
     `generateFromPayWayLink`.
   - **(2) merchant gateway API** (`POST {baseUrl}/generate-qr`) only when
     `PAYWAY_PREFER_LOCAL_KHQR !== 'false'` is false.
   - **(3) local EMVCo fallback** via the pure function `generateBakongKhqr`.
   In every mode it finally builds a PNG data URL with the `qrcode` npm package
   (`QRCode.toDataURL(qrString, { margin: 1, width: 340 })`) unless the mode already
   produced one.
5. **Response.** The route returns `{ success: true, data: { tran_id, amount, currency,
   qr_string, qr_code (data URL), deeplink, payment_link, merchant_name, expire_in,
   expires_at } }`.
6. **Render.** The browser sets `#dynKhqrImg` to `item.qr_code || item.qr_image`, fills
   the amount/merchant/routing/expiry displays, sets the ABA deeplink button
   `#dynKhqrAbaDeeplinkBtn` to `item.deeplink`, and puts `item.qr_string` in
   `#dynKhqrStringPre` (copy button copies the raw KHQR string).

A second form on the same page (`#linkToQrForm`) posts to the same
`/api/v1/payway/generate-khqr` endpoint with slightly different fields.

## 2. Functions: inputs, outputs, endpoints, headers, hash formulas

### `crc16Ccitt(data)` (payway.js)
- **Input:** any string. **Output:** 4-character uppercase hex CRC string.
- **Formula:** CRC-16/CCITT-FALSE. `crc = 0xFFFF`; for each character
  `crc ^= (charCodeAt(i) << 8)`, then 8 times: if `crc & 0x8000` then
  `crc = ((crc << 1) ^ 0x1021) & 0xFFFF` else `crc = (crc << 1) & 0xFFFF`.
  Finally `crc.toString(16).toUpperCase().padStart(4,'0')`.
- **Note:** it iterates UTF-16 code units, not UTF-8 bytes.

### `formatTlv(tag, value)` (payway.js)
- **Input:** a 2-digit tag and any value. **Output:** `tag + length + value`.
- **Length:** `Buffer.byteLength(value, 'utf8')` padded to 2 digits (UTF-8 **byte**
  length, not character count).

### `generateBakongKhqr({...})` (payway.js, pure)
- **Inputs:** `amount`, `currency` (default `USD`), `tran_id`,
  `merchantName`, `bakongAccountId`, `abaAccountNumber`, `bankName`,
  `paymentLink`, `expiresInMinutes` (default 15), `expiresInSeconds`.
- **Output:** a complete EMVCo KHQR string (TLV).
- **Structure:**
  - Tag `00` = `01`; tag `01` = `12` (dynamic QR).
  - Tag `30` with sub `00` Bakong account id, sub `01` ABA account number,
    sub `02` bank name.
  - Tag `52` = `5399` (MCC); tag `53` = `840` for USD else `116` for KHR.
  - Tag `54` amount: USD -> `String(parseFloat(amount))` with any trailing `.00`
    removed; KHR -> `String(Math.round(parseFloat(amount)))`.
  - Tag `58` = `KH`; tag `59` = merchant name truncated to 25 chars; tag `60` = `N/A`.
  - Tag `62`: if a link id is derived from the PayWay link, a routing block is
    embedded under sub `68` (sub-sub `00` a fixed routing namespace, `01`
    `linkId-<last 8 digits of epoch ms>`, `02` a fixed store id, `03` `1`, `05` `1`).
    Otherwise sub `01` = `tran_id` (or `Date.now()`).
  - Tag `99`: sub `00` = creation epoch **ms**, sub `01` = expiry epoch **ms**
    (`now + durationMs`), sub `67` containing sub `00` = an MD5-derived token hex
    (see below), sub `68` = `PLIK`.
  - Tag `63`: `6304` + CRC. The CRC is computed over `payload + "6304"`.
- **MD5 token formula:** `MD5(now + tran_id + amount).hex.substring(0,17).toUpperCase()`.
- **Link id derivation:** strip a leading `https?://link.payway.com.kh/`, strip a
  leading `ABAPAY`, then take the first `\d{6}` match, else the first 10 chars.

### `generateFromPayWayLink({ paymentLink, amount, currency })` (payway.js, async)
- **Inputs:** PayWay link URL, amount, currency. **Output:** object
  `{ qrString, qrImage, token, tran_id, expireIn, merchantName, raw }` or `null`.
- **Endpoint 1 (page fetch):** `GET <paymentLink>` with headers user-agent and
  accept text/html; 6 s timeout; follows Ajax-style redirects by default.
- **Page parsing:** matches `window.__NUXT__ = (...);</script>` and evaluates the
  captured expression with `new Function(...)`; reads
  `nuxtData.data[0].data.aba_data` and `nuxtData.state.request_time`.
- **Endpoint 2:** `POST https://pwapp.ababank.com/api/pw-app/v1/payment/gateway/list-payment-options`
  headers `Content-Type: application/json`, `language: en`.
  Body: `{ request_time, aba_data, additional_fields, hash }` where
  `additional_fields = JSON.stringify({ amount: formattedAmount })` (amount is a
  **string**, USD `toFixed(2)`, KHR integer) and
  `hash = SHA512(request_time + aba_data + additional_fields).hex`.
- **Endpoint 3:** `POST https://pwapp.ababank.com/api/pw-app/v1/payment/gateway/abapay-khqr/request-qr`
  headers `Content-Type: application/json`, `token: <token from endpoint 2>`.
  Body: `{ request_time, aba_data: <new aba_data>, hash }` where
  `hash = HMAC-SHA512(key = token, message = request_time + new aba_data).hex`.

### `generateFromAbaData({ abaData, amount, currency })` (payway.js, async)
- Same two POSTs as above, but `request_time` is generated locally as UTC
  `yyyyMMddHHmmss` (`getUTCFullYear + pad(getUTCMonth+1) + ...`) instead of being
  read from the page. Amount formatting identical to `generateFromPayWayLink`.

### `getDeepLinks(khqrString)` (payway.js)
- **Output:** `{ abaMobileScheme, webLink }` where
  `abaMobileScheme = "abamobilebank://ababank.com?type=payway&qrcode=" + encodeURIComponent(khqrString)`
  and `webLink` is the configured payment link.

### `checkTransaction(tran_id, { client_id, token })` (payway.js, async)
- **Live path (when both `client_id` and `token` are present):**
  `POST https://pwapp.ababank.com/api/pw-app/v1/payment-link/check-payment-status`
  headers `Content-Type: application/json`, `language: en`, `token`.
  Body: `{ request_time (UTC yyyyMMddHHmmss), client_id, device_id (random 10 hex
  chars), hash }` where `hash = SHA512(client_id + device_id + request_time).hex`.
  **Paid** when `response.data.action === "approved"`.
- **Fallback path:** `POST {baseUrl}/check-transaction-2` with
  `{ req_time, merchant_id, tran_id, hash }` where
  `hash = HMAC-SHA512(key = apiKey, message = req_time + merchant_id + tran_id)`.
  Paid when `status.code === '00'` (or `status === '00'` / `0`).

### `generateTranId(prefix = 'D')` (payway.js)
- **Output:** `prefix + floor(Date.now()/1000) + 4 random base36 uppercase chars`.
  The KHQR routes call it with prefix `PW`.

### `extractAbaDataFromLink(link)` (api.js, async)
- Validates the link is `https` and host is exactly `link.payway.com.kh`, then does a
  manual redirect loop (max 5) enforcing the same host each hop.
- Tries `window.__NUXT__` parsing with `new Function`, then a list of regex patterns
  for `aba_data`, requiring a base64-ish value of length >= 200.
- Falls back to a Playwright headless browser that blocks non-PayWay hosts and scans
  captured response bodies, then caches the result in memory.
- Returns `{ abaData, method }` or `null`.

### `POST /payway/generate-khqr` (api.js)
- **Headers:** `authenticateApiKey` middleware plus the dashboard `x-requested-by`.
- Generates `tran_id = generateTranId('PW')`, calls `generateQr`, and returns the
  `success/data` envelope described in section 1.

### `POST /create-tran` (api.js)
- Body may include `amount`, `currency`, `order_id`, `items`, `continue_url`,
  `webhook`, `cancel_url`, `aba_data`, `client_id`, `payment_link`/`link`/`payway_link`/`url`,
  `expires_in_minutes`, `expires_in_seconds`.
- Resolves a link, optionally calls `extractAbaDataFromLink`, calls `generateQr` with
  `abaData`, stores records in SQLite (`payment_links`, `payway_transactions`), and
  returns `tran_id`, `client_id`, `qr_string`, `qr_code`, `abapay_deeplink`,
  `checkout_url`, `expire_in`, `expires_at`.

### `POST /check-payment-status` (api.js)
- Body: `tran_id` plus optional `client_id` and `local_order_id` (and `token`).
- Looks up local DB records, then calls `payway.checkTransaction(tran_id,
  { client_id, token })`. Marks paid records, triggers the merchant webhook, and
  answers `status: "approved"` or `"pending"`/`"expired"`.

## 3. Configuration names it needs (names only)

From `.env.example` / the service constructor (never read actual `.env`):

- Server/API: `PORT`, `NODE_ENV`, `APP_URL`, `API_URL`, `ADMIN_API_KEY`.
- PayWay merchant: `PAYWAY_MERCHANT_ID`, `PAYWAY_API_KEY`, `PAYWAY_PUBLIC_KEY`,
  `PAYWAY_BASE_URL`.
- Link/merchant display: `PAYMENT_LINK`, `PAYWAY_MERCHANT_NAME`.
- ABA accounts: `ABA_ACCOUNT_NUMBER_USD`, `ABA_ACCOUNT_NUMBER_KHR`,
  legacy `ABA_ACCOUNT_NUMBER`.
- Bakong: `BAKONG_ACCOUNT_ID`.
- Mode switch: `PAYWAY_PREFER_LOCAL_KHQR`.
- Internal (not from `.env`): `webhookSecret`, `sandboxMode`, `internalMerchantId`.

## 4. Fragile or risky parts (do not copy the values)

- **Undocumented endpoints.** `pwapp.ababank.com` paths
  `list-payment-options`, `abapay-khqr/request-qr` and
  `payment-link/check-payment-status` are reverse-engineered and not part of the
  public PayWay API contract. They can change or disappear at any time.
- **Remote code execution risk.** Both `generateFromPayWayLink` and
  `extractAbaDataFromLink` parse the page's `window.__NUXT__` with
  `new Function(...)`, which executes JavaScript returned by a remote server. That is
  arbitrary code execution in the Node process; the C# port never evaluates script
  and instead reads the state with regex/JSON only.
- **Third-party QR leak.** The fallback builds the QR image with
  `https://api.qrserver.com/v1/create-qr-code/?...&data=<KHQR>`, sending the full
  payment payload (amount, account, tokenized data) to a third-party service. The C#
  port renders QR PNGs locally with QRCoder and never contacts such a service.
- **Hardcoded/default values in the source** that must **never** be copied into this
  repository: a default merchant name placeholder, a default Bakong account id /
  ABA account number placeholder, sample/test API and public keys, a default store id
  in the routing block, a webhook secret, and default customer email/phone. **None of
  those hardcoded values may be copied.** All merchant-specific values must come from
  configuration/user-secrets.
- **MD5 token** in tag 99 is not a cryptographic signature and offers no real
  security; it is kept only for byte-for-byte compatibility with the reference.
- **CRC over UTF-16 code units** means non-ASCII merchant names produce a CRC that
  does not match a UTF-8-byte CRC implementation; kept intentionally for parity.

## 5. Payment status check (client_id + token) and the "approved" answer

The live status check uses the **`client_id`** and **`token`** obtained from the
request-qr response, stored alongside the transaction. The client POSTs to
`https://pwapp.ababank.com/api/pw-app/v1/payment-link/check-payment-status` with
headers `Content-Type: application/json`, `language: en` and
`token: <token>`, and body `{ request_time, client_id, device_id, hash }` where
`request_time` is UTC `yyyyMMddHHmmss`, `device_id` is 10 random hex chars, and
`hash = SHA512(client_id + device_id + request_time).hex`.

The payment is **approved** when the JSON body has `data.action === "approved"`.
The route normalises this into `status: "approved"` / `data.action: "approved"`,
otherwise `"pending"`, or `"expired"` when the local `expires_at` has passed.
Tokens must never be logged.

## 6. C# port configuration, user-secrets and Docker names

The port binds the following non-secret defaults in `appsettings.json` under `Aba`
(empty strings mean "not configured"):

- `Aba:PaymentLink` (default `""`)
- `Aba:LinkAbaData` (default `""`)
- `Aba:MerchantName` (default `""`)
- `Aba:BakongAccountId` (default `""`)
- `Aba:AbaAccountNumberUsd` (default `""`)
- `Aba:AbaAccountNumberKhr` (default `""`)
- `Aba:Currency` (default `"USD"`)
- `Aba:QrLifetimeMinutes` (default `10`)

Set each value with user-secrets (replace the placeholder with your real value):

```
dotnet user-secrets set "Aba:PaymentLink" "https://link.payway.com.kh/REPLACE"
dotnet user-secrets set "Aba:LinkAbaData" "REPLACE"
dotnet user-secrets set "Aba:MerchantName" "REPLACE"
dotnet user-secrets set "Aba:BakongAccountId" "REPLACE@abaa"
dotnet user-secrets set "Aba:AbaAccountNumberUsd" "REPLACE"
dotnet user-secrets set "Aba:AbaAccountNumberKhr" "REPLACE"
dotnet user-secrets set "Aba:Currency" "USD"
dotnet user-secrets set "Aba:QrLifetimeMinutes" "10"
```

Equivalent Docker / environment variable names (double underscore for nesting):

- `Aba__PaymentLink`
- `Aba__LinkAbaData`
- `Aba__MerchantName`
- `Aba__BakongAccountId`
- `Aba__AbaAccountNumberUsd`
- `Aba__AbaAccountNumberKhr`
- `Aba__Currency`
- `Aba__QrLifetimeMinutes`
