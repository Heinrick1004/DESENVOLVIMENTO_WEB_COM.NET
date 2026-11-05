# Docker no Windows — preparação e bloqueio observado

Em 29/10/2026, o Docker Desktop/Engine e o WSL não estavam instalados. Foram preparados apenas Docker CLI 29.9.2 e Compose v5.6.1 portáteis em `.tools/docker-portable`, sem serviço ou instalação global. As versões e a configuração Compose foram verificadas; a execução de contêineres permanece bloqueada. Os resultados e hashes estão em [docker-ambiente.json](evidencias/finalizacao-r8-20261029/docker-ambiente.json).

## Ambiente encontrado

| Verificação real | Resultado |
|---|---|
| Windows | Windows 11 Pro 26H2, x64, build 26300.9711 |
| Memória | 13,9 GiB |
| Virtualização | Firmware habilitado; hypervisor presente |
| Conta em execução | Sem elevação administrativa |
| Serviços Docker/WSL | `com.docker.service`, `docker`, `vmcompute`, `LxssManager` e `WslService` inexistentes |
| Serviço `LanmanServer` | Em execução, inicialização automática |
| Recursos opcionais | Consulta exige elevação; estados não confirmados |
| `wsl --version`, `wsl --status`, `wsl --list --verbose` | Informam WSL não instalado; códigos 1, 50 e 1 |
| `docker --version` portátil | Aprovado: 29.9.2 |
| `docker compose version` portátil | Aprovado: v5.6.1 |
| `docker info` portátil | Código 1: pipe local `docker_engine` inexistente |

O Windows detectado corresponde à versão 26H2 em suporte na [relação oficial Microsoft](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information). A indicação isolada de SLAT retornada pelo CIM não foi usada para concluir incompatibilidade, pois havia hypervisor ativo; a Microsoft descreve essa limitação de diagnóstico em [requisitos do Hyper-V](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/host-hardware-requirements).

## O que os clientes portáteis permitem conferir

No PowerShell, aberto na raiz `PortalTarefas`, prepare somente esta sessão:

```powershell
$portalTools = Join-Path (Get-Location) '.tools/docker-portable'
$env:PATH = $portalTools + ';' + $env:PATH
$env:DOCKER_CONFIG = Join-Path $portalTools 'config'
$env:DOCKER_HOST = $null
$env:DOCKER_CONTEXT = $null
$env:DOCKER_TLS_VERIFY = $null
$env:DOCKER_CERT_PATH = $null
docker --version
docker compose version
docker info
```

O último comando falhou nesta execução por ausência de Engine. O diretório de configuração contém apenas a localização do plugin Compose; não contém credenciais ou contexto remoto. Nenhum `dockerd.exe` foi extraído ou executado.

`docker compose --env-file .tools/docker-portable/empty.env config --quiet` foi executado com dois cenários: variáveis obrigatórias ausentes, corretamente rejeitadas; e marcadores sintéticos definidos apenas no processo, aceitos. `config --services` retornou `db` e `web`. A conexão e o caminho do certificado usados nesse teste eram marcadores, sem acesso a banco ou certificado real. Não publique o resultado de `config` expandido com valores reais; prefira `--quiet`.

Os binários vieram do [índice oficial Docker](https://download.docker.com/win/static/stable/x86_64/) e da [release oficial Compose v5.6.1](https://github.com/docker/compose/releases/tag/v5.6.1). O hash do Compose corresponde ao digest publicado pela API GitHub. Os executáveis retornaram `NotSigned` no Authenticode; o hash do ZIP Docker foi calculado localmente, sem alegação de checksum independente. As ferramentas ficam ignoradas pelo Git e não devem integrar a entrega acadêmica; os links e hashes permitem restaurá-las.

## Instalação manual pendente

Os comandos desta seção **não foram executados**. A habilitação inicial do WSL altera recursos do Windows, exige administrador e pode solicitar reinício; depende de autorização e interação do responsável pela máquina. A preparação local não substitui essa etapa.

1. Abra PowerShell como administrador e execute:

   ```powershell
   wsl --install --no-distribution
   ```

2. Se o Windows solicitar reinício, salve o trabalho e reinicie manualmente. Depois, confira:

   ```powershell
   wsl --update
   wsl --version
   wsl --status
   ```

   A instalação sem distribuição é suportada pela [documentação WSL](https://learn.microsoft.com/en-us/windows/wsl/basic-commands). O Docker requer WSL 2.1.5 ou posterior e não exige instalar Ubuntu; confira o [backend WSL 2](https://docs.docker.com/desktop/features/wsl/).

3. Consulte o instalador antes de executar a instalação interativa:

   ```powershell
   winget show --exact --id Docker.DockerDesktop --source winget
   winget install --exact --id Docker.DockerDesktop --source winget --interactive
   ```

   As opções de seleção e instalação interativa são descritas pela [Microsoft](https://learn.microsoft.com/en-us/windows/package-manager/winget/install). Alternativamente, use o download da [página oficial Docker Desktop para Windows](https://docs.docker.com/desktop/setup/install/windows-install/).

4. Escolha instalação por usuário e backend WSL 2. O instalador oficial também aceita `install --user --backend=wsl-2 --no-windows-containers`. A instalação por usuário não exige administrador, mas a preparação inicial do WSL exige. Abra o Docker Desktop manualmente e examine os termos apresentados antes de aceitá-los. Essas etapas seguem o [procedimento Docker](https://docs.docker.com/desktop/setup/install/windows-install/).

5. Em um novo terminal, sem o ambiente isolado usado acima, execute:

   ```powershell
   docker --version
   docker compose version
   docker info
   docker info --format '{{.OSType}}'
   ```

   O servidor deve responder e o tipo deve ser `linux`, pois as imagens deste projeto são Linux. Se necessário, selecione o modo Linux no Docker Desktop. A presença de versões de cliente, isoladamente, não comprova essa condição.

## Retomada da validação do projeto

Com o Engine respondendo, forneça credenciais externas para um banco exclusivo `PortalTarefas_Tests_<sufixo>` e um certificado local real, e use o procedimento de homologação do README e os scripts existentes. Registre build da imagem, inicialização controlada, migrations, HTTPS, API, MVC, React, `/health/live`, `/health/ready` e versão vinculada ao commit.

Até essa execução real, build/execução Docker, Health Checks de contêiner e homologação em contêiner continuam bloqueados. Esta preparação não executou instalação de sistema, alteração de recursos, reinicialização, interface gráfica, novas capturas ou mudanças em imagens existentes.
