# Gera docs/FERRAMENTAS.md a partir dos atributos [Tool] do código.
# Uso: python3 tools/gerar_lista_ferramentas.py .
import re, glob, sys, os
root = sys.argv[1]
src = os.path.join(root, 'src', 'TeraTools.Revit')
modules = [("general","Geral","Painel, busca, configurações e ajuda"),("modeling","Modelagem","Alinhar, distribuir, matrizes e transformações"),
 ("selection","Seleção","Seleção, filtros e visibilidade"),("analysis","Análise","Colisões e medições"),
 ("generators","Geradores","Padrões, imagem, brises, quadros espaciais, cinético e temas"),("geometry","Geometria","Booleanas, partes, aberturas e sólidos"),
 ("floors","Pisos","Pisos, paginação, rodapés e revestimentos"),("interiors","Interiores","Ambientes, mobiliário, ergonomia, iluminação, forros e paredes"),
 ("joinery","Marcenaria","Módulos planejados, detalhamento e plano de corte"),("materials","Materiais","Biblioteca, aplicação e especificação de materiais"),
 ("docs","Documentação","Vistas, pranchas, anotações e exportação"),("quantities","Quantitativos","Tabelas, quantitativos e orçamento"),
 ("quality","Qualidade","Verificações e regras personalizadas"),("project","Projeto","Configurações, presets, bibliotecas e lote")]
const = {"General":"general","Modeling":"modeling","Selection":"selection","Analysis":"analysis","Generators":"generators","Geometry":"geometry","Floors":"floors",
 "Interiors":"interiors","Joinery":"joinery","Materials":"materials","Documentation":"docs","Quantities":"quantities","Quality":"quality","Project":"project"}
strre = r'"((?:[^"\\]|\\.)*)"'
def unesc(s): return s.encode('utf-8').decode('unicode_escape').encode('latin-1').decode('utf-8') if '\\' in s else s
tools = []
for f in glob.glob(os.path.join(src, 'Tools', '**', '*.cs'), recursive=True):
    text = open(f, encoding='utf-8').read()
    for m in re.finditer(r'\[Tool\((.*?)\)\]\s*public sealed class', text, re.S):
        body = m.group(1)
        head = re.match(r'\s*' + strre + r'\s*,\s*Modules\.(\w+)\s*,\s*' + strre + r'\s*,\s*' + strre, body, re.S)
        if not head: continue
        tid, mod, pt, en = head.group(1), head.group(2), head.group(3), head.group(4)
        def named(n):
            mm = re.search(n + r'\s*=\s*' + strre, body, re.S)
            return mm.group(1) if mm else ''
        order = re.search(r'Order\s*=\s*(\d+)', body)
        group = named('Group').split('|')[0]
        tools.append(dict(id=tid, module=const.get(mod, mod), name=pt, desc=named('DescPt'), help=named('HelpPt'), group=group,
                          order=int(order.group(1)) if order else 0))
def clean(s):
    s = s.replace('\\n', '\n').replace('\\"', '"').replace('\\\\', '\\')
    return s
out = ["# Téra Tools — referência das ferramentas", "",
       f"Lista gerada a partir do código-fonte: **{len(tools)} ferramentas** em {len(modules)} módulos. "
       "No Revit, cada ferramenta também tem uma página de ajuda (botão **Ajuda** ou tecla **F1** no diálogo, ou no painel Téra).", ""]
out.append("## Índice"); out.append("")
for mid, mname, mdesc in modules:
    n = len([t for t in tools if t['module'] == mid])
    out.append(f"- [{mname}](#{mname.lower().replace(' ', '-')}) — {mdesc} ({n})")
out.append("")
for mid, mname, mdesc in modules:
    ts = sorted([t for t in tools if t['module'] == mid], key=lambda t: (t['order'], t['name']))
    if not ts: continue
    out.append(f"## {mname}"); out.append(""); out.append(f"_{mdesc}_"); out.append("")
    for t in ts:
        g = f" · {t['group']}" if t['group'] else ""
        out.append(f"### {clean(t['name'])}")
        out.append(f"`{t['id']}`{g}"); out.append("")
        if t['desc']: out.append(clean(t['desc'])); out.append("")
        if t['help']:
            for para in clean(t['help']).split('\n'):
                if para.strip(): out.append("> " + para.strip()); out.append(">")
            if out[-1] == ">": out.pop()
            out.append("")
open(os.path.join(root, 'docs', 'FERRAMENTAS.md'), 'w', encoding='utf-8').write('\n'.join(out).rstrip() + '\n')
print(len(tools), "ferramentas")
for mid, mname, _ in modules: print(mname, len([t for t in tools if t['module'] == mid]))
