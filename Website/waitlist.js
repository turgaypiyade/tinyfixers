// Launch-only registrations. No email addresses, management keys or raw IPs are public.
const DAY = 86400000;
const RETENTION = 180 * DAY;
const MAX_BODY = 4096;
const EMAIL = /^[^\s@<>]{1,64}@[^\s@<>]+\.[^\s@<>]+$/;
const headers = { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' };
const reply = (body, status = 200) => new Response(JSON.stringify(body), { status, headers });

async function bodyJson(request) {
  const reader = request.body?.getReader();
  if (!reader) throw new Error('invalid');
  const chunks = []; let size = 0;
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    size += value.length;
    if (size > MAX_BODY) { await reader.cancel(); throw new Error('invalid'); }
    chunks.push(value);
  }
  const data = new Uint8Array(size); let offset = 0;
  for (const chunk of chunks) { data.set(chunk, offset); offset += chunk.length; }
  const result = JSON.parse(new TextDecoder().decode(data));
  if (!result || typeof result !== 'object' || Array.isArray(result)) throw new Error('invalid');
  return result;
}

async function authorized(request, env) {
  const expected = env.WAITLIST_ADMIN_TOKEN;
  const provided = request.headers.get('Authorization')?.replace(/^Bearer /, '');
  if (!expected || !provided || provided.length > 256) return false;
  const hash = async value => new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(value)));
  const [a, b] = await Promise.all([hash(expected), hash(provided)]);
  let different = 0;
  for (let i = 0; i < a.length; i++) different |= a[i] ^ b[i];
  return different === 0;
}

export async function routeWaitlist(request, env) {
  const url = new URL(request.url);
  if (!url.pathname.startsWith('/api/launch-list')) return null;
  if (!env.LAUNCH_LIST || !env.WAITLIST_ADMIN_TOKEN) return reply({ ok: false, error: 'unavailable' }, 503);
  if (url.pathname === '/api/launch-list' && request.method === 'GET') return reply({ ok: true });
  const admin = url.pathname === '/api/launch-list/export' || url.pathname === '/api/launch-list/remove';
  if (admin && !await authorized(request, env)) return reply({ ok: false }, 401);
  if (url.pathname === '/api/launch-list/export' && request.method === 'GET') {
    const object = env.LAUNCH_LIST.get(env.LAUNCH_LIST.idFromName('wonder-fixers-launch-v1'));
    return object.fetch('https://launch.internal/export');
  }
  if (request.method !== 'POST' || url.pathname === '/api/launch-list/export') return reply({ ok: false }, 405);
  if (!admin && (url.pathname !== '/api/launch-list' || request.headers.get('Origin') !== url.origin)) return reply({ ok: false }, 403);
  if (!request.headers.get('Content-Type')?.startsWith('application/json')) return reply({ ok: false, error: 'invalid' }, 415);
  let data;
  try { data = await bodyJson(request); } catch { return reply({ ok: false, error: 'invalid' }, 400); }
  if (!admin && data.website) return reply({ ok: true }); // bot trap; do not store anything
  const email = typeof data.email === 'string' ? data.email.trim().toLowerCase() : '';
  if (email.length > 254 || !EMAIL.test(email)) return reply({ ok: false, error: 'email' }, 400);
  const object = env.LAUNCH_LIST.get(env.LAUNCH_LIST.idFromName('wonder-fixers-launch-v1'));
  if (admin) return object.fetch('https://launch.internal/remove', { method: 'POST', body: JSON.stringify({ email }) });
  if (data.consent !== true) return reply({ ok: false, error: 'consent' }, 400);
  const now = Date.now();
  const key = await crypto.subtle.importKey('raw', new TextEncoder().encode(env.WAITLIST_ADMIN_TOKEN), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
  const ip = request.headers.get('CF-Connecting-IP') || 'unknown';
  const signature = await crypto.subtle.sign('HMAC', key, new TextEncoder().encode(Math.floor(now / 3600000) + ':' + ip));
  const visitor = Array.from(new Uint8Array(signature), n => n.toString(16).padStart(2, '0')).join('');
  try {
    return await object.fetch('https://launch.internal/subscribe', { method: 'POST', body: JSON.stringify({ email, language: data.language === 'tr' ? 'tr' : 'en', visitor }) });
  } catch { return reply({ ok: false, error: 'unavailable' }, 503); }
}

// SQLite-backed Durable Object: supported by the Workers Free plan. No paid-plan upgrade.
// A single object keeps deduplication and quotas atomic, without scans of the game or site.
export class LaunchWaitlist {
  constructor(ctx) {
    this.ctx = ctx;
    this.sql = ctx.storage.sql;
    this.sql.exec('CREATE TABLE IF NOT EXISTS subscriptions (email TEXT PRIMARY KEY, language TEXT NOT NULL, created_at INTEGER NOT NULL, expires_at INTEGER NOT NULL, consent_version TEXT NOT NULL)');
    this.sql.exec('CREATE INDEX IF NOT EXISTS subscription_expiry ON subscriptions(expires_at)');
    this.sql.exec('CREATE TABLE IF NOT EXISTS rate_limits (bucket TEXT PRIMARY KEY, attempts INTEGER NOT NULL, expires_at INTEGER NOT NULL)');
    this.sql.exec('CREATE INDEX IF NOT EXISTS rate_expiry ON rate_limits(expires_at)');
    this.sql.exec('CREATE TABLE IF NOT EXISTS counters (key TEXT PRIMARY KEY, value INTEGER NOT NULL)');
  }

  count(key) { return this.sql.exec('SELECT value FROM counters WHERE key = ?', key).toArray()[0]?.value || 0; }
  increment(key) { this.sql.exec('INSERT INTO counters(key,value) VALUES (?,1) ON CONFLICT(key) DO UPDATE SET value=value+1', key); }

  async fetch(request) {
    const action = new URL(request.url).pathname;
    if (action === '/export') return reply({ entries: this.sql.exec('SELECT email,language,created_at,consent_version FROM subscriptions ORDER BY created_at LIMIT 5000').toArray() });
    const data = await request.json();
    if (action === '/remove') {
      this.ctx.storage.transactionSync(() => {
        this.sql.exec('DELETE FROM subscriptions WHERE email = ?', data.email);
        this.sql.exec("INSERT OR REPLACE INTO counters(key,value) SELECT 'total',COUNT(*) FROM subscriptions");
      });
      return reply({ ok: true });
    }
    if (action !== '/subscribe') return reply({ ok: false }, 404);
    const now = Date.now();
    const day = 'day:' + Math.floor(now / DAY);
    const result = this.ctx.storage.transactionSync(() => {
      if (this.count('attempts:' + day) >= 1000) return reply({ ok: false, error: 'unavailable' }, 503);
      this.increment('attempts:' + day);
      const attempts = this.sql.exec('SELECT attempts FROM rate_limits WHERE bucket = ?', data.visitor).toArray()[0]?.attempts || 0;
      if (attempts >= 5) return reply({ ok: false, error: 'rate' }, 429);
      this.sql.exec('INSERT INTO rate_limits(bucket,attempts,expires_at) VALUES (?,1,?) ON CONFLICT(bucket) DO UPDATE SET attempts=attempts+1', data.visitor, now + 2 * 3600000);
      if (this.sql.exec('SELECT email FROM subscriptions WHERE email = ?', data.email).toArray().length) return reply({ ok: true });
      // Small explicit ceilings keep launch-list writes comfortably inside free allowances.
      if (this.count(day) >= 100 || this.count('total') >= 5000) return reply({ ok: false, error: 'unavailable' }, 503);
      this.sql.exec('INSERT INTO subscriptions(email,language,created_at,expires_at,consent_version) VALUES (?,?,?,?,?)', data.email, data.language, now, now + RETENTION, 'launch-once-v1');
      this.increment(day); this.increment('total');
      return reply({ ok: true });
    });
    if (await this.ctx.storage.getAlarm() === null) await this.ctx.storage.setAlarm(now + DAY);
    return result;
  }

  async alarm() {
    const now = Date.now();
    this.ctx.storage.transactionSync(() => {
      this.sql.exec('DELETE FROM subscriptions WHERE expires_at <= ?', now);
      this.sql.exec('DELETE FROM rate_limits WHERE expires_at <= ?', now);
      this.sql.exec("DELETE FROM counters WHERE (key LIKE 'day:%' OR key LIKE 'attempts:day:%') AND key NOT IN (?,?)", 'day:' + Math.floor(now / DAY), 'attempts:day:' + Math.floor(now / DAY));
      this.sql.exec("INSERT OR REPLACE INTO counters(key,value) SELECT 'total',COUNT(*) FROM subscriptions");
    });
    if (this.count('total') > 0) await this.ctx.storage.setAlarm(now + DAY);
  }
}
