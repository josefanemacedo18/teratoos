# Téra Tools para Revit 2027

Plugin para **arquitetura de interiores, marcenaria e documentação** no Autodesk Revit 2027, com geradores paramétricos inspirados no Pixy Tools. Ele cria **elementos BIM editáveis**: pisos, forros, paredes, famílias paramétricas e tabelas nativas do Revit. Nada fica "morto" no modelo.

- **124 ferramentas** em **14 módulos**, numa aba própria chamada **Téra Tools** e num painel lateral com busca
- Português e inglês
- Cada operação vira **um único Desfazer** (Ctrl+Z) e termina com um **relatório** que pode ser exportado para Excel, CSV ou HTML
- Presets reutilizáveis (pessoais, do projeto ou da equipe), histórico de operações e ajuda em cada ferramenta

> **Situação atual: versão 0.1.0, primeira versão completa.** O código foi compilado contra a API oficial do Revit 2027, e os testes automáticos do núcleo passam (102 testes). Ainda **não foi executado dentro do Revit**: o ambiente onde ele foi desenvolvido não tem o Revit. Nos primeiros usos podem aparecer erros. Quando aparecer um, siga as instruções de [Quando algo der errado](#quando-algo-der-errado) para que ele seja corrigido.

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
   2. Ajuste as opções no diálogo. Muitos diálogos têm **pré-visualização**, e você pode **salvar presets**.
   3. Confirme. Tudo é feito numa única operação: se não gostar, **Ctrl+Z** desfaz tudo de uma vez.
   4. Leia o **relatório**: ele mostra o que foi criado, avisos e tabelas, e pode **selecionar os elementos** citados ou **exportar para Excel**.
3. Nos campos de medida, um número sozinho usa a unidade do projeto. Você também pode escrever a unidade: `1,20 m`, `120 cm` ou `1200 mm`.
4. Para mudar o idioma, os limites de desempenho e as pastas, use **Geral ▸ Configurações**.

**Recomendação:** nos primeiros usos, teste numa **cópia** do seu projeto.

O guia completo está em [docs/GUIA.md](docs/GUIA.md), e a lista de todas as ferramentas em [docs/FERRAMENTAS.md](docs/FERRAMENTAS.md).

---

## O que tem em cada módulo

| Módulo | Ferramentas | Destaques |
|---|---|---|
| Geral | 6 | Painel com busca, favoritos e histórico; configurações; ajuda; diagnóstico |
| Modelagem | 13 | Alinhar, distribuir, matrizes linear e radial, copiar, girar, espelhar, elevação, redimensionar, agrupar, substituir tipo mantendo parâmetros |
| Seleção | 6 | Filtro avançado (categoria, família, tipo, material, parâmetro, ambiente, nível), semelhantes, isolar, ocultar e revelar |
| Análise | 3 | Verificação de colisões, medições |
| Geradores | 11 | Padrões paramétricos, imagem para blocos, brises, quadro espacial, painéis cinéticos, paletas, colorir por parâmetro, estilos de apresentação |
| Geometria | 12 | Unir e recortar, booleanas, partes, aberturas, sólido por perfil, espessura em face, converter linhas/CAD, verificar geometria |
| Pisos | 8 | Piso por ambiente, regiões, faixas e soleiras, **paginação com juntas e ponto de partida**, rodapés, quantitativos de peças e caixas |
| Interiores | 27 | Ambientes (criar, numerar, códigos, planilha, vistas), mobiliário e layouts, **ergonomia e acessibilidade com regras editáveis**, iluminação, forros e sancas, pintura, revestimentos, ripado e boiserie |
| Marcenaria | 7 | **Módulos paramétricos** (armários, aéreos, torres, nichos, roupeiros, gabinetes, ilhas), composição na parede, detalhamento, **plano de corte, ferragens, Excel/CSV e nesting** |
| Materiais | 6 | Biblioteca pessoal, aplicar, acabamentos por ambiente, substituir, ficha técnica, relatório de uso |
| Documentação | 15 | Conjuntos de plantas, cortes e 3D por ambiente, pranchas automáticas, carimbo, índice, **PDF/DWG**, cotas, etiquetas, legendas e notas |
| Quantitativos | 4 | Tabelas, levantamento em Excel, **orçamento com a sua tabela de preços**, comparação antes × depois |
| Qualidade | 2 | Verificação do modelo com **regras editáveis em planilha** |
| Projeto | 4 | Configurações do projeto, presets, transferência de padrões entre projetos, processamento em lote |

O que foi feito de cada item do seu pedido original está em [docs/STATUS.md](docs/STATUS.md).

---

## Quando algo der errado

1. Quando ocorre um erro inesperado, o Téra Tools mostra uma **janela de erro**. Clique em **Copiar detalhes**.
2. Cole o texto na conversa com o Claude e conte, em uma frase, o que você estava fazendo (por exemplo: "rodei Paginação de revestimento num piso de 3×4 m").
3. Se o Revit fechar ou travar, os registros ficam em `%AppData%\TeraTools\logs`. Em **Geral ▸ Abrir pasta de dados** você chega lá direto. Envie o arquivo mais recente.

Mensagens em português que explicam o que fazer (por exemplo, "Escolha o tipo de forro") **não são erros do plugin**: são orientações.

---

## Limites importantes

Estes limites são intencionais, para não prometer o que o Revit não garante:

- **Booleanas e modificações de faces** dependem do tipo de elemento. O plugin avisa quando um elemento não aceita a operação, em vez de falhar em silêncio.
- **Ergonomia e acessibilidade** usam valores de referência **editáveis** que você define. O relatório aponta itens para revisão e **nunca declara conformidade** com normas.
- **Nesting de chapas** é um otimizador próprio, bom para orçamento e planejamento. Confira com o fornecedor. Arquivos de **CNC** específicos de cada máquina **não** são gerados: há um CSV genérico de fabricação, que você ajusta ao seu software.
- **Iluminação pelo método dos lúmens** é um pré-dimensionamento. O cálculo luminotécnico final deve ser feito em software próprio.
- **Painéis cinéticos** têm estados paramétricos (abrir e fechar por regra). Animação em tempo real e simulação física são escopos separados, como você mesma previu.
- Os **recursos avançados da seção 13** do pedido ficam para depois da estabilização dos módulos principais.
- Compatível com o **Revit 2027**. Outras versões exigem uma compilação específica.

---

## Para quem for programar

Veja [docs/DESENVOLVIMENTO.md](docs/DESENVOLVIMENTO.md): arquitetura, como compilar e como criar novas ferramentas.
