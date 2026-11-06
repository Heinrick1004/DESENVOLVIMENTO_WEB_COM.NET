// Teste local do bloco real de certificado: npm ci no frontend, Node 24 e Bash.
// Dotnet/chmod e a escrita de GITHUB_ENV são interceptados; nenhum cofre é acessado.
// Valores sintéticos existem somente na memória/ambiente dos processos e não são exibidos.
import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const { default: yaml } = await import(pathToFileURL(path.join(root, 'src/PortalTarefas.Frontend/node_modules/js-yaml/index.js')).href);
const source = await fs.readFile(path.join(root, '.github/workflows/ci.yml'), 'utf8');
const workflow = yaml.load(source);
const certificateStep = workflow.jobs.validar.steps.find(step => step.run?.includes('PORTAL_CI_CERTIFICATE_PASSWORD'));
if (!certificateStep || certificateStep.shell !== 'bash' ||
    certificateStep.env?.PORTAL_CI_CERTIFICATE_PASSWORD !== '${{ secrets.PORTAL_CI_CERTIFICATE_PASSWORD }}') {
  throw new Error('Não foi encontrada a etapa Bash com o Actions Secret esperado.');
}

const bash = process.env.PORTAL_STATIC_BASH ?? (process.platform === 'win32' ? 'C:/Program Files/Git/bin/bash.exe' : 'bash');
const syntheticPassword = `aA1!${crypto.randomBytes(24).toString('hex')}`;
const scenarios = [
  { name: 'ausente', value: undefined, expectedExit: 1 },
  { name: 'curto_15_caracteres', value: syntheticPassword.slice(0, 15), expectedExit: 1 },
  { name: 'multilinha_crlf', value: `${syntheticPassword}\r\n`, expectedExit: 1 },
  { name: 'multilinha_lf', value: `${syntheticPassword}\n`, expectedExit: 1 },
  { name: 'retorno_cr', value: `${syntheticPassword}\r`, expectedExit: 1 },
  { name: 'valido_16_caracteres_linha_unica', value: syntheticPassword.slice(0, 16), expectedExit: 0 },
];

// /dev/null absorve a redireção do bloco original. A função printf confere
// formato e valor da atribuição antes de retornar sem persistir seus bytes.
// A função echo reconhece e suprime somente o protocolo add-mask correto.
const harness = `
set -e
set -o pipefail
ci_mask_checked=0
ci_env_checked=0
ci_dotnet_checked=0
ci_chmod_checked=0
trap 'builtin printf "PORTAL_SECRET_TEST_FLAGS=%s,%s,%s,%s\\n" "$ci_mask_checked" "$ci_env_checked" "$ci_dotnet_checked" "$ci_chmod_checked"' EXIT
echo() {
  if [ "$#" -eq 1 ] && [ "$1" = "::add-mask::$PORTAL_CI_CERTIFICATE_PASSWORD" ]; then
    ci_mask_checked=1
    return 0
  fi
  builtin echo "$@"
}
printf() {
  if [ "$#" -eq 2 ] && [ "$1" = 'ASPNETCORE_Kestrel__Certificates__Default__Password=%s\\n' ] &&
     [ "$2" = "$PORTAL_CI_CERTIFICATE_PASSWORD" ] && [ "$GITHUB_ENV" = '/dev/null' ]; then
    ci_env_checked=1
    return 0
  fi
  builtin printf "$@"
}
dotnet() {
  if [ "$#" -eq 6 ] && [ "$1" = dev-certs ] && [ "$2" = https ] &&
     [ "$3" = --export-path ] && [ "$4" = "$RUNNER_TEMP/portal-ci.pfx" ] &&
     [ "$5" = --password ] && [ "$6" = "$PORTAL_CI_CERTIFICATE_PASSWORD" ]; then
    ci_dotnet_checked=1
    return 0
  fi
  return 71
}
chmod() {
  if [ "$#" -eq 2 ] && [ "$1" = 644 ] && [ "$2" = "$RUNNER_TEMP/portal-ci.pfx" ]; then
    ci_chmod_checked=1
    return 0
  fi
  return 72
}
${certificateStep.run}
`;

const results = scenarios.map(scenario => {
  const env = { ...process.env, GITHUB_ENV: '/dev/null', RUNNER_TEMP: '/portal-ci-secret-test' };
  delete env.PORTAL_CI_CERTIFICATE_PASSWORD;
  if (scenario.value !== undefined) env.PORTAL_CI_CERTIFICATE_PASSWORD = scenario.value;
  const run = spawnSync(bash, ['--noprofile', '--norc', '-s'], {
    input: harness, encoding: 'utf8', env, timeout: 10000, maxBuffer: 64 * 1024,
  });
  const stdout = run.stdout ?? '';
  const stderr = run.stderr ?? '';
  const flags = /PORTAL_SECRET_TEST_FLAGS=([01]),([01]),([01]),([01])/.exec(stdout);
  const checks = {
    mascaraSolicitada: flags?.[1] === '1',
    atribuicaoGithubEnvConferidaSemPersistirValor: flags?.[2] === '1',
    argumentosDotnetConferidos: flags?.[3] === '1',
    permissaoArquivoConferida: flags?.[4] === '1',
  };
  const noExposure = ![stdout, stderr].some(output =>
    output.includes(syntheticPassword) || (scenario.value && output.includes(scenario.value)));
  const valid = scenario.expectedExit === 0;
  const observedChecks = Object.values(checks);
  const guardsCorrect = Boolean(flags) && (valid ? observedChecks.every(Boolean) : observedChecks.every(value => !value));
  const passed = !run.error && run.status === scenario.expectedExit && noExposure && guardsCorrect;
  return {
    cenario: scenario.name, exitCodeEsperado: scenario.expectedExit, exitCode: run.status,
    aprovado: passed, dadosSinteticosAusentesDaSaida: noExposure, verificacoes: checks,
    falhaAoIniciarProcesso: Boolean(run.error),
    // Nunca registrar stdout/stderr, valores, argumentos ou ambiente do processo.
  };
});

const passed = results.every(result => result.aprovado);
const report = {
  instanteUtc: new Date().toISOString(),
  natureza: 'Teste local com stubs do bloco Bash extraído do workflow atual.',
  comandoExecutado: 'node scripts/Test-CiSecret.mjs',
  node: process.version, workflow: '.github/workflows/ci.yml',
  workflowSha256: crypto.createHash('sha256').update(source).digest('hex'),
  etapa: certificateStep.name,
  blocoSha256: crypto.createHash('sha256').update(certificateStep.run).digest('hex'),
  segredoGithubRealAcessado: false, certificadoRealGerado: false,
  valoresDeSenhaGravadosOuPublicados: false,
  resultado: passed ? 'aprovado com stubs locais' : 'reprovado com stubs locais',
  aprovados: results.filter(result => result.aprovado).length,
  reprovados: results.filter(result => !result.aprovado).length,
  ignorados: 0, cenarios: results,
  limites: [
    'Senhas aleatórias são sintéticas e transitórias; nenhum valor real de Actions Secrets é lido.',
    'Dotnet e chmod são funções substitutas: este teste não gera PFX nem inicia um contêiner.',
    'A atribuição de GITHUB_ENV é validada em memória com saída direcionada a /dev/null.',
    'O protocolo add-mask é conferido e suprimido pelo harness; saídas brutas nunca integram a evidência.',
    'A aprovação valida as guardas e argumentos do bloco, sem comprovar configuração/consumo do cofre GitHub ou execução remota de E4.2.',
  ],
};
const outputFlag = process.argv.indexOf('--output');
const output = path.resolve(root, outputFlag >= 0 ? process.argv[outputFlag + 1] : 'docs/evidencias/finalizacao-20261106/ci-secret-local.json');
await fs.mkdir(path.dirname(output), { recursive: true });
await fs.writeFile(output, `${JSON.stringify(report, null, 2)}\n`, 'utf8');
console.log(JSON.stringify({ resultado: report.resultado, aprovados: report.aprovados, reprovados: report.reprovados, ignorados: 0, evidencia: path.relative(root, output) }));
if (!passed) process.exitCode = 1;
