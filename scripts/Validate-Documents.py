#!/usr/bin/env python3
"""Valida a documentação acadêmica sem alterar fontes, relatórios ou históricos.

Pré-requisito: Python 3.10+ e PyPDF2 3.0.1 (python -m pip install PyPDF2==3.0.1).
O PDF acadêmico é opcional, externo e nunca é copiado para a entrega.
"""

from __future__ import annotations

import argparse
from collections import Counter, defaultdict
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import unicodedata
from urllib.parse import unquote, urlsplit

try:
    from PyPDF2 import PdfReader
except ImportError:
    raise SystemExit("Instale o pré-requisito: python -m pip install PyPDF2==3.0.1")


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT = "docs/evidencias/finalizacao-20261106/validacao-documental.json"
VALID_STATES = {
    "Concluído e verificado", "Implementado sem execução", "Reprovado",
    "Bloqueado", "Pendente", "Não aplicável", "Limitação histórica",
}
EXPECTED_GROUPS = {"G": 4, "R1": 30, "R2": 29, "R3": 31, "R4": 31,
                   "R5": 31, "R6": 31, "R7": 30, "R8": 31}
SOURCE_ANNEX = (
    "src/PortalTarefas.Web/Program.cs",
    "src/PortalTarefas.Web/Middleware/RequisicaoMiddleware.cs",
    "src/PortalTarefas.Web/Controllers/DiagnosticoController.cs",
)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def plain_markdown(value: str) -> str:
    value = re.sub(r"!?\[([^\]]*)\]\([^)]+\)", r"\1", value)
    return value.replace("**", "").replace("`", "")


def normalize(value: str) -> str:
    return re.sub(r"\s+", "", value.replace("\u00ad", "")).casefold()


def normalize_academic(value: str) -> str:
    ascii_text = unicodedata.normalize("NFD", value).encode("ascii", "ignore").decode()
    return re.sub(r"[^a-z0-9]", "", ascii_text.casefold())


def observed_git() -> dict:
    if not (ROOT / ".git").exists():
        return {"commitObservado": None, "branchObservada": None,
                "limite": "Cópia sem metadados Git próprios; não atribui o repositório de uma pasta ancestral à entrega extraída."}
    def command(*args: str) -> str | None:
        try:
            return subprocess.check_output(["git", "-C", str(ROOT), *args],
                                           stderr=subprocess.DEVNULL, text=True).strip()
        except (OSError, subprocess.CalledProcessError):
            return None
    return {
        "commitObservado": command("rev-parse", "HEAD"),
        "branchObservada": command("branch", "--show-current"),
        "limite": "Os hashes descrevem os arquivos atuais; o SHA Git não incorpora alterações não commitadas.",
    }


def check_requirements(errors: list[str]) -> tuple[dict, list[dict]]:
    matrix = ROOT / "docs/MATRIZ_DE_REQUISITOS.md"
    rows = []
    for line_number, line in enumerate(read_text(matrix).splitlines(), 1):
        if not re.match(r"^\|\s*(?:G|R\d)-\d+\s*\|", line):
            continue
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if len(cells) != 7:
            errors.append(f"Matriz, linha {line_number}: esperadas sete colunas, encontradas {len(cells)}.")
            continue
        rows.append(dict(zip(("id", "paginaItem", "exigencia", "arquivos", "procedimento",
                              "evidencia", "estado"), cells)))
    ids = Counter(row["id"] for row in rows)
    duplicated = [key for key, count in ids.items() if count > 1]
    expected_ids = {f"{group}-{number:02d}" for group, total in EXPECTED_GROUPS.items()
                    for number in range(1, total + 1)}
    missing_ids = sorted(expected_ids - set(ids))
    extra_ids = sorted(set(ids) - expected_ids)
    groups = Counter(row["id"].split("-", 1)[0] for row in rows)
    states = Counter(row["estado"] for row in rows)
    by_group = defaultdict(Counter)
    for row in rows:
        by_group[row["id"].split("-", 1)[0]][row["estado"]] += 1
    unknown = sorted(set(states) - VALID_STATES)
    if len(rows) != 248 or len(ids) != 248 or duplicated:
        errors.append("A matriz deve conter exatamente 248 requisitos com IDs únicos.")
    if missing_ids or extra_ids:
        errors.append("Os identificadores da matriz divergem dos 248 IDs originais.")
    if dict(groups) != EXPECTED_GROUPS:
        errors.append("A distribuição dos 248 requisitos por roteiro diverge da fonte acadêmica.")
    if unknown:
        errors.append("A matriz contém estados desconhecidos: " + ", ".join(unknown))
    for row in rows:
        if not row["evidencia"] or not re.match(r"^\d+\s*/\s*.+$", row["paginaItem"]):
            errors.append(f"{row['id']}: referência de página/item ou evidência ausente.")
    return {
        "arquivo": "docs/MATRIZ_DE_REQUISITOS.md", "sha256": digest(matrix),
        "quantidade": len(rows), "idsUnicos": len(ids), "idsDuplicados": duplicated,
        "idsAusentes": missing_ids, "idsInesperados": extra_ids,
        "estadosDesconhecidos": unknown, "estados": dict(states),
        "estadosPorRoteiro": {key: dict(value) for key, value in by_group.items()},
        "naoConcluidos": [{key: row[key] for key in ("id", "paginaItem", "estado")}
                           for row in rows if row["estado"] not in {"Concluído e verificado", "Não aplicável"}],
        "limite": "Classificações são contadas da matriz; sua veracidade depende das evidências de execução, não deste validador.",
    }, rows


def check_links(errors: list[str]) -> dict:
    sources = [ROOT / "README.md", *sorted((ROOT / "docs").rglob("*.md"))]
    missing, count = [], 0
    for source in sources:
        for target in re.findall(r"!?\[[^\]]*\]\(([^)]+)\)", read_text(source)):
            target = target.strip().strip("<>")
            parts = urlsplit(target)
            if parts.scheme or not parts.path:
                continue
            count += 1
            if not (source.parent / unquote(parts.path)).exists():
                missing.append({"fonte": source.relative_to(ROOT).as_posix(), "destino": target})
    if missing:
        errors.append(f"Há {len(missing)} links locais sem destino.")
    return {"arquivosMarkdown": len(sources), "linksLocaisVerificados": count,
            "destinosAusentes": missing,
            "limite": "Confere arquivos locais; não acessa URLs externas nem valida âncoras internas."}


def check_reports(errors: list[str]) -> list[dict]:
    sources = sorted((ROOT / "docs/relatorios").glob("[0-9][0-9]-*.md"))
    if len(sources) != 8 or [source.name[:2] for source in sources] != [f"{i:02d}" for i in range(1, 9)]:
        errors.append("Devem existir oito relatórios Markdown numerados de 01 a 08.")
    manifest_path = ROOT / "docs/evidencias/relatorios-pdf.json"
    manifest = {entry["pdf"]: entry for entry in json.loads(read_text(manifest_path))}
    results = []
    for source in sources:
        pdf = source.with_suffix(".pdf")
        entry = {"fonte": source.relative_to(ROOT).as_posix(),
                 "pdf": pdf.relative_to(ROOT).as_posix(), "sha256FonteMarkdown": digest(source)}
        if not pdf.is_file():
            errors.append(f"PDF ausente: {pdf.name}")
            entry["erro"] = "PDF ausente"
            results.append(entry)
            continue
        try:
            reader = PdfReader(str(pdf), strict=True)
            page_texts = [page.extract_text() or "" for page in reader.pages]
            pdf_text = normalize("\n".join(page_texts))
            headings = [plain_markdown(title) for title in
                        re.findall(r"^#{1,6}\s+(.+)$", read_text(source), re.M)]
            missing = [title for title in headings if normalize(title) not in pdf_text]
            sha = digest(pdf)
            record = manifest.get(pdf.name, {})
            matches_manifest = (record.get("sha256") == sha and record.get("paginas") == len(reader.pages)
                                and record.get("bytes") == pdf.stat().st_size)
            empty_pages = [number for number, text in enumerate(page_texts, 1) if not text.strip()]
            entry.update({"bytes": pdf.stat().st_size, "paginas": len(reader.pages), "sha256": sha,
                          "titulosConferidos": len(headings), "titulosAusentes": missing,
                          "paginasSemTexto": empty_pages, "manifestoConsistente": matches_manifest})
            if missing or empty_pages or not matches_manifest or not reader.pages:
                errors.append(f"{pdf.name}: título, texto de página, quantidade ou manifesto inconsistentes.")
        except Exception as exc:
            entry["erro"] = f"{type(exc).__name__}: {exc}"
            errors.append(f"Não foi possível validar {pdf.name}.")
        results.append(entry)
    return results


def check_annex(errors: list[str]) -> dict:
    annex = normalize(read_text(ROOT / "docs/relatorios/anexos/01-Codigo.md"))
    matches = {source: normalize(read_text(ROOT / source)) in annex for source in SOURCE_ANNEX}
    if not all(matches.values()):
        errors.append("O anexo 01 diverge de pelo menos uma fonte de código obrigatória.")
    return matches


def check_academic(path: Path | None, rows: list[dict], errors: list[str]) -> dict:
    if path is None:
        return {"executada": False, "motivo": "PDF acadêmico externo não informado; verificação opcional."}
    reader = PdfReader(str(path), strict=True)
    pages = [normalize_academic(page.extract_text() or "") for page in reader.pages]
    checked, mismatches = 0, []
    for row in rows:
        if row["id"].startswith("G-"):
            continue  # Os quatro itens gerais sintetizam a página 2 em vez de citá-la integralmente.
        checked += 1
        page_number = int(row["paginaItem"].split("/", 1)[0])
        if not 1 <= page_number <= len(pages) or normalize_academic(row["exigencia"]) not in pages[page_number - 1]:
            mismatches.append({"id": row["id"], "pagina": page_number})
    if len(pages) != 26 or mismatches or checked != 244:
        errors.append("O PDF acadêmico ou as referências dos 244 itens R1–R8 divergem da fonte esperada.")
    return {"executada": True, "fonteExterna": str(path.resolve()), "sha256": digest(path),
            "paginas": len(pages), "textosRoteirosConferidos": checked, "divergencias": mismatches,
            "itensGerais": "G-01 a G-04 são sínteses da página 2 e exigem revisão humana.",
            "copiadoParaEntrega": False}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--academic-pdf", type=Path, help="Caminho opcional para Roteiros.pdf externo.")
    parser.add_argument("--output", type=Path, default=Path(DEFAULT_OUTPUT),
                        help="JSON de saída relativo à raiz do projeto ou caminho absoluto.")
    args = parser.parse_args()
    errors: list[str] = []
    requirements, rows = check_requirements(errors)
    links = check_links(errors)
    reports = check_reports(errors)
    annex = check_annex(errors)
    academic = check_academic(args.academic_pdf, rows, errors)
    result = {
        "instanteUtc": datetime.now(timezone.utc).isoformat(), "natureza": "Validação documental atual, sem modificar evidências históricas.",
        "git": observed_git(), "requisitos": requirements, "links": links, "pdfs": reports,
        "anexoFonteCompletoAtualizado": annex, "pdfAcademico": academic,
        "aprovado": not errors, "erros": errors,
        "limites": ["Não executa testes da aplicação, CI remoto, Docker ou navegador.",
                    "Hashes e títulos não substituem revisão visual de tabelas, código e imagens.",
                    "Não declara 100% dos requisitos atendidos nem transforma bloqueios em aprovações."]}
    output = args.output if args.output.is_absolute() else ROOT / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    summary = {"aprovado": result["aprovado"], "requisitos": requirements["quantidade"],
               "estados": requirements["estados"], "pdfs": len(reports),
               "linksAusentes": len(links["destinosAusentes"]), "erros": errors, "saida": str(output)}
    print(json.dumps(summary, ensure_ascii=sys.stdout.encoding.lower().replace("-", "") != "utf8"))
    return 0 if result["aprovado"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
