from pathlib import Path
import argparse
from datetime import datetime, timezone
import html
import json
import re
import subprocess
import hashlib
import uuid
import os
import shutil

parser = argparse.ArgumentParser(description='Regenera somente os relatórios selecionados, preservando os demais PDFs e metadados.')
parser.add_argument('--reports', nargs='+', choices=[f'{number:02}' for number in range(1, 9)], required=True)
parser.add_argument('--browser', help='Executável Chrome/Chromium/Edge; autodetectado quando omitido.')
options = parser.parse_args()

root = Path(__file__).resolve().parents[1]
reports = root / 'docs/relatorios'
work = root / '.local/relatorios-html'
work.mkdir(parents=True, exist_ok=True)
candidates = [options.browser, os.environ.get('PORTAL_EVIDENCE_BROWSER'),
              r'C:\Program Files\Google\Chrome\Application\chrome.exe',
              r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
              shutil.which('chromium'), shutil.which('google-chrome')]
chrome = next((Path(candidate) for candidate in candidates if candidate and Path(candidate).is_file()), None)
if not chrome:
    parser.error('Informe --browser com um Chrome, Chromium ou Edge instalado.')
profile = work / ('chrome-profile-' + uuid.uuid4().hex)

def inline(text, source):
    tokens = []
    def hold(content):
        tokens.append(content)
        return 'ZZTOKEN' + str(len(tokens) - 1) + 'ZZ'
    def url(target):
        if target.startswith(('https:', 'http:', '#')):
            return target
        return (source.parent / target).resolve().as_uri()
    text = re.sub(r'`([^`]+)`', lambda m: hold('<code>' + html.escape(m.group(1)) + '</code>'), text)
    text = re.sub(r'!\[([^\]]*)\]\(([^)]+)\)', lambda m: hold('<figure><img src="' + html.escape(url(m.group(2)), quote=True) + '" alt="' + html.escape(m.group(1), quote=True) + '"><figcaption>' + html.escape(m.group(1)) + '</figcaption></figure>'), text)
    def link(match):
        label, target = match.group(1), match.group(2)
        if target.startswith(('https:', 'http:', '#')):
            return hold('<a href="' + html.escape(target, quote=True) + '">' + html.escape(label) + '</a>')
        # Chrome converte links file: em caminhos absolutos da máquina de geração.
        # Imprimir o caminho relativo mantém a referência útil no ZIP extraído.
        return hold('<span>' + html.escape(label) + ' <small class="local-path">(' + html.escape(target) + ')</small></span>')
    text = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', link, text)
    text = html.escape(text)
    text = re.sub(r'\*\*([^*]+)\*\*', r'<strong>\1</strong>', text)
    for i, content in enumerate(tokens):
        text = text.replace('ZZTOKEN' + str(i) + 'ZZ', content)
    return text

def render(text, source):
    lines = text.splitlines()
    out, paragraph, list_type = [], [], None
    i = 0
    def flush():
        if paragraph:
            out.append('<p>' + inline(' '.join(paragraph), source) + '</p>'); paragraph.clear()
    def close_list():
        nonlocal list_type
        if list_type:
            out.append('</' + list_type + '>'); list_type = None
    while i < len(lines):
        line = lines[i]
        if line.startswith('```'):
            flush(); close_list(); code = []; i += 1
            while i < len(lines) and not lines[i].startswith('```'):
                code.append(lines[i]); i += 1
            out.append('<pre><code>' + html.escape('\n'.join(code)) + '</code></pre>')
        elif re.match(r'^#{1,6} ', line):
            flush(); close_list(); match = re.match(r'^(#{1,6}) (.*)', line); level = len(match.group(1))
            out.append(f'<h{level}>' + inline(match.group(2), source) + f'</h{level}>')
        elif line.startswith('|'):
            flush(); close_list(); table = []
            while i < len(lines) and lines[i].startswith('|'):
                cells = [c.strip() for c in lines[i].strip('|').split('|')]
                if not all(re.match(r'^:?-+:?$', c) for c in cells): table.append(cells)
                i += 1
            i -= 1
            out.append('<table>')
            for j, cells in enumerate(table):
                tag = 'th' if j == 0 else 'td'
                out.append('<tr>' + ''.join('<' + tag + '>' + inline(c, source) + '</' + tag + '>' for c in cells) + '</tr>')
            out.append('</table>')
        elif re.match(r'^(?:- |\d+\. )', line):
            flush(); new_type = 'ul' if line.startswith('- ') else 'ol'
            if list_type != new_type:
                close_list(); list_type = new_type; out.append('<' + list_type + '>')
            out.append('<li>' + inline(re.sub(r'^(?:- |\d+\. )', '', line), source) + '</li>')
        elif not line.strip():
            flush(); close_list()
        elif line.startswith('!['):
            flush(); close_list(); out.append(inline(line, source))
        else:
            close_list(); paragraph.append(line)
        i += 1
    flush(); close_list()
    return '\n'.join(out)

style = '''@page {size:A4;margin:20mm 16mm} body{font-family:Arial,sans-serif;color:#17212e;font-size:10.5pt;line-height:1.5} h1{font-size:21pt;line-height:1.15;border-bottom:2px solid #233f57;padding-bottom:8mm} h2{font-size:14pt;margin-top:7mm;break-after:avoid} h3{font-size:12pt;break-after:avoid} p,li{orphans:3;widows:3} table{width:100%;border-collapse:collapse;font-size:8.5pt;margin:5mm 0;table-layout:fixed} th,td{border:1px solid #b7c6d3;padding:5px;overflow-wrap:anywhere;vertical-align:top} th{background:#eaf0f5;text-align:left} tr{break-inside:avoid} pre{font-size:7.8pt;background:#f2f5f7;border:1px solid #d6e0e7;padding:8px;white-space:pre-wrap;overflow-wrap:anywhere;line-height:1.35} code{font-family:Consolas,monospace;font-size:.92em} a{color:#164d78;text-decoration:none;overflow-wrap:anywhere} figure{margin:6mm 0;break-inside:avoid} figure img{display:block;width:100%;max-height:225mm;object-fit:contain} figcaption{font-size:9pt;color:#4b5968;text-align:center}.annex{break-before:page}'''
manifest_path = root / 'docs/evidencias/relatorios-pdf.json'
previous = json.loads(manifest_path.read_text(encoding='utf-8-sig')) if manifest_path.is_file() else []
results = {entry['fonte']: entry for entry in previous}
selected = set(options.reports)
sources = [source for source in sorted(reports.glob('[0-9][0-9]-*.md')) if source.name[:2] in selected]
if {source.name[:2] for source in sources} != selected:
    parser.error('Não foi encontrado exatamente um Markdown para cada roteiro selecionado.')
preserved = {source.name: hashlib.sha256(source.read_bytes()).hexdigest()
             for source in reports.glob('[0-9][0-9]-*.pdf') if source.name[:2] not in selected}
for source in sources:
    text = source.read_text(encoding='utf-8')
    body = render(text, source)
    if source.name.startswith('01-'):
        annex = reports / 'anexos/01-Codigo.md'
        body += '<section class="annex">' + render(annex.read_text(encoding='utf-8'), annex) + '</section>'
    document = '<!doctype html><html lang="pt-BR"><meta charset="utf-8"><title>' + html.escape(source.stem) + '</title><style>' + style + '</style><body>' + body + '</body></html>'
    target_html = work / (source.stem + '.html')
    target_html.write_text(document, encoding='utf-8')
    target_pdf = work / (source.stem + '-' + uuid.uuid4().hex + '.pdf')
    command = [str(chrome), '--headless=new', '--disable-gpu', '--disable-background-networking', '--no-first-run', '--no-default-browser-check', '--allow-file-access-from-files', '--no-pdf-header-footer', '--user-data-dir=' + str(profile), '--print-to-pdf=' + str(target_pdf), target_html.as_uri()]
    run = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=60, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    if run.returncode:
        raise RuntimeError(source.name + ': Chrome exit ' + str(run.returncode) + '\n' + run.stderr.decode(errors='replace'))
    data = target_pdf.read_bytes()
    if not data.startswith(b'%PDF-') or len(data) < 1000:
        raise RuntimeError('PDF inválido: ' + str(target_pdf))
    pages = len(re.findall(rb'/Type\s*/Page\b', data))
    if pages < 1:
        raise RuntimeError('PDF sem páginas identificáveis: ' + source.name)
    final_pdf = reports / (source.stem + '.pdf')
    os.replace(target_pdf, final_pdf)
    results[source.name] = {'fonte': source.name, 'pdf': final_pdf.name, 'bytes': len(data), 'paginas': pages,
                            'sha256': hashlib.sha256(data).hexdigest(), 'fonteSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                            'instanteUtc': datetime.now(timezone.utc).isoformat(),
                            'renderer': 'Chrome headless local, HTML sem dependências externas; referências locais impressas como caminhos relativos'}
    print(source.name + ': ' + str(pages) + ' páginas, ' + str(len(data)) + ' bytes', flush=True)
for name, digest in preserved.items():
    if hashlib.sha256((reports / name).read_bytes()).hexdigest() != digest:
        raise RuntimeError('Um PDF não selecionado mudou durante a renderização: ' + name)
manifest_path.parent.mkdir(parents=True, exist_ok=True)
manifest_path.write_text(json.dumps([results[key] for key in sorted(results)], ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
