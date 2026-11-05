const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const program = fs.readFileSync(path.join(__dirname, '../PortalMercado.API/Program.cs'), 'utf8');
const match = program.match(/options\.UseRequestInterceptor\("""\s*([\s\S]*?)\s*"""\);/);
assert.ok(match, 'Falta el interceptor');
// El cargador Swashbuckle reconstruye una función ordinaria a partir de parámetros y cuerpo.
// Referencia: Swashbuckle.AspNetCore.SwaggerUI/index.js, parseFunction.
function load(source) {
  const start = source.indexOf('{');
  const declaration = source.slice(0, start);
  const parameters = declaration.slice(declaration.indexOf('(') + 1, declaration.lastIndexOf(')'));
  return new Function(parameters, source.slice(start + 1, source.lastIndexOf('}')));
}
assert.throws(() => load('async function(request) { await fetch("/api/auth/csrf"); return request; }'), SyntaxError);
const interceptor = load(match[1]);
(async function () {
  let calls = 0;
  global.fetch = async (url, options) => {
    calls++;
    assert.equal(url, '/api/auth/csrf');
    assert.equal(options.credentials, 'same-origin');
    return {ok: true, json: async () => ({requestToken: 'TOKEN_FICTICIO'})};
  };
  for (const method of ['GET', 'HEAD', 'OPTIONS']) {
    const request = {method};
    assert.equal(interceptor(request), request);
  }
  assert.equal(calls, 0);
  const post = {method: 'POST', headers: {'Content-Type': 'application/json'}};
  assert.equal(await interceptor(post), post);
  assert.equal(post.headers['X-CSRF-TOKEN'], 'TOKEN_FICTICIO');
  assert.equal(post.headers['Content-Type'], 'application/json');
  const withoutHeaders = {method: 'post'};
  await interceptor(withoutHeaders);
  assert.equal(withoutHeaders.headers['X-CSRF-TOKEN'], 'TOKEN_FICTICIO');
  global.fetch = async () => ({ok: false});
  await assert.rejects(interceptor({method: 'POST'}), /No se pudo preparar/);
  console.log('OK: carga compatible, GET sin fetch, POST con CSRF y fallo CSRF rechazado.');
})().catch(error => { console.error(error); process.exitCode = 1; });
