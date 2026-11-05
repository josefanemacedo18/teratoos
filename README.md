# Téra Tools para Revit 2027

Plugin para **arquitetura de interiores, marcenaria e documentação** no Autodesk Revit 2027, com geradores paramétricos inspirados no Pixy Tools. Ele cria **elementos BIM editáveis**: pisos, forros, paredes, famílias paramétricas e tabelas nativas do Revit. Nada fica "morto" no modelo.

- **84 ferramentas** em **11 módulos**, numa aba própria chamada **Téra Tools** e num painel lateral com busca
- Sem ferramentas repetidas: tudo o que o Revit já faz nativamente com a mesma precisão foi retirado (veja [docs/AUDITORIA.md](docs/AUDITORIA.md))
- **Pré-visualização 3D** nos geradores, na marcenaria, nos forros, na iluminação e nas paredes: você gira, aproxima e vê o resultado **antes** de aplicar
- Interface **100% em português** e **medidas sempre em milímetros**
- **Catálogo de acabamentos** do projeto (pisos, revestimentos, pinturas, rodapés, forros) com planilha CSV, e **marcenaria livre** para montar qualquer móvel por vãos
- Cada operação vira **um único Desfazer** (Ctrl+Z) e termina com um **relatório** que pode ser exportado para Excel, CSV ou HTML
- Presets reutilizáveis (pessoais, do projeto ou da equipe), histórico de operações e ajuda em cada ferramenta

> **Situação atual: versão 0.3.0.** Compilado contra a API oficial do Revit 2027; os testes automáticos do núcleo passam (199 testes). Esta versão corrige as causas de "cliquei em gerar e não gerou" (gerador de padrões, quadro espacial, pisos, paginação, forros) e acrescenta o catálogo de acabamentos, seis padrões de assentamento, a marcenaria livre e as referências da NBR 9050:2020. O plugin ainda **não foi executado dentro do Revit** neste ambiente de desenvolvimento: se aparecer algum erro, siga [Quando algo der errado](#quando-algo-der-errado).

---

## Instalação, passo a passo

Você não precisa saber programar. O GitHub compila o plugin sozinho a cada atualização e gera um arquivo `.zip` pronto para instalar.

### 1. Baixar o instalador

1. Entre no GitHub com a sua conta e abra o repositório **teratoos**.
2. Clique na aba **Actions**, no topo da página.
3. Na lista, clique na execução mais recente de **Build Téra Tools** que tenha o **símbolo verde ✅**.
4. Role a página até **Artifacts** e clique em **TeraTools-Revit2027**. O download de um arquivo `.zip` começa.

> Cada pacote fica disponível para download por 90 dias; cada atualização do código gera um novo. Quando houver versões oficiais publicadas, elas aparecerão em **Releases**, na página inicial do repositório, sem prazo.

### 2. Instalar

1. **Feche o Revit.**
2. Clique com o botão direito no `.zip` baixado e escolha **Extrair tudo…**.
3. Abra a pasta extraída e dê dois cliques em **`Instalar.bat`**.
   - Se o Windows mostrar "O Windows protegeu o computador", clique em **Mais informações** e depois em **Executar assim mesmo**.
4. Quando aparecer **"Pronto! Abra o Revit 2027…"**, pressione qualquer tecla para fechar a janela.

### 3. Primeira abertura

1. Abra o **Revit 2027**.
2. Aparece um aviso de segurança sobre o suplemento **Téra Tools**. Clique em **Carregar sempre**.
3. A aba **Téra Tools** aparece na faixa de opções.

### Atualizar ou remover

- **Atualizar:** baixe o `.zip` novo e repita a instalação. O instalador substitui a versão anterior, e suas configurações e presets são mantidos.
- **Remover:** dê dois cliques em **`Desinstalar.bat`**, com o Revit fechado.

---

## Primeiros passos

1. Clique em **Painel Téra**, o primeiro botão da aba. Ele abre um painel lateral com todas as ferramentas, uma **busca** (aceita palavras com ou sem acento), **favoritos** (estrela), **recentes** e **histórico**.
2. Quase todas as ferramentas funcionam do mesmo jeito:
   1. Selecione os elementos ou ambientes (ou selecione quando o plugin pedir).
   2. Ajuste as opções no diálogo. Muitos diálogos têm **pré-visualização em 2D e 3D** (arraste para girar, botão direito para mover, roda do mouse para aproximar, duplo clique para enquadrar), e você pode **salvar presets**.
   3. Confirme. Tudo é feito numa única operação: se não gostar, **Ctrl+Z** desfaz tudo de uma vez.
   4. Leia o **relatório**: ele mostra o que foi criado, avisos e tabelas, e pode **selecionar os elementos** citados ou **exportar para Excel**.
3. Nos campos de medida, um número sozinho é em **milímetros**. Você também pode escrever a unidade: `1,20 m` ou `120 cm`.
4. Para mudar os limites de desempenho e as pastas, use **Geral ▸ Configurações**.
5. Se uma ferramenta não conseguir criar algo, o relatório diz **quantos itens falharam e o motivo informado pelo Revit**. Se nada for criado, aparece uma mensagem com o motivo, em vez de terminar em silêncio.

**Recomendação:** nos primeiros usos, teste numa **cópia** do seu projeto.

O guia completo está em [docs/GUIA.md](docs/GUIA.md), e a lista de todas as ferramentas em [docs/FERRAMENTAS.md](docs/FERRAMENTAS.md).

---

## O que tem em cada módulo

| Módulo | Ferramentas | Destaques |
|---|---|---|
| Geral | 4 | Painel com busca, favoritos, recentes e histórico; configurações; ajuda; diagnóstico e pasta de logs |
| Modelagem | 5 | **Filtro avançado** (categoria, família, tipo, material, parâmetro, ambiente, nível), elementos do ambiente, **distribuir pelo ponto de inserção**, **girar cada elemento no lugar**, converter linhas/CAD em pisos e forros (com furos) |
| Geradores | 10 | **Gerador de padrões no estilo Pixy** (hexágono, Voronoi, estrela, círculos, losangos, triângulos, quadrados, ondas e tijolos, com fade e furos reais), imagem para blocos, brises, **quadro espacial** (sob a cobertura ou acima da base), painéis cinéticos, paletas, colorir por parâmetro, gerador de temas |
| Pisos | 9 | Floor Tracer, Splitter, Merger e Filler, faixas e soleiras, **paginação com 13 padrões** (alinhada, amarração, diagonal, espinha de peixe, **chevron**, trama, **hexágono**, **octógono com taco**, **opus romano**, **madeira com réguas aleatórias**, **losangos 3D** e modulares) e **tonalidades do catálogo**, rodapés, quantitativos |
| Interiores | 23 | Ambientes, mobiliário e layouts, **ergonomia e acessibilidade com referências da NBR 9050:2020** (circulação, manobra, portas, camas, lavatórios, bancadas, tomadas e interruptores), iluminação, forros e sancas, pintura, revestimentos, ripado e boiserie |
| Marcenaria | 8 | **Marcenaria livre** (qualquer móvel por vãos, com todo tipo de porta e gaveta, elevação clicável e 3D), módulos paramétricos, composição na parede, detalhamento, **plano de corte, ferragens, Excel/CSV e nesting** |
| Materiais | 7 | **Catálogo de acabamentos** no projeto (CSV), **aplicar acabamento do catálogo** nos ambientes, **quantitativo e custo dos acabamentos**, biblioteca pessoal, acabamentos por ambiente, substituir, ficha técnica |
| Documentação | 9 | Conjuntos de plantas, cortes e 3D por ambiente, pranchas automáticas, renumeração, cotas internas, legendas e notas |
| Quantitativos | 4 | Tabelas (inclusive de ambientes), levantamento em Excel, **orçamento com a sua tabela de preços**, comparação antes × depois |
| Qualidade | 2 | Verificação do modelo (inclusive geometria) com **regras editáveis em planilha** |
| Projeto | 3 | Presets e bibliotecas, transferência das configurações do Téra Tools entre projetos, processamento em lote |

O que foi feito de cada item do seu pedido original está em [docs/STATUS.md](docs/STATUS.md).

---

## Quando algo der errado

1. Quando ocorre um erro inesperado, o Téra Tools mostra uma **janela de erro**. Clique em **Copiar detalhes**.
2. Cole o texto na conversa com o Claude e conte, em uma frase, o que você estava fazendo (por exemplo: "rodei Paginação de revestimento num piso de 3×4 m").
3. Se o Revit fechar ou travar, os registros ficam em `%AppData%\TeraTools\logs`. Em **Geral ▸ Diagnóstico ▸ Abrir a pasta de dados e logs** você chega lá direto. Envie o arquivo mais recente.

Mensagens em português que explicam o que fazer (por exemplo, "Escolha o tipo de forro") **não são erros do plugin**: são orientações.

---

## Limites importantes

Estes limites são intencionais, para não prometer o que o Revit não garante:

- **Furos do Gerador de padrões** usam vazios de família, o mesmo mecanismo de um vazio no Editor de famílias. Só funcionam em elementos que o Revit deixa recortar por vazio (em geral paredes, pisos, forros e coberturas). O plugin avisa quando o elemento não aceita; nesse caso, use a saída em relevo.
- **Unir e preencher pisos** usam operações booleanas do Revit; contornos muito complexos podem ser recusados, e o plugin informa quais.
- **Ergonomia e acessibilidade** usam valores de referência **editáveis** que você define. O relatório aponta itens para revisão e **nunca declara conformidade** com normas.
- **Nesting de chapas** é um otimizador próprio, bom para orçamento e planejamento. Confira com o fornecedor. Arquivos de **CNC** específicos de cada máquina **não** são gerados: há um CSV genérico de fabricação, que você ajusta ao seu software.
- **Iluminação pelo método dos lúmens** é um pré-dimensionamento. O cálculo luminotécnico final deve ser feito em software próprio.
- **Painéis cinéticos** têm estados paramétricos (abrir e fechar por regra). Animação em tempo real e simulação física são escopos separados, como você mesma previu.
- Os **recursos avançados da seção 13** do pedido ficam para depois da estabilização dos módulos principais.
- Compatível com o **Revit 2027**. Outras versões exigem uma compilação específica.

---

## Para quem for programar

Veja [docs/DESENVOLVIMENTO.md](docs/DESENVOLVIMENTO.md): arquitetura, como compilar e como criar novas ferramentas.
