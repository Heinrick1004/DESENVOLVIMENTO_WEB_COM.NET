// Verificação estática local. Não executa Docker nem o workflow GitHub Actions.
// Requer npm ci no frontend, Bash e PowerShell instalados.
import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { spawnSync } from 'node:child_process';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const { default: yaml } = await import(pathToFileURL(path.join(root, 'src/PortalTarefas.Frontend/node_modules/js-yaml/index.js')).href);
const workflowPath = path.join(root, '.github/workflows/ci.yml');
const source = await fs.readFile(workflowPath, 'utf8');
const workflow = yaml.load(source);
const steps = workflow.jobs.validar.steps;
const composeSource = await fs.readFile(path.join(root, 'compose.yaml'), 'utf8');
const compose = yaml.load(composeSource);
const dockerfile = await fs.readFile(path.join(root, 'Dockerfile'), 'utf8');
const bash = process.env.PORTAL_STATIC_BASH ?? (process.platform === 'win32' ? 'C:/Program Files/Git/bin/bash.exe' : 'bash');
const powershell = process.env.PORTAL_STATIC_POWERSHELL ?? (process.platform === 'win32' ? 'powershell.exe' : 'pwsh');
const bashResults = [];
for (const step of steps) {
  if (!step.run || step.shell === 'pwsh') continue;
  const result = spawnSync(bash, ['-n'], { input: step.run, encoding: 'utf8' });
  bashResults.push({ etapa: step.name, comando: 'bash -n < bloco run da etapa', exitCode: result.status, aprovado: !result.error && result.status === 0 });
}

const parser = "$tokens=$null; $parseErrors=$null; [System.Management.Automation.Language.Parser]::ParseFile($env:PORTAL_STATIC_PS_FILE,[ref]$tokens,[ref]$parseErrors) | Out-Null; if ($parseErrors.Count -gt 0) { exit 1 }; exit 0";
const powershellResults = [];
const powershellFiles = (await fs.readdir(path.join(root, 'scripts'))).filter(name => name.endsWith('.ps1')).sort();
for (const filename of powershellFiles) {
  const relative = `scripts/${filename}`;
  const result = spawnSync(powershell, ['-NoProfile', '-Command', parser], { encoding: 'utf8', env: { ...process.env, PORTAL_STATIC_PS_FILE: path.join(root, relative) } });
  const text = await fs.readFile(path.join(root, relative));
  powershellResults.push({ arquivo: relative, comando: 'PowerShell Parser.ParseFile (sem executar o script)', exitCode: result.status, aprovado: !result.error && result.status === 0, sha256: crypto.createHash('sha256').update(text).digest('hex') });
}
const powershellBlocks = [];
const inputParser = "[Console]::InputEncoding=[Text.UTF8Encoding]::new($false); $source=[Console]::In.ReadToEnd(); $tokens=$null; $parseErrors=$null; [System.Management.Automation.Language.Parser]::ParseInput($source,[ref]$tokens,[ref]$parseErrors) | Out-Null; if ($parseErrors.Count -gt 0) { exit 1 }; exit 0";
for (const step of steps) {
  if (!step.run || step.shell !== 'pwsh') continue;
  const result = spawnSync(powershell, ['-NoProfile', '-Command', inputParser], { input: step.run, encoding: 'utf8' });
  powershellBlocks.push({ etapa: step.name, comando: 'PowerShell Parser.ParseInput (sem executar o bloco run)', exitCode: result.status, aprovado: !result.error && result.status === 0, sha256: crypto.createHash('sha256').update(step.run).digest('hex') });
}
const readiness = steps.findIndex(step => step.run?.includes('SELECT 1'));
const integration = steps.findIndex(step => step.run?.includes('Test-Integration.ps1'));
const smoke = steps.findIndex(step => step.run?.includes('Smoke-Test.ps1'));
const nativeHealth = steps.findIndex(step => step.run?.includes('.State.Health.Status'));
const save = steps.findIndex(step => step.run?.includes('docker save'));
const upload = steps.findIndex(step => step.uses?.includes('upload-artifact') && step.if !== 'failure()');
const publicationSteps = steps.filter(step => step.run?.includes('dotnet publish') || step.run?.includes('docker build') || step.run?.includes('docker save') || (step.uses?.includes('upload-artifact') && step.if !== 'failure()'));
const gatePatterns = {
  restoreDotnet: /\bdotnet restore\b/,
  buildDotnet: /\bdotnet build\b/,
  integrationSql: /Test-Integration\.ps1.*-Initialize/,
  npmCi: /\bnpm(?:\.cmd)? ci\b/,
  npmAudit: /\bnpm(?:\.cmd)? audit --audit-level=high\b/,
  generatedContract: /Generate-Client\.ps1/,
  contractDiff: /git diff --exit-code.*docs\/openapi\.json.*schema\.d\.ts/,
  realClient: /\bnpm(?:\.cmd)? run verify:client\b/,
  frontendTests: /\bnpm(?:\.cmd)? test\b/,
  typecheck: /\bnpm(?:\.cmd)? run typecheck\b/,
  frontendBuild: /\bnpm(?:\.cmd)? run build\b/,
};
const gateIndexes = Object.fromEntries(Object.entries(gatePatterns).map(([name, pattern]) => [name, steps.findIndex(step => pattern.test(step.run ?? ''))]));
const firstPublication = steps.findIndex(step => publicationSteps.includes(step));
const requiredGates = Object.values(gateIndexes).filter(index => index >= 0).map(index => steps[index]);
const sdkStep = steps.find(step => step.uses?.startsWith('actions/setup-dotnet@'));
const sdk = JSON.parse(await fs.readFile(path.join(root, 'global.json'), 'utf8')).sdk.version;
const checks = {
  gatilhosPullRequestEBranchPrincipal: Boolean(workflow.on?.pull_request?.branches?.includes('main') && workflow.on?.push?.branches?.includes('main')),
  sdkExplicitoIgualGlobalJson: sdkStep?.with?.['dotnet-version'] === sdk,
  sqlProntoAntesDeMigrationsETestes: readiness >= 0 && integration > readiness,
  gatesObrigatoriosAntesDeEmpacotar: firstPublication >= 0 && Object.values(gateIndexes).every(index => index >= 0 && index < firstPublication),
  gatesObrigatoriosSemIgnorarOuPularFalhas: requiredGates.every(step => !step['continue-on-error'] && step.if === undefined) && !workflow.jobs.validar['continue-on-error'],
  smokeAntesDeSalvarImagemEPublicarArtefatos: smoke >= 0 && save > smoke && upload > save,
  healthCheckNativoAntesDeSalvarImagem: nativeHealth > smoke && save > nativeHealth && steps[nativeHealth].if === undefined && !steps[nativeHealth]['continue-on-error'],
  publicacaoSemAlwaysOuContinueOnError: publicationSteps.length >= 4 && publicationSteps.every(step => !step['continue-on-error'] && step.if === undefined),
  composeIsoladoConfiguravel: compose.name === '${PORTAL_COMPOSE_PROJECT:-portal-tarefas-tests}',
  composeMantemConexaoExternaObrigatoria: compose.services.web.environment.ConnectionStrings__PortalTarefas.startsWith('${PORTAL_SQL_CONNECTION:?'),
  composeSemSeedAutomatico: compose.services.web.environment.Seed__Enabled === 'false',
  imagemComUsuarioFinalNaoPrivilegiado: [...dockerfile.matchAll(/^USER\s+(.+)$/gm)].at(-1)?.[1].trim() === '$APP_UID',
  imagemDefineHealthCheck: /^HEALTHCHECK\s+/m.test(dockerfile),
};
const passed = bashResults.length > 0 && bashResults.every(result => result.aprovado) && powershellResults.length > 0 && powershellResults.every(result => result.aprovado) && powershellBlocks.length > 0 && powershellBlocks.every(result => result.aprovado) && Object.values(checks).every(Boolean);
const report = {
  instanteUtc: new Date().toISOString(),
  resultado: passed ? 'aprovado na verificação estática' : 'falha na verificação estática',
  comandoExecutado: 'node scripts/Validate-CiStatic.mjs',
  node: process.version,
  workflow: '.github/workflows/ci.yml',
  workflowSha256: crypto.createHash('sha256').update(source).digest('hex'),
  yaml: { parser: 'js-yaml', aprovado: true, etapas: steps.length },
  compose: { arquivo: 'compose.yaml', parser: 'js-yaml', aprovado: true, sha256: crypto.createHash('sha256').update(composeSource).digest('hex') },
  dockerfile: { arquivo: 'Dockerfile', sha256: crypto.createHash('sha256').update(dockerfile).digest('hex') },
  bash: { blocos: bashResults.length, resultados: bashResults },
  powershell: { scripts: powershellResults, blocosWorkflow: powershellBlocks },
  gatesObrigatorios: gateIndexes,
  verificacoesDeOrdemEBloqueio: checks,
  limitacoes: [
    'O parser YAML e bash -n validam sintaxe; não executam as etapas.',
    'O parser de compose.yaml nao resolve interpolacao nem substitui docker compose config.',
    'O parser PowerShell apenas lê os scripts, sem migrations ou testes SQL.',
    'O workflow GitHub Actions não foi disparado e Docker não foi executado por este comando.',
    'Esta evidência não comprova build da imagem, Health Check do contêiner, upload de artefatos ou implantação.',
  ],
};
const target = path.join(root, 'docs/evidencias/ci-validacao-estatica.json');
await fs.mkdir(path.dirname(target), { recursive: true });
await fs.writeFile(target, JSON.stringify(report, null, 2) + '\n', 'utf8');
console.log(`YAML: ${steps.length} etapas; Bash: ${bashResults.length} blocos; PowerShell: ${powershellResults.length} scripts e ${powershellBlocks.length} blocos. ${report.resultado}.`);
console.log(`Evidência: ${target}`);
if (!passed) process.exitCode = 1;
