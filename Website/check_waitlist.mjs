// Isolated Node tests with real in-memory SQLite; never contacts Cloudflare or sends mail.
import assert from 'node:assert/strict';
import { DatabaseSync } from 'node:sqlite';
import { LaunchWaitlist, routeWaitlist } from './waitlist.js';
import worker from './worker.js';

function fixture() {
  const db = new DatabaseSync(':memory:'); let alarm = null;
  const storage = {
    sql: { exec(query, ...args) { const rows = db.prepare(query).all(...args); return { toArray: () => rows }; } },
    transactionSync(callback) {
      db.exec('BEGIN');
      try { const value = callback(); db.exec('COMMIT'); return value; }
      catch (error) { db.exec('ROLLBACK'); throw error; }
    },
    getAlarm: async () => alarm,
    setAlarm: async value => { alarm = value; }
  };
  const object = new LaunchWaitlist({ storage });
  const env = { WAITLIST_ADMIN_TOKEN: 'only-a-fixture-secret-do-not-use-live', LAUNCH_LIST: {
    idFromName: name => name,
    get: () => ({ fetch: (url, init) => object.fetch(new Request(url, init)) })
  } };
  const request = (body, options = {}) => new Request('https://wondigames.com' + (options.path || '/api/launch-list'), {
    method: options.method || 'POST',
    headers: { Origin: options.origin || 'https://wondigames.com', 'Content-Type': 'application/json', 'CF-Connecting-IP': options.ip || '192.0.2.1', ...(options.headers || {}) },
    ...(options.method === 'GET' ? {} : { body: typeof body === 'string' ? body : JSON.stringify(body) })
  });
  const signup = (email, options) => routeWaitlist(request({ email, consent: true, language: 'tr', ...options?.data }, options), env);
  return { db, object, env, request, signup, alarm: () => alarm };
}

let checks = 0;
async function check(name, test) { await test(); checks++; console.log('PASS ' + name); }
await check('same-origin registration, consent metadata and private storage', async () => {
  const f = fixture(); assert.equal((await f.signup(' HELLO@Example.com ')).status, 200);
  const [row] = f.db.prepare('SELECT * FROM subscriptions').all();
  assert.equal(row.email, 'hello@example.com'); assert.equal(row.language, 'tr');
  assert.equal(row.consent_version, 'launch-once-v1'); assert.equal(row.expires_at-row.created_at, 180*86400000);
  assert(f.alarm() > Date.now());
});
await check('no consent is rejected', async () => {
  const f = fixture(); assert.equal((await f.signup('a@example.com', { data: { consent: false } })).status, 400);
  assert.equal(f.db.prepare('SELECT COUNT(*) n FROM subscriptions').get().n, 0);
});
await check('cross-origin and missing-origin requests are rejected', async () => {
  const f = fixture(); assert.equal((await f.signup('a@example.com', { origin: 'https://other.example' })).status, 403);
  const r = f.request({ email: 'a@example.com', consent: true }); r.headers.delete('Origin');
  assert.equal((await routeWaitlist(r, f.env)).status, 403);
});
await check('invalid address, oversized body and wrong content type are rejected', async () => {
  const f = fixture(); assert.equal((await f.signup('broken')).status, 400);
  assert.equal((await routeWaitlist(f.request('x'.repeat(5000)), f.env)).status, 400);
  assert.equal((await f.signup('a@example.com', { headers: { 'Content-Type': 'text/plain' } })).status, 415);
});
await check('honeypot does not save an address', async () => {
  const f = fixture(); assert.equal((await f.signup('a@example.com', { data: { website: 'spam' } })).status, 200);
  assert.equal(f.db.prepare('SELECT COUNT(*) n FROM subscriptions').get().n, 0);
});
await check('repeat registration is idempotent', async () => {
  const f = fixture(); await f.signup('a@example.com'); await f.signup('A@example.com');
  assert.equal(f.db.prepare('SELECT COUNT(*) n FROM subscriptions').get().n, 1);
  assert.equal(f.object.count('total'), 1);
});
await check('per-network rate limit and no raw IP persisted', async () => {
  const f = fixture(); for (let i=0;i<5;i++) assert.equal((await f.signup(`a${i}@example.com`)).status, 200);
  assert.equal((await f.signup('last@example.com')).status, 429);
  const [row] = f.db.prepare('SELECT * FROM rate_limits').all(); assert.equal(row.bucket.length, 64);
  assert(!row.bucket.includes('192.0.2.1'));
});
await check('daily capacity enforced atomically', async () => {
  const f = fixture(); for (let i=0;i<100;i++) assert.equal((await f.signup(`a${i}@example.com`, { ip: `192.0.2.${i}` })).status, 200);
  assert.equal((await f.signup('last@example.com', { ip: '198.51.100.1' })).status, 503);
  assert.equal(f.object.count('total'), 100);
});
await check('overall capacity and daily attempt cap', async () => {
  const f = fixture(); f.db.prepare("INSERT INTO counters VALUES ('total',5000)").run();
  assert.equal((await f.signup('a@example.com')).status, 503);
  f.db.prepare("UPDATE counters SET value=0 WHERE key='total'").run();
  f.db.prepare('INSERT OR REPLACE INTO counters VALUES (?,1000)').run('attempts:day:'+Math.floor(Date.now()/86400000));
  assert.equal((await f.signup('a@example.com')).status, 503);
});
await check('exports are private; unauthorized removal is blocked', async () => {
  const f = fixture(); await f.signup('a@example.com');
  for (const path of ['/api/launch-list/export','/api/launch-list/remove']) {
    assert.equal((await routeWaitlist(f.request({ email:'a@example.com' }, { path }), f.env)).status, 401);
  }
  const response = await routeWaitlist(f.request(null, { path:'/api/launch-list/export', method:'GET', headers:{ Authorization:'Bearer '+f.env.WAITLIST_ADMIN_TOKEN } }), f.env);
  assert.equal((await response.json()).entries[0].email, 'a@example.com');
});
await check('authorized removal and expiry cleanup', async () => {
  const f = fixture(); await f.signup('a@example.com');
  const response = await routeWaitlist(f.request({ email:'a@example.com' }, { path:'/api/launch-list/remove', headers:{ Authorization:'Bearer '+f.env.WAITLIST_ADMIN_TOKEN } }), f.env);
  assert.equal(response.status,200); assert.equal(f.object.count('total'),0);
  await f.signup('b@example.com');
  f.db.prepare('UPDATE subscriptions SET expires_at=?').run(Date.now()-1);
  const today='day:'+Math.floor(Date.now()/86400000); const before=f.object.count(today);
  await f.object.alarm(); assert.equal(f.object.count('total'),0); assert.equal(f.object.count(today),before);
});
await check('missing bindings fail closed', async () => {
  const f = fixture(); assert.equal((await routeWaitlist(f.request({ email:'a@example.com',consent:true }), {})).status,503);
});
await check('existing video byte-range behavior stays intact', async () => {
  const env={ ASSETS:{ fetch:async()=>new Response('abcdefghij') } };
  const result=await worker.fetch(new Request('https://wondigames.com/img/clips/a.mp4',{headers:{Range:'bytes=2-5'}}),env);
  assert.equal(result.status,206);assert.equal(await result.text(),'cdef');
  assert.equal(result.headers.get('Content-Range'),'bytes 2-5/10');
});
console.log(`${checks} waitlist checks passed. No network calls or emails.`);
