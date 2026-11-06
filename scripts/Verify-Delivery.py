#!/usr/bin/env python3
"""Inspect and extract an academic ZIP without overwriting or deleting files.

Usage: python scripts/Verify-Delivery.py path/to/delivery.zip
       python scripts/Verify-Delivery.py path/to/delivery.zip --output-dir new-folder
       python scripts/Verify-Delivery.py --self-test

The PDF reader is optional at installation time, but missing it blocks complete
verification. Supported readers: PyPDF2, pypdf or PyMuPDF. Findings never include
matched credential values. Builds/tests must be run separately in the extracted
project. This script does not execute files from the archive.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib
import json
import re
import stat
import sys
import tempfile
import zipfile
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath


REQUIRED = (
    "PortalTarefas.sln", "global.json", "NuGet.Config", "README.md",
    "Dockerfile", "compose.yaml", ".env.example", ".github/workflows/ci.yml",
    "src/PortalTarefas.Web/PortalTarefas.Web.csproj",
    "src/PortalTarefas.Web/Program.cs",
    "src/PortalTarefas.Frontend/package.json",
    "src/PortalTarefas.Frontend/package-lock.json",
    "src/PortalTarefas.Frontend/src/api/schema.d.ts",
    "tests/PortalTarefas.Tests/PortalTarefas.Tests.csproj",
    "scripts/Test-Integration.ps1", "scripts/Generate-Client.ps1",
    "docs/openapi.json", "docs/MATRIZ_DE_REQUISITOS.md",
    "docs/STATUS_DE_EXECUCAO.md", "docs/ENTREGA_FINAL.md",
    "docs/PLANO_CAPTURAS_FINAIS.md", "docs/INVENTARIO_ENTREGA.json",
)
REPORTS = (
    "01-Pipeline", "02-MVC", "03-Persistencia", "04-API",
    "05-React", "06-Identity", "07-Observabilidade", "08-Entrega",
)
FORBIDDEN_PARTS = {
    ".git", ".local", ".tools", ".nuget", ".cache", "__pycache__",
    "node_modules", "bin", "obj", "dist", "artifacts", "testresults",
}
FORBIDDEN_SUFFIXES = {
    ".pfx", ".p12", ".key", ".pem", ".mdf", ".ldf", ".bak",
    ".tmp", ".pyc", ".log", ".tsbuildinfo", ".user", ".suo",
}
RESERVED = {"CON", "PRN", "AUX", "NUL"} | {
    f"{prefix}{number}" for prefix in ("COM", "LPT") for number in range(1, 10)
}
TOKEN_PATTERNS = {
    "github-token": re.compile(r"\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})\b"),
    "aws-access-key": re.compile(r"\b(?:AKIA|ASIA)[A-Z0-9]{16}\b"),
    "private-key": re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----"),
    "openai-api-key": re.compile(r"\bsk-(?:proj-)?[A-Za-z0-9_-]{40,}\b"),
    "google-api-key": re.compile(r"\bAIza[A-Za-z0-9_-]{35}\b"),
    "jwt": re.compile(r"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{12,}\b"),
}
PASSWORD_LITERAL = re.compile(
    r"(?i)\b(?:[A-Za-z_]*(?:password|senha|client_secret|api_key)[A-Za-z_]*)"
    r"[\"']?\s*(?:\{\s*get;\s*\}\s*)?[:=]\s*([\"'])([^\"'\r\n]*)\1"
)
CONNECTION_PASSWORD = re.compile(r"(?i)\b(?:Password|Pwd)\s*=\s*([^;\r\n\"']*)")
UNQUOTED_PASSWORD = re.compile(
    r"(?i)^\s*(?:[A-Za-z_]*(?:password|senha|client_secret|api_key)[A-Za-z_]*)"
    r"\s*[:=]\s*([^\s\"'#][^\r\n]*)$"
)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def safe_member(info: zipfile.ZipInfo) -> tuple[str, ...]:
    name = info.orig_filename
    if not name or "\x00" in name or "\\" in name or name.startswith("/"):
        raise ValueError("Nome de entrada ZIP vazio, absoluto ou incompatível.")
    parts = tuple(name.rstrip("/").split("/"))
    if any(part in ("", ".", "..") or ":" in part or
           part.endswith((" ", ".")) or part.split(".")[0].upper() in RESERVED
           for part in parts):
        raise ValueError("Entrada ZIP com caminho inseguro ou nome reservado.")
    mode = (info.external_attr >> 16) & 0xFFFF
    if stat.S_ISLNK(mode) or (stat.S_IFMT(mode) not in (0, stat.S_IFREG, stat.S_IFDIR)):
        raise ValueError("Links e arquivos especiais não são aceitos no ZIP.")
    if info.flag_bits & 1:
        raise ValueError("O ZIP acadêmico não deve conter entradas criptografadas.")
    return parts


def forbidden_path(relative: str) -> bool:
    path = PurePosixPath(relative)
    return (
        bool({p.lower() for p in path.parts} & FORBIDDEN_PARTS)
        or path.suffix.lower() in FORBIDDEN_SUFFIXES
        or path.name.lower() in {"secrets.json", "appsettings.local.json"}
        or (path.name.startswith(".env") and path.name != ".env.example")
        or relative.startswith("src/PortalTarefas.Web/wwwroot/app/")
    )


def external_or_generated(value: str, line: str, end: int) -> bool:
    value = value.strip()
    if not value or value in {"***", "<redacted>", "[redacted]", "REDACTED"}:
        return True
    if value.startswith(("$", "${", "{{", "<", "%", "process.env.", "os.environ")):
        return True
    if re.search(r"\$\(openssl\s+rand\b", value):
        return True
    # Prefixes concatenated with actual random generation are not passwords.
    tail = line[end:]
    if re.match(r"\s*\+\s*(?:Guid\.NewGuid|crypto\.|secrets\.)", tail):
        return True
    return False


def decode_text(data: bytes) -> str | None:
    if data.startswith((b"\xff\xfe", b"\xfe\xff")):
        return data.decode("utf-16")
    if b"\x00" in data[:4096]:
        return None
    try:
        return data.decode("utf-8-sig")
    except UnicodeDecodeError:
        return None


def scan_text(text: str, relative: str) -> list[dict]:
    findings = []
    for number, line in enumerate(text.splitlines(), 1):
        fingerprint = hashlib.sha256(line.encode("utf-8")).hexdigest()
        for rule, pattern in TOKEN_PATTERNS.items():
            if pattern.search(line):
                findings.append({"path": relative, "line": number, "rule": rule,
                                 "lineSha256": fingerprint, "status": "blocked"})
        for match in PASSWORD_LITERAL.finditer(line):
            if not external_or_generated(match[2], line, match.end()):
                findings.append({"path": relative, "line": number, "rule": "password-literal",
                                 "lineSha256": fingerprint, "status": "review-required"})
        for match in CONNECTION_PASSWORD.finditer(line):
            if not re.search(r"(?i)\b(?:Server|Data Source|Database|Initial Catalog|User ID)\s*=", line):
                continue
            value = match[1].strip()
            if not external_or_generated(value, line, match.end()) and not re.search(r"[\\{}]", value):
                findings.append({"path": relative, "line": number, "rule": "connection-password",
                                 "lineSha256": fingerprint, "status": "review-required"})
        configuration_text = (PurePosixPath(relative).suffix.lower() in
                              {".yml", ".yaml", ".ini", ".config", ".example", ".sh", ".md", ".txt"}
                              or PurePosixPath(relative).name.startswith(".env"))
        match = UNQUOTED_PASSWORD.search(line) if configuration_text else None
        if match and not external_or_generated(match[1], line, match.end()):
            findings.append({"path": relative, "line": number, "rule": "password-literal",
                             "lineSha256": fingerprint, "status": "review-required"})
    return findings


def apply_baseline(findings: list[dict], baseline_path: Path | None) -> None:
    """Allow only exact reviewed password false positives; never allow real keys."""
    entries = [] if baseline_path is None else json.loads(baseline_path.read_text(encoding="utf-8-sig"))
    if not isinstance(entries, list):
        raise ValueError("Baseline deve ser uma lista de exceções documentadas.")
    for finding in findings:
        if finding["rule"] not in {"password-literal", "connection-password"}:
            continue
        for entry in entries:
            if (all(entry.get(k) == finding[k] for k in ("path", "rule", "lineSha256"))
                    and isinstance(entry.get("reason"), str) and len(entry["reason"].strip()) >= 20):
                finding["status"] = "reviewed-false-positive"
                finding["reason"] = entry["reason"]
                break


def verify_pdf(path: Path) -> tuple[dict, str | None]:
    data = path.read_bytes()
    structural = data.startswith(b"%PDF-") and b"%%EOF" in data[-2048:]
    if not structural:
        return {"status": "failed", "reason": "Cabeçalho ou fim de PDF inválido."}, None
    for module_name in ("PyPDF2", "pypdf"):
        try:
            module = importlib.import_module(module_name)
        except ImportError:
            continue
        try:
            reader = module.PdfReader(str(path), strict=True)
            if reader.is_encrypted:
                raise ValueError("PDF protegido por senha.")
            texts = [page.extract_text() or "" for page in reader.pages]
            if not texts:
                raise ValueError("PDF sem páginas.")
            return {"status": "passed", "reader": module_name, "pages": len(texts),
                    "textCharacters": sum(map(len, texts))}, "\n".join(texts)
        except Exception as error:
            return {"status": "failed", "reason": type(error).__name__}, None
    try:
        import fitz
    except ImportError:
        return {"status": "blocked", "reason": "Instale PyPDF2, pypdf ou PyMuPDF para abrir os oito PDFs."}, None
    try:
        with fitz.open(path) as document:
            if document.needs_pass or document.page_count == 0:
                raise ValueError("PDF vazio ou criptografado.")
            texts = [page.get_text() for page in document]
            return {"status": "passed", "reader": "PyMuPDF", "pages": len(texts),
                    "textCharacters": sum(map(len, texts))}, "\n".join(texts)
    except Exception as error:
        return {"status": "failed", "reason": type(error).__name__}, None


def write_json(path: Path, value: object) -> None:
    with path.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def inspect_archive(archive_path: Path, output: Path, baseline: Path | None) -> dict:
    if output.exists():
        raise ValueError("A pasta de verificação já existe; escolha outra para não sobrescrever arquivos.")
    if not archive_path.is_file():
        raise ValueError("Informe um arquivo ZIP existente.")
    if output.resolve() == archive_path.resolve() or output.resolve() in archive_path.resolve().parents:
        raise ValueError("A saída da verificação deve ser separada do arquivo ZIP.")
    with zipfile.ZipFile(archive_path) as archive:
        entries = archive.infolist()
        if not entries or len(entries) > 20000:
            raise ValueError("Quantidade de entradas ZIP inválida ou excessiva.")
        names, roots, prepared = set(), set(), []
        total = 0
        for info in entries:
            parts = safe_member(info)
            normalized = "/".join(parts)
            if normalized.casefold() in names:
                raise ValueError("ZIP contém entradas duplicadas, inclusive variações de caixa.")
            names.add(normalized.casefold())
            roots.add(parts[0])
            if not info.is_dir() and len(parts) < 2:
                raise ValueError("ZIP deve ter uma única pasta principal.")
            total += info.file_size
            if (info.file_size > 256 * 1024 * 1024 or total > 2 * 1024 ** 3 or
                    (info.file_size > 1024 * 1024 and info.file_size / max(info.compress_size, 1) > 1000)):
                raise ValueError("ZIP excede os limites de tamanho ou expansão segura.")
            prepared.append((info, parts))
        if len(roots) != 1:
            raise ValueError("ZIP deve conter exatamente uma pasta principal.")
        root_name = next(iter(roots))
        output.mkdir(parents=True, exist_ok=False)
        extracted = output / "extraido"
        extracted.mkdir()
        files, findings, pdfs = [], [], []
        for info, parts in prepared:
            target = extracted.joinpath(*parts)
            if extracted.resolve() not in target.resolve().parents:
                raise ValueError("Tentativa de extrair fora da pasta de verificação.")
            if info.is_dir():
                target.mkdir(parents=True, exist_ok=True)
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            digest = hashlib.sha256()
            with archive.open(info) as source, target.open("xb") as destination:
                for chunk in iter(lambda: source.read(1024 * 1024), b""):
                    destination.write(chunk)
                    digest.update(chunk)
            relative = "/".join(parts[1:])
            files.append({"path": relative, "bytes": info.file_size,
                          "compressedBytes": info.compress_size, "sha256": digest.hexdigest()})
            if forbidden_path(relative):
                findings.append({"path": relative, "rule": "forbidden-local-file", "status": "blocked"})
            if target.suffix.lower() == ".pdf":
                result, text = verify_pdf(target)
                pdfs.append({"path": relative, **result})
            else:
                text = decode_text(target.read_bytes())
            if text is not None:
                findings.extend(scan_text(text, relative))
    apply_baseline(findings, baseline)
    project = extracted / root_name
    paths = {entry["path"] for entry in files}
    required = list(REQUIRED) + [f"docs/relatorios/{name}.{ext}" for name in REPORTS for ext in ("md", "pdf")]
    missing = [name for name in required if name not in paths]
    captures = sorted(path for path in paths if path.startswith("docs/evidencias/capturas-finais/") and path.lower().endswith(".png"))
    actual = {item["path"]: item for item in files}
    capture_manifests = []
    for name in sorted(paths):
        if not name.startswith("docs/evidencias/capturas-finais/") or not name.endswith("/manifest.json"):
            continue
        manifest = json.loads((project / name).read_text(encoding="utf-8-sig"))
        if manifest.get("resultado") != "aprovado":
            continue
        pictures = manifest.get("capturas", [])
        images_match = bool(pictures)
        for picture in pictures:
            filename = picture.get("arquivo", "")
            relative = str(PurePosixPath(name).parent / filename)
            if "/" in filename or "\\" in filename or relative not in actual or actual[relative]["sha256"] != picture.get("sha256"):
                images_match = False
                continue
            if not (project / relative).read_bytes().startswith(b"\x89PNG\r\n\x1a\n"):
                images_match = False
        states = {picture.get("estado") for picture in pictures if picture.get("roteiro") == 5}
        scenarios_complete = (
            sum(picture.get("roteiro") == 2 for picture in pictures) >= 4
            and sum(picture.get("roteiro") == 4 for picture in pictures) >= 1
            and {"idle", "loading", "empty", "success", "error", "problem-details"}.issubset(states)
        )
        requests_match = all(check.get("status") == check["esperado"]
                             for check in manifest.get("verificacoes", []) if "esperado" in check)
        capture_manifests.append({"path": name, "captures": len(pictures), "hashesAndPngValid": images_match,
                                  "requiredScenarios": scenarios_complete, "httpChecksMatch": requests_match})
    approved_captures = [manifest for manifest in capture_manifests if
                         manifest["hashesAndPngValid"] and manifest["requiredScenarios"] and manifest["httpChecksMatch"]]
    migrations = sorted(path for path in paths if path.startswith("src/PortalTarefas.Web/Data/Migrations/") and path.endswith(".cs"))
    manifest_path = project / "docs/INVENTARIO_ENTREGA.json"
    manifest_errors = []
    if manifest_path.exists():
        manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        indexed = manifest.get("essenciais", [])
        for expected in indexed:
            relative = expected["caminho"]
            if relative not in actual or actual[relative]["sha256"] != expected["sha256"] or actual[relative]["bytes"] != expected["bytes"]:
                manifest_errors.append(relative)
        if not indexed:
            manifest_errors.append("Inventário sem arquivos essenciais.")
        extras = set(actual) - {entry["caminho"] for entry in indexed} - {"docs/INVENTARIO_ENTREGA.json"}
        manifest_errors.extend(sorted(extras))
    mandatory_pdf_paths = {f"docs/relatorios/{name}.pdf" for name in REPORTS}
    mandatory_pdfs = [pdf for pdf in pdfs if pdf["path"] in mandatory_pdf_paths]
    unreviewed = [finding for finding in findings if finding["status"] != "reviewed-false-positive"]
    checks = {
        "zipCrcAndExtraction": True, "requiredFiles": not missing,
        "inventoryMatches": not manifest_errors and manifest_path.exists(),
        "eightReadablePdfs": len(mandatory_pdfs) == 8 and all(pdf["status"] == "passed" for pdf in mandatory_pdfs),
        "finalCapturesPresent": bool(approved_captures), "migrationsPresent": len(migrations) >= 3,
        "noUnreviewedSecurityFindings": not unreviewed,
    }
    summary = {
        "observedAtUtc": datetime.now(timezone.utc).isoformat(),
        "status": "passed" if all(checks.values()) else "pending",
        "archive": str(archive_path.resolve()), "archiveBytes": archive_path.stat().st_size,
        "archiveSha256": sha256_file(archive_path), "extractedProject": str(project.resolve()),
        "fileCount": len(files), "uncompressedBytes": sum(f["bytes"] for f in files),
        "checks": checks, "missingFiles": missing, "inventoryMismatches": manifest_errors,
        "pdfs": pdfs, "finalCaptureCount": approved_captures[-1]["captures"] if approved_captures else 0,
        "totalCaptureFiles": len(captures), "captureManifests": capture_manifests, "finalCaptures": captures,
        "securityFindings": len(findings), "unreviewedSecurityFindings": len(unreviewed),
        "limits": [
            "Nenhum arquivo existente foi substituído ou removido; nenhuma fonte foi executada.",
            "Restore/build/testes devem ser executados separadamente no projeto extraído.",
            "A leitura dos PDFs confirma estrutura, páginas e extração de texto; revisão visual de layout é independente.",
            "Capturas obrigatórias conferidas por manifesto aprovado, cenários, hashes e assinatura PNG; revisão visual é independente.",
            "Busca de padrões conhecidos, inclusive texto extraído de PDFs; não é garantia matemática de ausência de segredos.",
            "Valores encontrados não são incluídos no relatório; falsos positivos exigem baseline exato e justificativa.",
        ],
    }
    write_json(output / "inventario-extraido.json", files)
    write_json(output / "seguranca.json", findings)
    write_json(output / "verificacao.json", summary)
    return summary


def self_test() -> None:
    rejected = 0
    for name in ("../outside", "/absolute", "Root/../outside", "Root/C:/x", "Root/a\\b", "Root/CON.txt", "Root/file. "):
        try:
            safe_member(zipfile.ZipInfo(name))
        except ValueError:
            rejected += 1
    assert rejected == 7
    link = zipfile.ZipInfo("Root/link")
    link.external_attr = (stat.S_IFLNK | 0o777) << 16
    try:
        safe_member(link)
        raise AssertionError("Symlink accepted")
    except ValueError:
        pass
    assert safe_member(zipfile.ZipInfo("Root/docs/a.md")) == ("Root", "docs", "a.md")
    assert forbidden_path("src/node_modules/pkg/a.js")
    assert forbidden_path(".env") and not forbidden_path(".env.example")
    assert scan_text("ghp_" + "A" * 36, "sample.txt")[0]["rule"] == "github-token"
    key = "pass" + "word"
    assert scan_text(key + ' = ""', "sample.txt") == []
    assert scan_text(key + ' = "$EXTERNAL_VALUE"', "sample.txt") == []
    assert scan_text(key + '="Aa1!$(openssl rand -hex 24)"', "sample.sh") == []
    assert scan_text('const ' + key + ' = credentials.' + key + ';', "sample.mjs") == []
    assert scan_text(key.upper() + '=synthetic-only', "sample.example")[0]["rule"] == "password-literal"
    assert scan_text(key + ' = "Aa1!" + Guid.NewGuid().ToString("N")', "sample.cs") == []
    secret_findings = scan_text(key + ' = "synthetic-test-value"', "sample.txt")
    assert secret_findings[0]["rule"] == "password-literal"
    with tempfile.TemporaryDirectory(prefix="portal-zip-verifier-") as directory:
        root = Path(directory)
        baseline = root / "baseline.json"
        allowed = [{**secret_findings[0], "reason": "Valor sintético criado apenas pelo autoteste do verificador."}]
        baseline.write_text(json.dumps(allowed), encoding="utf-8")
        apply_baseline(secret_findings, baseline)
        assert secret_findings[0]["status"] == "reviewed-false-positive"
        token_findings = scan_text("ghp_" + "A" * 36, "sample.txt")
        baseline.write_text(json.dumps([{**token_findings[0], "reason": "Tentativa sintética de suprimir token não deve ser aceita."}]), encoding="utf-8")
        apply_baseline(token_findings, baseline)
        assert token_findings[0]["status"] == "blocked"
        evil_zip = root / "unsafe.zip"
        with zipfile.ZipFile(evil_zip, "w") as archive:
            archive.writestr("Root/../outside.txt", "outside")
        try:
            inspect_archive(evil_zip, root / "output", None)
            raise AssertionError("Traversal archive accepted")
        except ValueError:
            assert not (root / "output").exists()
        duplicate_zip = root / "duplicate.zip"
        with zipfile.ZipFile(duplicate_zip, "w") as archive:
            archive.writestr("Root/a.txt", "first")
            archive.writestr("Root/A.txt", "second")
        try:
            inspect_archive(duplicate_zip, root / "output", None)
            raise AssertionError("Case duplicate accepted")
        except ValueError:
            assert not (root / "output").exists()
    print("Autoteste aprovado: caminhos, symlink, duplicação, arquivos privados, segredos e baseline.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("archive", type=Path, nargs="?")
    parser.add_argument("--output-dir", type=Path, help="Pasta nova; o comando recusa qualquer destino existente.")
    parser.add_argument("--baseline", type=Path, help="Lista JSON de falsos positivos exatos: path, rule, lineSha256, reason.")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        self_test()
        return 0
    if args.archive is None:
        parser.error("Informe o ZIP ou utilize --self-test.")
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    output = args.output_dir or args.archive.parent / f"verificacao-{stamp}"
    result = inspect_archive(args.archive, output, args.baseline)
    print(json.dumps({"status": result["status"], "report": str((output / "verificacao.json").resolve()),
                      "extractedProject": result["extractedProject"], "archiveSha256": result["archiveSha256"],
                      "files": result["fileCount"], "captures": result["finalCaptureCount"],
                      "unreviewedSecurityFindings": result["unreviewedSecurityFindings"]}, ensure_ascii=False))
    return 0 if result["status"] == "passed" else 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValueError, OSError, zipfile.BadZipFile, RuntimeError, json.JSONDecodeError) as error:
        # Do not print matched contents, including values in parser exceptions.
        print(f"Verificação interrompida ({type(error).__name__}); nenhum arquivo existente foi substituído.", file=sys.stderr)
        raise SystemExit(2)
