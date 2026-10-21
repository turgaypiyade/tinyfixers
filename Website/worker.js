// Statik dosyalar Cloudflare assets'ten doğrudan sunulur; bu Worker yalnız video yollarında çalışır
// (wrangler.jsonc → assets.run_worker_first). Assets byte-range desteklemiyor, iPhone Safari ise MP4'ü
// ancak 206 Partial Content yanıtıyla oynatıyor → Range isteğini burada dilimleyip cevaplıyoruz.
export default {
  async fetch(request, env) {
    const range = request.headers.get("Range");
    const headers = new Headers(request.headers);
    headers.delete("Range");
    const asset = await env.ASSETS.fetch(new Request(request.url, { method: request.method, headers }));
    if (!asset.ok || !range) return withAcceptRanges(asset);

    const m = /^bytes=(\d*)-(\d*)$/.exec(range.trim());
    if (!m || (m[1] === "" && m[2] === "")) return withAcceptRanges(asset);   // çoklu/bozuk aralık → tam dosya

    const body = await asset.arrayBuffer();
    const size = body.byteLength;
    let start, end;
    if (m[1] === "") { start = Math.max(0, size - Number(m[2])); end = size - 1; }   // bytes=-N (son N bayt)
    else { start = Number(m[1]); end = m[2] === "" ? size - 1 : Math.min(Number(m[2]), size - 1); }
    if (start >= size || start > end) {
      return new Response(null, { status: 416, headers: { "Content-Range": `bytes */${size}`, "Accept-Ranges": "bytes" } });
    }

    const out = new Headers(asset.headers);
    out.set("Content-Range", `bytes ${start}-${end}/${size}`);
    out.set("Content-Length", String(end - start + 1));
    out.set("Accept-Ranges", "bytes");
    return new Response(request.method === "HEAD" ? null : body.slice(start, end + 1), { status: 206, headers: out });
  },
};

function withAcceptRanges(response) {
  const out = new Response(response.body, response);
  out.headers.set("Accept-Ranges", "bytes");
  return out;
}
