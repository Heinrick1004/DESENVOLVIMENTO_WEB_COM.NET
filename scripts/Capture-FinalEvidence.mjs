// Evidência da aplicação publicada: dados/erros vêm do servidor real.
// Loading atrasa a requisição real e error aborta uma consulta no navegador.
// Credenciais sintéticas permanecem em arquivo externo ignorado pelo Git.
import fs from 'node:fs/promises';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
function option(name, fallback) {
  const index = args.indexOf(name);
  if (index < 0) return fallback;
  if (!args[index + 1] || args[index + 1].startsWith('--')) throw new Error(`Valor ausente: ${name}`);
  return args[index + 1];
}
const base = new URL(option('--url', 'https://localhost:7240'));
if (base.protocol !== 'https:' || !['localhost', '127.0.0.1', '[::1]'].includes(base.hostname) || base.username || base.password) {
  throw new Error('Informe uma origem HTTPS local sem credenciais.');
}
const database = option('--database');
if (!/^PortalTarefas_Tests_[A-Za-z0-9_]+$/.test(database || '')) throw new Error('Identifique o banco exclusivo com --database PortalTarefas_Tests_....');
const credentials = JSON.parse(await fs.readFile(path.resolve(root, option('--credentials', '.local/auditoria-smoke-user.json')), 'utf8'));
const email = credentials.email ?? credentials.Email;
const password = credentials.password ?? credentials.senha ?? credentials.Password ?? credentials.Senha;
if (!email || !password) throw new Error('Arquivo externo de conta sintética incompleto; valores omitidos.');
if (!/^[^@\s]+@[^@\s]+\.(invalid|test)$/i.test(email)) {
  throw new Error('Utilize uma conta explicitamente sintética em domínio de teste (.invalid ou .test).');
}
const outputRoot = path.resolve(root, option('--output', 'docs/evidencias/capturas-finais'));
if (!outputRoot.startsWith(root + path.sep)) throw new Error('A saída deve permanecer dentro da solução.');
const output = path.join(outputRoot, new Date().toISOString().replace(/[:.]/g, '-'));
await fs.mkdir(output, { recursive: true });
const toolRoot = path.join(root, '.tools/ui-evidence/node_modules/playwright-core');
const { chromium } = await import(pathToFileURL(path.join(toolRoot, 'index.mjs')).href);
const toolVersion = JSON.parse(await fs.readFile(path.join(toolRoot, 'package.json'), 'utf8')).version;
let executablePath = option('--browser', process.env.PORTAL_EVIDENCE_BROWSER);
if (!executablePath) {
  for (const candidate of ['C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe']) {
    try { await fs.access(candidate); executablePath = candidate; break; } catch { /* próximo candidato */ }
  }
}
if (!executablePath) throw new Error('Informe --browser com um Chromium instalado.');
const browser = await chromium.launch({ executablePath, headless: true });
const manifest = {
  instanteUtc: new Date().toISOString(), origem: base.origin, banco: database,
  ambiente: option('--environment', 'Development'), navegador: browser.version(), playwright: toolVersion,
  commitBase: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(),
  alteracoesLocais: execFileSync('git', ['diff', '--name-only'], { cwd: root, encoding: 'utf8' }).trim().split(/\r?\n/).filter(Boolean),
  viewport: { width: 1440, height: 1100 },
  natureza: 'Aplicação real publicada com SQL Server exclusivo e conta sintética. Nenhum corpo HTTP, DOM ou estado React foi fabricado.',
  limites: 'TLS local ignorado exclusivamente no navegador de captura. Idle pausado via CDP; loading e erro usam controle de rede declarado. Imagens anteriores preservadas.',
  verificacoes: [], capturas: [],
};
let stage = 'iniciar';
let context;
let csrf;
async function record(method, endpoint, response, expected) {
  manifest.verificacoes.push({ metodo: method, caminho: endpoint, status: response.status(), esperado: expected });
  if (response.status() !== expected) throw new Error(`Resposta inesperada em ${method} ${endpoint}: ${response.status()}.`);
  return response;
}
async function save(page, filename, details = {}, region) {
  if (region) await region.screenshot({ path: path.join(output, filename), animations: 'disabled' });
  else await page.screenshot({ path: path.join(output, filename), fullPage: true, animations: 'disabled' });
  const content = await fs.readFile(path.join(output, filename));
  manifest.capturas.push({ arquivo: filename, caminho: new URL(page.url()).pathname + new URL(page.url()).search, sha256: createHash('sha256').update(content).digest('hex'), ...details });
  console.log(`Captura: ${filename}`);
}
async function visit(page, endpoint) {
  const response = await page.goto(endpoint, { waitUntil: 'networkidle' });
  await record('GET', endpoint, response, 200);
  await page.locator('h1').first().waitFor({ state: 'visible' });
}
try {
  context = await browser.newContext({ baseURL: base.origin, ignoreHTTPSErrors: true, viewport: manifest.viewport, locale: 'pt-BR', timezoneId: 'America/Sao_Paulo' });
  context.setDefaultTimeout(15000);
  const request = context.request;
  stage = 'validar saúde e versão';
  for (const endpoint of ['/health/live', '/health/ready']) await record('GET', endpoint, await request.get(endpoint), 200);
  const version = await record('GET', '/version', await request.get('/version'), 200);
  manifest.versaoServida = await version.json();
  stage = 'autenticar conta sintética';
  csrf = (await (await record('GET', '/api/auth/csrf', await request.get('/api/auth/csrf'), 200)).json()).token;
  await record('POST', '/api/auth/login', await request.post('/api/auth/login', { headers: { 'X-CSRF-TOKEN': csrf }, data: { email, senha: password } }), 200);
  csrf = (await (await record('GET', '/api/auth/csrf', await request.get('/api/auth/csrf'), 200)).json()).token;
  const before = await (await record('GET', '/api/tarefas?tamanho=50', await request.get('/api/tarefas?tamanho=50'), 200)).json();
  const selected = before.itens.find(item => item.publica) ?? before.itens[0];
  if (!selected) throw new Error('O seed de teste não disponibilizou tarefas.');
  const page = await context.newPage();
  stage = 'capturar MVC';
  await visit(page, '/Tarefas/Index');
  await save(page, 'r2-01-lista-mvc.png', { roteiro: 2, cenario: 'Lista real autenticada.' });
  await visit(page, `/Tarefas/Details/${selected.id}`);
  await save(page, 'r2-02-detalhes-mvc.png', { roteiro: 2, cenario: 'Detalhes reais da tarefa do seed.' });
  await visit(page, '/Tarefas/Create');
  await save(page, 'r2-03-cadastro-mvc.png', { roteiro: 2, cenario: 'Formulário de cadastro real.' });
  await page.locator('#Titulo').fill('x');
  await page.locator('#Descricao').fill('Entrada fictícia preservada na validação da entrega.');
  await page.locator('#Prioridade').selectOption('3');
  const [invalid] = await Promise.all([
    page.waitForResponse(response => new URL(response.url()).pathname === '/Tarefas/Create' && response.request().method() === 'POST'),
    page.getByRole('button', { name: 'Criar tarefa', exact: true }).click(),
  ]);
  await record('POST', '/Tarefas/Create', invalid, 200);
  await page.waitForLoadState('networkidle');
  const messages = await page.locator('.validation-summary-errors li').allTextContents();
  if (!messages.length || await page.locator('#Titulo').inputValue() !== 'x' || messages.some(message => /rowversion|field is required/i.test(message))) {
    throw new Error('Mensagens de validação MVC ou preservação de valores incorretas.');
  }
  await save(page, 'r2-04-validacao-mvc.png', { roteiro: 2, cenario: 'POST real inválido; valores preservados e nenhuma gravação.', mensagens: messages });
  stage = 'capturar OpenAPI';
  const swagger = await page.goto('/swagger/index.html', { waitUntil: 'networkidle' });
  await record('GET', '/swagger/index.html', swagger, 200);
  await page.locator('.swagger-ui .opblock').first().waitFor({ state: 'visible' });
  const schemasToggle = page.locator('.models-control');
  if (await schemasToggle.count() && await schemasToggle.getAttribute('aria-expanded') === 'true') await schemasToggle.click();
  await save(page, 'r4-01-swagger-operacoes.png', { roteiro: 4, cenario: 'Swagger UI servido em Development, operações reais.' });
  const first = page.locator('.opblock.opblock-get').filter({ has: page.locator('[data-path="/api/tarefas"]') }).first();
  const operation = await first.count() ? first : page.locator('.opblock').first();
  await operation.locator('.opblock-summary').click();
  await operation.locator('.parameters-container').waitFor({ state: 'visible' });
  await operation.locator('.responses-wrapper').waitFor({ state: 'visible' });
  await save(page, 'r4-02-swagger-contrato.png', { roteiro: 4, cenario: 'Captura do bloco real da operação expandida, após carregar parâmetros e respostas do contrato.', regiao: 'Bloco GET /api/tarefas da Swagger UI; captura direta do elemento, sem editar imagem.' }, operation);
  stage = 'capturar React success e empty';
  await page.goto('/app/tarefas', { waitUntil: 'networkidle' });
  await page.locator('[data-state="success"]').waitFor({ state: 'visible' });
  await save(page, 'r5-04-success.png', { roteiro: 5, estado: 'success', cenario: 'Dados fictícios reais do SQL de teste, sem interceptação.' });
  await page.locator('#busca').fill(`sem_resultados_entrega_${Date.now()}`);
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await page.locator('[data-state="empty"]').waitFor({ state: 'visible' });
  await save(page, 'r5-03-empty.png', { roteiro: 5, estado: 'empty', cenario: 'Filtro único sem resultados, resposta real do servidor.' });
  stage = 'capturar React loading';
  let releaseLoading;
  const loadingGate = new Promise(resolve => { releaseLoading = resolve; });
  const delayRoute = async route => { await loadingGate; await route.continue(); };
  await page.route('**/api/tarefas?*', delayRoute);
  try {
    await page.locator('#busca').fill('');
    await page.getByRole('button', { name: 'Buscar', exact: true }).click();
    await page.locator('[data-state="loading"]').waitFor({ state: 'visible' });
    await save(page, 'r5-02-loading.png', { roteiro: 5, estado: 'loading', cenario: 'Controle de rede: requisição real temporariamente retida antes de seguir ao servidor; nenhum corpo substituído.' });
  } finally { releaseLoading(); }
  await page.locator('[data-state="success"]').waitFor({ state: 'visible' });
  await page.unroute('**/api/tarefas?*', delayRoute);
  stage = 'capturar erro de rede e recuperação';
  const abortRoute = route => route.abort('connectionfailed');
  await page.route('**/api/tarefas?*', abortRoute);
  await page.locator('#busca').fill('captura_erro_controlado');
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await page.locator('[data-state="error"]').waitFor({ state: 'visible' });
  await save(page, 'r5-05-error-rede.png', { roteiro: 5, estado: 'error', cenario: 'Controle de rede: uma consulta GET abortada no navegador. Corpo de erro não fabricado.' });
  await page.unroute('**/api/tarefas?*', abortRoute);
  await page.getByRole('button', { name: 'Tentar novamente', exact: true }).click();
  await page.locator('[data-state="empty"]').waitFor({ state: 'visible' });
  manifest.verificacoes.push({ cenario: 'Recuperação após erro de rede controlado', resultado: 'aprovado', estadoFinal: 'empty' });
  stage = 'capturar Problem Details real';
  await page.goto('/app/tarefas/nova', { waitUntil: 'networkidle' });
  await page.locator('#titulo').fill('Validar a entrega acadêmica');
  await page.locator('#descricao').fill('Entrada fictícia da captura: prioridade alta sem prazo.');
  await page.locator('#prioridade').selectOption('3');
  const [problemResponse] = await Promise.all([
    page.waitForResponse(response => new URL(response.url()).pathname === '/api/tarefas' && response.request().method() === 'POST'),
    page.getByRole('button', { name: 'Criar tarefa', exact: true }).click(),
  ]);
  await record('POST', '/api/tarefas', problemResponse, 400);
  const problem = await problemResponse.json();
  if (!problem.errors || !/application\/problem\+json/.test(problemResponse.headers()['content-type'] || '')) throw new Error('O 400 não devolveu Validation Problem Details.');
  await page.locator('#prazo-error').waitFor({ state: 'visible' });
  if (await page.locator('#prazo').getAttribute('aria-invalid') !== 'true') throw new Error('O erro de prazo não foi associado ao campo.');
  await save(page, 'r5-06-problem-details.png', { roteiro: 5, estado: 'problem-details', cenario: 'POST real: prioridade alta sem prazo, HTTP 400 e mensagens do servidor.', resposta: { status: problem.status, title: problem.title, errors: problem.errors } });
  const after = await (await record('GET', '/api/tarefas?tamanho=50', await request.get('/api/tarefas?tamanho=50'), 200)).json();
  if (after.total !== before.total) throw new Error('A contagem de tarefas mudou; revisar concorrência antes de aceitar as capturas.');
  manifest.verificacoes.push({ cenario: 'Cadastros inválidos não gravaram tarefas', totalAntes: before.total, totalDepois: after.total, resultado: 'aprovado' });
  stage = 'capturar quadro transitório idle';
  const idle = await context.newPage();
  const cdp = await context.newCDPSession(idle);
  await cdp.send('Debugger.enable');
  let resolvePause;
  const paused = new Promise(resolve => { resolvePause = resolve; });
  cdp.on('Debugger.paused', resolvePause);
  await idle.addInitScript(() => {
    let observed = false;
    new MutationObserver(() => {
      if (!observed && document.querySelector('[data-state="idle"]')) { observed = true; debugger; }
    }).observe(document, { subtree: true, childList: true });
  });
  let pausedExecution = false;
  try {
    await idle.goto('/app/tarefas', { waitUntil: 'commit' });
    let timeout;
    await Promise.race([paused, new Promise((_, reject) => { timeout = setTimeout(() => reject(new Error('Quadro idle não observado antes do efeito React.')), 15000); })]).finally(() => clearTimeout(timeout));
    pausedExecution = true;
    const state = await cdp.send('Runtime.evaluate', { expression: "document.querySelector('[data-state]')?.dataset.state", returnByValue: true });
    if (state.result.value !== 'idle') throw new Error('Quadro pausado sem estado idle.');
    const shot = await cdp.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true, fromSurface: true });
    const image = Buffer.from(shot.data, 'base64');
    await fs.writeFile(path.join(output, 'r5-01-idle.png'), image);
    manifest.capturas.push({ arquivo: 'r5-01-idle.png', roteiro: 5, estado: 'idle', caminho: '/app/tarefas', sha256: createHash('sha256').update(image).digest('hex'), cenario: 'Quadro real pausado via CDP/MutationObserver antes do useEffect; DOM/estado React preservados.' });
    console.log('Captura: r5-01-idle.png');
  } finally {
    if (pausedExecution) await cdp.send('Debugger.resume').catch(() => {});
    await cdp.detach();
    await idle.close();
  }
  manifest.resultado = 'aprovado';
} catch (error) {
  manifest.resultado = 'incompleto';
  manifest.etapaInterrompida = stage;
  // Mensagens de transporte não são persistidas: podem incluir cabeçalhos.
  console.error(`Captura interrompida em: ${stage}. Valores privados omitidos.`);
  process.exitCode = 1;
} finally {
  if (context && csrf) {
    try { await record('POST', '/api/auth/logout', await context.request.post('/api/auth/logout', { headers: { 'X-CSRF-TOKEN': csrf } }), 204); }
    catch { manifest.logout = 'não confirmado'; process.exitCode = 1; }
  }
  await browser.close();
  await fs.writeFile(path.join(output, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n', 'utf8');
  const rows = manifest.capturas.map(item => `| ${item.roteiro} | [${item.arquivo}](${item.arquivo}) | ${item.cenario} |`).join('\n');
  await fs.writeFile(path.join(output, 'README.md'), `# Capturas finais reais\n\nResultado: **${manifest.resultado}**. UTC: ${manifest.instanteUtc}. Commit base: \`${manifest.commitBase}\`.\n\nVersão servida: \`${JSON.stringify(manifest.versaoServida)}\`. Alterações locais e hashes das imagens estão em [manifest.json](manifest.json).\n\n${manifest.natureza}\n\n${manifest.limites}\n\n| Roteiro | Imagem | Procedimento |\n|---|---|---|\n${rows}\n\nAs credenciais, cookies, tokens, connection strings e o arquivo de conta não são copiados. Os cadastros inválidos usam SQL de teste e não gravam tarefas.\n`, 'utf8');
  console.log(`Manifesto: ${path.relative(root, path.join(output, 'manifest.json'))}`);
}
