// Captura MVC real com conta sintética externa. Nunca imprime credenciais,
// cookies, tokens ou HTML completo. Requer Playwright Core local em .tools.
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const option = (name, fallback) => {
  const index = args.indexOf(name);
  if (index < 0) return fallback;
  if (!args[index + 1] || args[index + 1].startsWith('--')) throw new Error(`Valor ausente: ${name}`);
  return args[index + 1];
};
const base = new URL(option('--url', 'https://localhost:7240'));
if (base.protocol !== 'https:' || !['localhost', '127.0.0.1', '[::1]'].includes(base.hostname) || base.username || base.password) throw new Error('Use uma origem HTTPS local, sem credenciais.');
const credentialPath = path.resolve(root, option('--credentials', '.local/auditoria-smoke-user.json'));
const credentials = JSON.parse(await fs.readFile(credentialPath, 'utf8'));
const email = credentials.email ?? credentials.Email;
const password = credentials.password ?? credentials.senha ?? credentials.Password ?? credentials.Senha;
if (!email || !password) throw new Error('O arquivo externo precisa informar email e password. Valores omitidos.');
const output = path.join(root, 'docs/evidencias/roteiro-02-mvc');
await fs.mkdir(output, { recursive: true });
const { chromium } = await import(pathToFileURL(path.join(root, '.tools/ui-evidence/node_modules/playwright-core/index.mjs')).href);
const browser = await chromium.launch({ executablePath: option('--browser', 'C:/Program Files/Google/Chrome/Application/chrome.exe'), headless: true });
const manifest = {
  instanteUtc: new Date().toISOString(),
  origem: base.origin,
  ambienteInformado: option('--environment', 'Development'),
  navegador: browser.version(),
  natureza: 'Capturas MVC reais, com SQL Server e conta sintética controlada, sem interceptação HTTP e sem alteração de DOM.',
  limites: 'Banco exclusivo de auditoria. Certificado HTTPS local ignorado somente no contexto de captura. Credenciais externas não são copiadas para a evidência.',
  requisicoes: [],
  capturas: [],
};
let stage = 'iniciar contexto';
let activePage;
try {
  const database = (await fs.readFile(path.join(root, '.local/auditoria-test-database.txt'), 'utf8')).trim();
  if (/^PortalTarefas_Tests_[A-Za-z0-9_]+$/.test(database)) manifest.banco = database;
  const context = await browser.newContext({ baseURL: base.origin, ignoreHTTPSErrors: true, viewport: { width: 1440, height: 1100 }, locale: 'pt-BR', timezoneId: 'America/Sao_Paulo' });
  const request = context.request;
  async function record(method, endpoint, response) {
    manifest.requisicoes.push({ metodo: method, caminho: endpoint, status: response.status() });
    return response;
  }
  stage = 'obter proteção CSRF';
  let csrfResponse = await record('GET', '/api/auth/csrf', await request.get('/api/auth/csrf'));
  if (csrfResponse.status() !== 200) throw new Error('Proteção CSRF indisponível.');
  let csrf = (await csrfResponse.json()).token;
  stage = 'autenticar conta sintética';
  const login = await record('POST', '/api/auth/login', await request.post('/api/auth/login', { headers: { 'X-CSRF-TOKEN': csrf }, data: { email, senha: password } }));
  if (login.status() !== 200) throw new Error('A conta sintética não pôde autenticar.');
  csrfResponse = await record('GET', '/api/auth/csrf', await request.get('/api/auth/csrf'));
  csrf = (await csrfResponse.json()).token;

  stage = 'consultar tarefas públicas reais';
  const listResponse = await record('GET', '/api/tarefas?tamanho=50', await request.get('/api/tarefas?tamanho=50'));
  if (listResponse.status() !== 200) throw new Error('Listagem real indisponível.');
  const before = await listResponse.json();
  const selected = before.itens.find(item => item.publica && item.id === 3)
    ?? before.itens.find(item => item.publica);
  if (!selected) throw new Error('Nenhuma tarefa pública real disponível para os detalhes.');
  const page = await context.newPage();
  activePage = page;
  async function capture(endpoint, filename) {
    const response = await page.goto(endpoint, { waitUntil: 'networkidle' });
    if (response.status() !== 200 || !page.url().includes(endpoint.split('?')[0])) throw new Error('A página MVC real não respondeu conforme esperado.');
    await page.locator('h1').first().waitFor({ state: 'visible' });
    await page.screenshot({ path: path.join(output, filename), fullPage: true, animations: 'disabled' });
    manifest.capturas.push({ arquivo: filename, caminho: endpoint, status: response.status() });
  }
  stage = 'capturar listagem MVC';
  await capture('/Tarefas/Index', '01-lista.png');
  stage = 'capturar detalhes MVC';
  await capture(`/Tarefas/Details/${selected.id}`, '02-detalhes.png');

  stage = 'enviar formulário MVC inválido';
  const form = await page.goto('/Tarefas/Create', { waitUntil: 'networkidle' });
  if (form.status() !== 200 || !page.url().endsWith('/Tarefas/Create')) throw new Error('Formulário MVC autenticado indisponível.');
  await page.locator('#Titulo').fill('x');
  await page.locator('#Descricao').fill('Entrada preservada');
  await page.locator('#Prioridade').selectOption('3');
  stage = 'submeter formulário MVC inválido';
  const [invalid] = await Promise.all([
    page.waitForResponse(response => new URL(response.url()).pathname === '/Tarefas/Create' && response.request().method() === 'POST'),
    page.getByRole('button', { name: 'Criar tarefa', exact: true }).click(),
  ]);
  await record('POST', '/Tarefas/Create', invalid);
  await page.waitForLoadState('networkidle');
  stage = 'verificar mensagens e valores preservados do formulário';
  const titlePreserved = await page.locator('#Titulo').inputValue() === 'x';
  const descriptionPreserved = await page.locator('#Descricao').inputValue() === 'Entrada preservada';
  const messages = await page.locator('.validation-summary-errors li').allTextContents();
  if (invalid.status() !== 200 || !titlePreserved || !descriptionPreserved || messages.length === 0) {
    console.error(JSON.stringify({ status: invalid.status(), titlePreserved, descriptionPreserved, validationMessages: messages.length }));
    throw new Error('A validação MVC não preservou os valores e mensagens esperados.');
  }
  if (messages.some(message => /field is required|rowversion|versão da tarefa/i.test(message))) {
    throw new Error('A criação ainda exige a versão de concorrência; publique a correção antes de aceitar esta evidência.');
  }
  await page.screenshot({ path: path.join(output, '03-formulario-invalido.png'), fullPage: true, animations: 'disabled' });
  stage = 'confirmar que nenhuma tarefa foi criada';
  const afterResponse = await record('GET', '/api/tarefas?tamanho=50', await request.get('/api/tarefas?tamanho=50'));
  const after = await afterResponse.json();
  if (before.total !== after.total) throw new Error('A quantidade de tarefas mudou durante a captura; verificar concorrência antes de aceitar a evidência.');
  manifest.capturas.push({ arquivo: '03-formulario-invalido.png', caminho: '/Tarefas/Create', status: invalid.status(), tituloPreservado: titlePreserved, descricaoPreservada: descriptionPreserved, mensagens: messages, totalAntes: before.total, totalDepois: after.total });
  stage = 'encerrar sessão sintética';
  const logout = await record('POST', '/api/auth/logout', await request.post('/api/auth/logout', { headers: { 'X-CSRF-TOKEN': csrf } }));
  if (logout.status() !== 204) throw new Error('A sessão sintética não foi encerrada conforme esperado.');
  await fs.writeFile(path.join(output, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n', 'utf8');
  console.log(`Capturas MVC reais concluídas: ${manifest.capturas.length}. Manifesto: ${path.join(output, 'manifest.json')}`);
} catch {
  // Não propagar mensagens de transporte que possam conter cabeçalhos sensíveis.
  console.error(`Captura MVC interrompida na etapa: ${stage}. Credenciais e cabeçalhos omitidos.`);
  if (activePage) await activePage.screenshot({ path: path.join(root, '.local/mvc-capture-failure.png'), fullPage: true }).catch(() => {});
  process.exitCode = 1;
} finally { await browser.close(); }
