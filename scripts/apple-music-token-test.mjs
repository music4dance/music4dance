// Smoke test for an Apple Music API developer token.
// Usage: node scripts/apple-music-token-test.mjs <path/to/AuthKey_KEYID.p8> <TEAM_ID> [songId,songId,...]
// Key ID is taken from the AuthKey_<KEYID>.p8 filename (override with APPLE_KEY_ID).
// No dependencies: uses Node's built-in crypto and fetch.
import { createPrivateKey, sign } from "node:crypto";
import { readFileSync } from "node:fs";
import { basename } from "node:path";

const [keyPath, teamId, idArg] = process.argv.slice(2);
if (!keyPath || !teamId) {
  console.error("Usage: node scripts/apple-music-token-test.mjs <AuthKey_KEYID.p8> <TEAM_ID> [ids]");
  process.exit(1);
}
const keyId = process.env.APPLE_KEY_ID ?? basename(keyPath).match(/AuthKey_(\w+)\.p8/)?.[1];
if (!keyId) {
  console.error("Could not read the Key ID from the filename; set APPLE_KEY_ID.");
  process.exit(1);
}
const ids = idArg ?? "1017804211,1068336005,1072024229,1231281699,1351343163";

const b64url = (buf) => Buffer.from(buf).toString("base64url");
const now = Math.floor(Date.now() / 1000);
const header = { alg: "ES256", kid: keyId };
const claims = { iss: teamId, iat: now, exp: now + 60 * 60 }; // 1 hour is plenty for a test
const signingInput = `${b64url(JSON.stringify(header))}.${b64url(JSON.stringify(claims))}`;
const key = createPrivateKey(readFileSync(keyPath));
// JWT ES256 wants the raw r||s signature, not DER.
const signature = sign("sha256", Buffer.from(signingInput), { key, dsaEncoding: "ieee-p1363" });
const token = `${signingInput}.${b64url(signature)}`;

console.log(`Key ID ${keyId}, Team ID ${teamId}`);

async function call(label, url) {
  const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
  const text = await res.text();
  console.log(`\n== ${label}: HTTP ${res.status}`);
  if (!res.ok) {
    console.log(text.slice(0, 500));
    return null;
  }
  return JSON.parse(text);
}

const requested = ids.split(",");
const songs = await call(
  `lookup ${requested.length} ids`,
  `https://api.music.apple.com/v1/catalog/us/songs?ids=${ids}`,
);
if (!songs) {
  console.log("\n401 = bad Team ID / Key ID / key, or the key lacks MusicKit. 403 = membership issue.");
  process.exit(1);
}
const found = new Set(songs.data.map((s) => s.id));
for (const s of songs.data) {
  const a = s.attributes;
  console.log(`${s.id}  isrc=${a.isrc ?? "-"}  ${a.artistName} — ${a.name} [${a.albumName}]`);
}
const missing = requested.filter((id) => !found.has(id));
if (missing.length) console.log(`not found in us storefront: ${missing.join(", ")}`);

// Reverse lookup: ISRC -> catalog songs (stage 2 of the plan).
const isrc = songs.data.find((s) => s.attributes.isrc)?.attributes.isrc;
if (isrc) {
  const rev = await call(
    `reverse lookup isrc ${isrc}`,
    `https://api.music.apple.com/v1/catalog/us/songs?filter%5Bisrc%5D=${isrc}`,
  );
  for (const s of rev?.data ?? []) {
    console.log(`${s.id}  ${s.attributes.artistName} — ${s.attributes.name} [${s.attributes.albumName}]`);
  }
}
