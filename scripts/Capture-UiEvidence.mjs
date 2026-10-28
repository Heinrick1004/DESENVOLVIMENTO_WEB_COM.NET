// Ferramenta de evidência separada das dependências do produto:
// npm install --prefix .tools/ui-evidence --ignore-scripts --save-exact playwright-core@1.64.1
// node scripts/Capture-UiEvidence.mjs --url https://localhost:7240/app/
// Captura o React publicado. Respostas HTTP são interceptadas para simulação
// didática; estas imagens não comprovam persistência ou autorização no servidor.
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
function argument(name, fallback) {
  const index = args.indexOf(name);
  if (index < 0) return fallback;
  if (!args[index + 1] || args[index + 1].startsWith('--')) throw new Error(`Valor ausente: ${name}`);
  return args[index + 1];
}
const base = new URL(argument('--url', 'https://localhost:7240/app/'));
if (!['localhost', '127.0.0.1', '[::1]'].includes(base.hostname) || !['http:', 'https:'].includes(base.protocol) || base.username || base.password) {
  throw new Error('Use uma URL local HTTP(S), sem credenciais.');
}
if (!base.pathname.endsWith('/')) base.pathname += '/';
const outputRoot = path.resolve(root, argument('--output', 'docs/evidencias/roteiro-05-ui'));
if (!outputRoot.startsWith(root + path.sep)) throw new Error('As evidências devem ficar dentro da solução.');
const runId = new Date().toISOString().replace(/[:.]/g, '-');
const output = path.join(outputRoot, runId);
await fs.mkdir(output, { recursive: true });

const modulePath = path.join(root, '.tools/ui-evidence/node_modules/playwright-core/index.mjs');
const { chromium } = await import(pathToFileURL(modulePath).href);
const packageInfo = JSON.parse(await fs.readFile(path.join(root, '.tools/ui-evidence/node_modules/playwright-core/package.json'), 'utf8'));
const installedBrowsers = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
];
let executablePath = argument('--browser', process.env.PORTAL_EVIDENCE_BROWSER);
if (!executablePath) {
  for (const candidate of installedBrowsers) {
    try { await fs.access(candidate); executablePath = candidate; break; } catch { /* Próximo navegador. */ }
  }
}
if (!executablePath) throw new Error('Informe --browser com o executável de um navegador Chromium instalado.');
const browser = await chromium.launch({ executablePath, headless: true });
const manifest = {
  instanteUtc: new Date().toISOString(),
  url: base.href,
  ambienteInformado: argument('--environment', 'não informado'),
  navegador: browser.version(),
  playwright: packageInfo.version,
  viewport: { width: 1440, height: 1100 },
  natureza: 'Capturas reais do React publicado com HTTP interceptado no navegador para simulação didática.',
  limites: 'Não comprova SQL Server, autenticação ou autorização reais. O certificado local é ignorado apenas neste contexto de captura.',
  capturas: [],
};

const emptyPage = { itens: [], pagina: 1, tamanho: 10, total: 0, totalPaginas: 0 };
const samplePage = {
  itens: [
    { id: 9001, titulo: 'Preparar relatório — simulação didática', descricao: 'Dados interceptados apenas para observar a interface.', prioridade: 3, prazo: '2026-11-05', situacao: 'EmAndamento', equipe: 'Equipe de demonstração', publica: true },
    { id: 9002, titulo: 'Revisar os testes — simulação didática', descricao: null, prioridade: 2, prazo: '2026-11-10', situacao: 'Pendente', equipe: 'Equipe de demonstração', publica: true },
    { id: 9003, titulo: 'Organizar referências — simulação didática', descricao: null, prioridade: 1, prazo: null, situacao: 'Concluida', equipe: 'Equipe de demonstração', publica: true },
  ],
  pagina: 1, tamanho: 10, total: 3, totalPaginas: 1,
};
const validationProblem = {
  type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
  title: 'Confira os campos informados.',
  status: 400,
  detail: 'Simulação didática de validação devolvida pela API.',
  errors: { Prazo: ['Informe o prazo para uma tarefa de prioridade alta.'] },
  correlationId: 'simulacao-ui-problem-details',
};

async function scenario(name, execute) {
  const context = await browser.newContext({ viewport: manifest.viewport, locale: 'pt-BR', timezoneId: 'America/Sao_Paulo', ignoreHTTPSErrors: true });
  const intercepted = [];
  let releaseLoading;
  const loadingGate = new Promise(resolve => { releaseLoading = resolve; });
  const page = await context.newPage();
  page.setDefaultTimeout(10000);
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await context.route('**/api/**', async route => {
    const request = route.request();
    const endpoint = new URL(request.url()).pathname;
    intercepted.push({ metodo: request.method(), caminho: endpoint });
    const json = (body, status = 200) => route.fulfill({ status, contentType: status >= 400 ? 'application/problem+json' : 'application/json', body: JSON.stringify(body) });
    if (endpoint === '/api/auth/me') {
      return name === 'problem-details' ? json({ id: 'simulacao-ui', email: 'demonstracao@exemplo.invalid', equipe: 'Equipe de demonstração', roles: ['Usuario'], podeEditarOutrasEquipes: false }) : json({ status: 401, title: 'Sem sessão — simulação didática.' }, 401);
    }
    if (endpoint === '/api/auth/csrf') return json({ token: 'simulacao-sem-valor-de-autenticacao' });
    if (endpoint === '/api/tarefas' && request.method() === 'GET') {
      if (name === 'error-network') return route.abort('connectionfailed');
      if (name === 'idle' || name === 'loading') await loadingGate;
      return json(name === 'success' ? samplePage : emptyPage);
    }
    if (endpoint === '/api/tarefas' && request.method() === 'POST' && name === 'problem-details') return json(validationProblem, 400);
    // Nenhuma operação de escrita pode alcançar o servidor neste roteiro visual.
    return json({ status: 501, title: 'Rota não prevista pela simulação de evidências.' }, 501);
  });
  try {
    await execute(page, context, intercepted);
    if (errors.length) throw new Error(`Erros JavaScript na página: ${errors.join('; ')}`);
  } catch (error) {
    manifest.capturas.push({ estado: name, resultado: 'bloqueado', motivo: error.message, interceptacoes: intercepted });
    console.error(`${name}: ${error.message}`);
  } finally {
    releaseLoading();
    await context.close();
  }
}

async function screenshot(page, name, filename, expectedState, intercepted, extra = {}) {
  if (expectedState) await page.locator(`[data-state="${expectedState}"]`).waitFor({ state: 'visible' });
  await page.screenshot({ path: path.join(output, filename), fullPage: true, animations: 'disabled' });
  manifest.capturas.push({ estado: name, resultado: 'capturado', arquivo: filename, estadoObservado: expectedState, interceptacoes: intercepted, ...extra });
  console.log(`${name}: ${filename}`);
}

try {
  await scenario('idle', async (page, context, intercepted) => {
    const cdp = await context.newCDPSession(page);
    await cdp.send('Debugger.enable');
    let resolvePause;
    const paused = new Promise(resolve => { resolvePause = resolve; });
    cdp.on('Debugger.paused', resolvePause);
    await page.addInitScript(() => {
      let observed = false;
      new MutationObserver(() => {
        if (!observed && document.querySelector('[data-state="idle"]')) {
          observed = true;
          // Preserva o quadro real entre o primeiro commit React e o useEffect.
          // Não altera componentes, estado, texto ou árvore DOM.
          debugger;
        }
      }).observe(document, { subtree: true, childList: true });
    });
    let pausedExecution = false;
    try {
      await page.goto(new URL('tarefas', base).href, { waitUntil: 'commit' });
      let timeout;
      await Promise.race([paused, new Promise((_, reject) => { timeout = setTimeout(() => reject(new Error('O quadro idle transitório não foi observado antes do useEffect.')), 10000); })]).finally(() => clearTimeout(timeout));
      pausedExecution = true;
      const observed = await cdp.send('Runtime.evaluate', { expression: "JSON.stringify({state:document.querySelector('[data-state]')?.dataset.state, height:document.body.getBoundingClientRect().height})", returnByValue: true });
      const state = JSON.parse(observed.result.value);
      if (state.state !== 'idle') throw new Error('A execução pausou sem preservar o estado idle.');
      const image = await cdp.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true, fromSurface: true });
      await fs.writeFile(path.join(output, '01-idle.png'), Buffer.from(image.data, 'base64'));
      manifest.capturas.push({ estado: 'idle', resultado: 'capturado', arquivo: '01-idle.png', estadoObservado: state.state, metodo: 'Quadro real do DOM pausado via CDP/MutationObserver antes do useEffect; nenhum DOM ou estado React foi alterado.', interceptacoes: intercepted });
      console.log('idle: 01-idle.png (quadro transitório pausado)');
    } finally {
      if (pausedExecution) await cdp.send('Debugger.resume').catch(() => {});
      await cdp.detach();
    }
  });
  for (const [name, filename, expectedState] of [
    ['loading', '02-loading.png', 'loading'],
    ['empty', '03-empty.png', 'empty'],
    ['success', '04-success.png', 'success'],
    ['error-network', '05-error-network.png', 'error'],
  ]) {
    await scenario(name, async (page, _context, intercepted) => {
      await page.goto(new URL('tarefas', base).href);
      await screenshot(page, name, filename, expectedState, intercepted);
    });
  }
  await scenario('problem-details', async (page, _context, intercepted) => {
    await page.goto(new URL('tarefas/nova', base).href);
    await page.locator('#titulo').fill('Preparar a entrega do roteiro');
    await page.locator('#descricao').fill('Simulação didática de uma resposta de validação da API.');
    await page.locator('#prioridade').selectOption('3');
    await page.getByRole('button', { name: 'Criar tarefa', exact: true }).click();
    await page.locator('#prazo-error').waitFor({ state: 'visible' });
    if (await page.locator('#prazo').getAttribute('aria-invalid') !== 'true') throw new Error('O erro do campo prazo não foi associado ao formulário.');
    await screenshot(page, 'problem-details', '06-problem-details.png', undefined, intercepted, { respostaSimulada: validationProblem, sessao: 'Simulada somente no navegador; nenhuma autenticação real foi utilizada.' });
  });
} finally {
  await browser.close();
  await fs.writeFile(path.join(output, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n', 'utf8');
  const rows = manifest.capturas.map(item => `| ${item.estado} | ${item.resultado} | ${item.arquivo ? `[${item.arquivo}](${item.arquivo})` : item.motivo} |`).join('\n');
  const readme = `# Evidências de interface — Roteiro 5\n\n${manifest.natureza}\n\n${manifest.limites}\n\nURL: ${base.href}\n\nAmbiente informado pelo operador: ${manifest.ambienteInformado}\n\nInstante UTC: ${manifest.instanteUtc}\n\nNavegador: ${manifest.navegador}; Playwright Core ${manifest.playwright}.\n\n| Estado | Resultado | Arquivo ou motivo |\n|---|---|---|\n${rows}\n\nOs dados de sucesso, a sessão do formulário, o atraso, a falha de rede e o Problem Details são simulações controladas por interceptação HTTP. O arquivo manifest.json registra o procedimento e as rotas interceptadas. O estado idle é um quadro transitório real capturado com a execução pausada antes do useEffect, quando observado; o DOM e o estado React não foram editados. Nenhuma operação de escrita chega ao servidor.\n\nPara repetir, instale Playwright Core 1.64.1 em .tools/ui-evidence e execute scripts/Capture-UiEvidence.mjs com Node.js e --url apontando para a SPA publicada.\n`;
  await fs.writeFile(path.join(output, 'README.md'), readme, 'utf8');
  console.log(`Manifesto: ${path.join(output, 'manifest.json')}`);
}
if (manifest.capturas.some(item => item.resultado !== 'capturado')) process.exitCode = 1;
