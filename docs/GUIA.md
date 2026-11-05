# Guia de uso do Téra Tools

Este guia explica como o plugin funciona no dia a dia. A instalação está no [README](../README.md), e a descrição de cada ferramenta em [FERRAMENTAS.md](FERRAMENTAS.md).

---

## 1. Onde ficam as coisas

### Aba Téra Tools

Na faixa de opções do Revit, a aba **Téra Tools** tem um painel por módulo: Geral, Modelagem, Seleção, Análise, Geradores, Geometria, Pisos, Interiores, Marcenaria, Materiais, Documentação, Quantitativos, Qualidade e Projeto. Ferramentas relacionadas ficam agrupadas em menus suspensos para economizar espaço.

### Painel Téra (lateral)

É o jeito mais rápido de trabalhar:

- **Busca:** digite parte do nome ou uma palavra-chave (por exemplo `rodape`, `sanca`, `pdf`). Acentos são opcionais. **Enter** executa a primeira ferramenta da lista.
- **Favoritos:** clique na estrela de uma ferramenta.
- **Recentes:** as últimas ferramentas usadas.
- **Histórico:** as últimas operações, com data e resultado.

O painel abre sozinho ao iniciar o Revit. Isso pode ser desligado nas configurações.

### Pastas do plugin

Tudo fica em `%AppData%\TeraTools` (use **Geral ▸ Diagnóstico ▸ Abrir a pasta de dados e logs**):

| Pasta/arquivo | Conteúdo |
|---|---|
| `settings.json` | Suas configurações |
| `presets\` | Seus presets, um arquivo por ferramenta |
| `bibliotecas\` | Materiais (`materiais.json`), notas padrão (`notas.json`), regras de verificação |
| `exportacoes\` | Planilhas, PDFs, DWGs e relatórios gerados |
| `familias\2027\` | Famílias paramétricas geradas pelo plugin (TT_Bloco, TT_Chapa) |
| `logs\` | Registros para diagnóstico de erros |
| `historico.jsonl` | Histórico de operações |

---

## 2. Como uma ferramenta funciona

1. **Seleção:** se já houver elementos válidos selecionados, a ferramenta usa a seleção. Se não, ela pede para você clicar. Para terminar uma seleção múltipla, use **Concluir** na barra de opções do Revit.
2. **Diálogo de opções:**
   - Os campos são agrupados por assunto. Alguns só aparecem quando fazem sentido; por exemplo, "Espaçamento" só aparece no modo "Por espaçamento".
   - **Medidas:** um número sozinho é em **milímetros**. Você também pode escrever a unidade (`1,20 m`, `120 cm`).
   - **Pré-visualização 2D e 3D:** muitos diálogos mostram o resultado, atualizado enquanto você muda as opções, **antes de qualquer alteração no modelo**. Na aba **3D**: arraste com o botão esquerdo para girar, com o botão direito (ou do meio) para mover, use a roda do mouse para aproximar e dê **duplo clique** para enquadrar tudo. A aba **2D** mostra a planta ou a vista frontal com as medidas.
   - **Presets:** salve as opções com um nome para reutilizar. Um preset pode ser **pessoal** (só no seu computador), **do projeto** (fica dentro do .rvt e vai junto com o arquivo) ou de **biblioteca da equipe** (somente leitura, numa pasta compartilhada).
   - O plugin lembra as **últimas opções** que você usou em cada ferramenta.
3. **Execução:** tudo vira **um único Desfazer**. Avisos do Revit que apareceriam dezenas de vezes são recolhidos e listados no relatório.
4. **Relatório:** mostra contagens (criados, modificados, ignorados), avisos, erros e tabelas. Itens que o Revit recusou aparecem agrupados, com a quantidade e o motivo. Se nada for criado, a ferramenta para com uma mensagem explicando por quê. Nele você pode:
   - **selecionar no modelo** os elementos de uma linha;
   - **exportar** para Excel, CSV ou HTML;
   - **copiar** o texto.

### Proteções de desempenho

Antes de criar muitos elementos, o plugin pede confirmação (padrão: acima de 2.000). Ele recusa operações acima de um limite rígido (padrão: 30.000). Os dois valores podem ser alterados em **Configurações**.

---

## 3. Sistemas gerados e famílias do plugin

### Famílias TT_Bloco e TT_Chapa

Os geradores, o ripado, a boiserie e a marcenaria criam peças com duas famílias paramétricas que o próprio plugin gera e carrega no projeto:

- **TT_Bloco** (Modelo genérico): `TT_Largura`, `TT_Profundidade`, `TT_Altura` e `TT_Material`.
- **TT_Chapa** (Marcenaria): os mesmos parâmetros, mais os dados de corte: `TT_Peca`, `TT_Modulo`, `TT_Comprimento`, `TT_Espessura`, fitas de borda (`TT_FitaL1`, `TT_FitaL2`, `TT_FitaC1`, `TT_FitaC2`) e `TT_Veio`.

São famílias normais do Revit: dá para mover, copiar, mudar medidas e tabelar. Se a criação da família falhar em algum computador, o plugin gera **forma livre** (DirectShape) e avisa no relatório.

### Editar depois

Padrões, mosaicos, brises, quadros espaciais, painéis cinéticos, ripados, boiseries, módulos de marcenaria e as **colunas, coberturas e lâminas orgânicas** ficam **registrados no projeto** com as opções usadas. Para alterar:

- **Geradores ▸ Editar sistema gerado:** selecione uma peça. As opções originais reabrem e o sistema é refeito no mesmo lugar.
- **Marcenaria ▸ Editar módulo:** faz o mesmo para módulos de marcenaria.

### Versões anteriores

Cada vez que um sistema é refeito, as opções anteriores ficam guardadas (até 20 versões). Em **Editar sistema gerado**, escolha **Voltar a uma versão anterior…**, confira as opções daquela data e regenere. Se o sistema foi criado por uma versão mais antiga do gerador, o plugin avisa antes de refazer (o resultado pode mudar um pouco).

### Parâmetros compartilhados TT_*

O plugin cria e vincula sozinho os parâmetros `TT_` de que precisa: código, ambiente, grupo, sistema, status e outros. Use-os em tabelas e filtros como qualquer parâmetro do Revit.

---

## 4. Fluxos de trabalho sugeridos

### Projeto de interiores, do zero

1. Crie os ambientes com o comando nativo do Revit (**Arquitetura ▸ Ambiente ▸ Colocar ambientes automaticamente**). Depois use **Interiores ▸ Numerar ambientes** e **Padronizar nomes e códigos**.
2. **Materiais ▸ Biblioteca de materiais:** crie no projeto os materiais que você usa.
3. **Materiais ▸ Acabamentos por ambiente:** regras como "Banh* → porcelanato" preenchem piso, parede, forro e rodapé de cada ambiente.
4. **Pisos:** use **Piso por ambiente (Floor Tracer)**, depois **Paginação de revestimento** (peças reais com juntas e ponto de partida) e **Rodapés automáticos**. **Unir pisos (Floor Merger)** junta pisos vizinhos num só, **Dividir piso (Floor Splitter)** separa regiões, e **Preencher pisos (Floor Filler)** cria piso só onde o ambiente ainda não tem.
5. **Interiores ▸ Forros:** **Forro por ambiente**, ou **Sanca e rebaixo** para forros com rebaixo perimetral e fenda de luz.
6. **Interiores ▸ Paredes:** **Revestimento de parede** (barrados e revestimentos que recortam portas e janelas), **Ripado** e **Boiserie**.
7. **Interiores ▸ Mobiliário:** insira os móveis com o comando nativo **Componente (CM)**; depois use **Distribuir ao longo de parede/linha**, **Encostar na parede** e **Layouts salvos**, e confira com **Áreas de uso**.
8. **Interiores ▸ Ergonomia e acessibilidade:** ajuste as regras de referência para o seu projeto.
9. **Interiores ▸ Iluminação:** use **Luminárias em grade** (por espaçamento ou lux estimado), **Sobre mobiliário** e **Grupos e circuitos**.
10. **Documentação:** use **Conjunto de plantas**, **Vistas por ambiente** (planta e quatro elevações), **Montar pranchas** e **Cotas internas dos ambientes**. Para etiquetar e exportar PDF/DWG, use os comandos nativos (**Etiquetar todos não etiquetados**, **Exportar ▸ PDF**).
11. **Quantitativos:** use **Levantamento** e **Orçamento**.
12. **Qualidade ▸ Verificação de qualidade:** rode antes de cada entrega.

### Catálogo de acabamentos

1. **Materiais ▸ Catálogo de acabamentos:** cadastre cada acabamento (ou importe a planilha CSV). Cada item tem código, nome, tipo, aplicação (piso, parede, forro, rodapé, bancada, marcenaria), fornecedor, linha, cor, medidas da peça, junta e cor da junta, paginação, tonalidades, perda, preço e caixas. A pré-visualização mostra uma amostra de 2,40 × 2,40 m.
2. **Salvar no projeto** cria ou atualiza no Revit o material (com a hachura das juntas), os materiais das tonalidades e o tipo "TT código - nome" de piso, parede fina, rodapé ou forro.
3. **Materiais ▸ Aplicar acabamento do catálogo:** escolha o item e os ambientes. Piso → cria o piso e, se quiser, pagina com as peças e as tonalidades. Parede → pinta as faces ou cria o revestimento. Forro e rodapé → criam o elemento do tipo do item.
4. **Materiais ▸ Quantitativo e custo dos acabamentos:** área ou comprimento no modelo, perda, peças, caixas e custo de cada item.
5. Para levar o catálogo a outro projeto: **Exportar planilha** aqui e **Importar planilha** lá.

### Marcenaria livre (qualquer móvel)

1. **Marcenaria ▸ Marcenaria livre:** escolha um modelo (ou a caixa vazia) e ajuste **Caixa, base e materiais**.
2. Clique num vão da elevação e **divida** em colunas ou linhas. Cada vão pode ter medida fixa (mm) ou automática.
3. No vão final, escolha **prateleiras**, **cabideiro** ou **gavetas** (aparentes ou internas).
4. Escolha a **frente**: porta de abrir (esquerda/direita), duas portas, basculante, escamoteável, de correr ou camarão, com ou sem vidro. **Clique de novo** num vão para escolher o grupo em volta dele: uma frente no grupo cobre todos os vãos (por exemplo, portas de correr no roupeiro inteiro).
5. **Posicionar no projeto:** as peças viram famílias TT_Chapa num conjunto. Para mudar depois, use **Marcenaria ▸ Editar módulo**. O plano de corte, as ferragens e o nesting funcionam igual aos módulos.

### Marcenaria planejada

1. **Regras de marcenaria:** defina dobradiças por altura, corrediças e folgas. As regras ficam salvas no projeto.
2. **Módulo de marcenaria** para peças avulsas, ou **Compor módulos na parede** para dividir uma parede em módulos equilibrados, com complemento e tampo contínuo.
3. **Editar módulo** para mudar medidas, frentes, gavetas e materiais.
4. **Detalhamento do módulo:** frontal, lateral, planta, corte, 3D, 3D explodida, lista de peças e prancha montada.
5. **Plano de corte e ferragens:** lista de peças com medidas de corte, fitas e veio; consumo de chapas e fitas; ferragens; Excel e CSV de fabricação.
6. **Otimizar chapas (nesting):** distribui as peças nas chapas e desenha os planos de corte.

### Colunas orgânicas

1. **Orgânicos ▸ Colunas orgânicas:** escolha um estilo (cilíndrica, leque de nervuras, treliça cônica, cogumelo vazado, trompete com capitel prato, pétalas/tulipa, canelada ondulada, lamelas radiais, torcida lobada, ampulheta, inclinada em S, estrela) ou **Minhas últimas opções**.
2. Ajuste no diálogo, com a pré-visualização 3D:
   - **Silhueta** (como o diâmetro varia na altura): cilindro, cone, trompete, barriga, ampulheta, cogumelo, **fórmula** ou **desenhada**.
   - **Seção**: círculo, polígono, lobada, estrela, elipse ou **desenhada**.
   - **Eixo**: reto, inclinado, arqueado, em S ou **desenhado**.
   - **Torção, caneluras e ondas** (inclusive ondas em espiral).
   - **Construção**: cheia, oca (casca), nervuras, treliça em hélices, grelha, lâminas radiais em volta de um núcleo ou pétalas.
   - **Base e capitel**: disco, placa quadrada ou prato.
3. Formas desenhadas: depois do OK, o plugin pede as linhas — **seção** = contorno fechado em planta; **silhueta** = meio perfil numa elevação ou corte, e um clique no eixo; **eixo** = linha 3D de baixo para cima. O diálogo reabre para você conferir.
4. Clique nos pontos onde ficam as colunas (Esc para terminar). Com **Até o nível acima**, cada coluna vai até o próximo nível.
5. O relatório traz altura, volume, área, comprimento de barras, peso (pela densidade escolhida) e uma esbeltez λ indicativa.

### Coberturas orgânicas

1. **Orgânicos ▸ Coberturas orgânicas:** escolha um estilo (almofada sobre árvores, guarda-chuvas invertidos em funil, guarda-chuvas, membrana tensionada, abóbada envidraçada, sela, ondas com lâminas, plana inclinada, bolhas).
2. Indique a planta: retângulo, contorno de piso/cobertura/ambiente, linhas desenhadas (contorno fechado, inclusive curvo ou côncavo) ou círculo.
3. Ajuste a **superfície** (altura do beiral, quanto sobe, direção), a **pele** (contínua, painéis com juntas, lâminas ou sem pele), a **estrutura** (grelha, diagrid ou radial por apoio, com viga de borda) e os **apoios** (árvores com um ou dois níveis de galhos, ou pilares retos, numa grade automática ou nos pontos que você clicar).
4. **Ligar a colunas existentes** (opcional): selecione as colunas — inclusive as Colunas orgânicas — e escolha ligar no topo, num ponto intermediário (galhos saindo da coluna a uma porcentagem da altura) ou nos dois. A cobertura continua independente.
5. O relatório traz área coberta e área real da pele, painéis, metros de estrutura e de galhos, alturas, altura livre mínima e os **pontos onde a água pode empoçar**.

### Lâminas de fachada

1. **Orgânicos ▸ Lâminas de fachada:** escolha um estilo (cobre em onda com arco de entrada, aletas torcidas, torção em onda, abertura por atrator, aerofólios aleatórios, tubos com topo em onda, profundidade em onda, losangos em degradê).
2. Escolha a parede (as lâminas ficam do lado externo) ou as linhas do caminho em planta, retas ou curvas.
3. Ajuste a **seção** (chapa, tubo, losango, elipse, gota), o **giro ao longo da fachada** (onda, linear, aleatório, atrator ou fórmula com u, h, i, n, x, z), a **torção** da base ao topo, os **recortes** da base e do topo (inclinado, onda, **arco de entrada** ou linha desenhada numa elevação), as **faixas horizontais** e os **conjuntos de material**.
4. **Estado de abertura (cinético):** de 0% (todas no ângulo base) a 100% (variação completa). Gere estados diferentes para estudos e renders.
5. Cada lâmina vira uma forma livre numerada. O relatório traz a **lista de corte** (comprimentos iguais agrupados) e a rotação de cada lâmina.

### Geradores paramétricos

- **Gerador de padrões (estilo Pixy):** clique numa face plana ou curva (parede, piso, forro ou cobertura) e escolha o desenho:
  - **Padrões:** hexágono, Voronoi (células irregulares), estrela (Star Burst), círculos, losangos, triângulos, quadrados, ondas e tijolos.
  - **Saída:** **furos reais** (vazios que recortam o elemento, como um cobogó ou chapa perfurada) ou **peças em relevo** (sólidos sobre a face).
  - **Controle:** tamanho da célula, proporção de abertura (quanto de cada célula vira furo), escala, rotação, pontas e raio interno da estrela, amplitude e comprimento das ondas.
  - **Fade:** o tamanho dos furos diminui ao longo da face, de forma linear, radial (a partir de um centro) ou por ruído. A espessura das peças pode acompanhar o fade.
  - **Aleatório:** variação de tamanho, rotação, posição e falhas, com semente para repetir o mesmo resultado.
  - O resultado é **uma família** "TT Padrão …" no projeto, com parâmetro de **Material**. Para mudar, use **Editar sistema gerado**.
- **Imagem para blocos:** transforma uma imagem em mosaico de peças, com relevo pela tonalidade e cores convertidas em materiais.
- **Aletas e brises:** ao longo de parede ou linha, com rotação variável (linear, onda, aleatória, atrator ou fórmula) e interrupção nas janelas.
- **Quadro espacial:** malhas quadrado sobre quadrado (deslocada ou alinhada) e triangular, de 2 a 6 camadas, com nós esféricos e quantitativo de barras.
- **Painéis cinéticos:** painéis com estado de abertura por regra. Para mudar o estado, use **Editar sistema gerado**.
- **Temas:** o **Gerador de temas** aplica um visual completo (claro, croqui, maquete branca, monocromático, realista, técnico ou personalizado) à vista ativa, a vistas escolhidas ou a cópias delas. Também há **Paleta de cores** e **Colorir por parâmetro** (filtros e legenda).

---

### Referências visuais e autoteste

- **Projeto ▸ Referências visuais:** guarde dentro do .rvt imagens de referência (moodboard, inspirações, fotos de obra, croquis) com título, notas e etiquetas. Adicione arquivos ou cole com **Ctrl+V**. Com um elemento gerado selecionado, a janela mostra as referências daquele sistema e as novas já ficam ligadas a ele.
- **Geral ▸ Autoteste do plugin:** depois de instalar ou atualizar, ou quando algo "não gerar nada". Testa as ferramentas num cenário temporário e desfaz tudo; o modelo não muda.

---

## 5. Bibliotecas e trabalho em equipe

- **Materiais:** exporte a biblioteca para CSV, edite no Excel e importe de volta.
- **Notas padrão:** insira blocos de notas numeradas ou salve textos do projeto na biblioteca.
- **Regras de verificação:** exporte para planilha, edite e importe. As regras ficam no projeto.
- **Presets:** **Projeto ▸ Presets e bibliotecas** lista, exporta, importa e copia presets.
- **Pasta da equipe:** em **Configurações**, aponte a "pasta compartilhada de bibliotecas" para uma pasta de rede ou do OneDrive. Os presets e bibliotecas dessa pasta aparecem para todos, somente para leitura.
- **Transferir padrões entre projetos:** copia modelos de vista, filtros, materiais, tipos, tabelas e as configurações do Téra Tools de outro projeto aberto.

---

## 6. Configurações gerais (Geral ▸ Configurações)

| Opção | O que faz |
|---|---|
| Idioma | Automático (segue o Revit), Português ou Inglês. Vale após reiniciar o Revit. |
| Aviso de quantidade | Pede confirmação acima de N elementos criados. |
| Limite rígido | Recusa operações acima de N elementos. |
| Mostrar relatório | Abre o relatório ao final quando houver avisos, erros ou tabelas. |
| Selecionar resultados | Seleciona no modelo o que foi criado. |
| Lembrar opções | Reabre cada ferramenta com as últimas opções usadas. |
| Pasta de templates de família | Use se os templates (.rft) do Revit estiverem em outra pasta. |
| Pasta compartilhada de bibliotecas | Bibliotecas e presets da equipe. |
| Separador do CSV | Automático, ponto e vírgula, vírgula ou tabulação. |
| Painel ao iniciar | Abre o Painel Téra quando o Revit abre. |
| Log detalhado | Grava mais detalhes técnicos, útil quando for relatar um erro. |

---

## 7. Dicas

- Selecione **antes** de clicar na ferramenta: ela usa a seleção atual.
- Se o resultado não ficou como esperado, **Ctrl+Z** e rode de novo: as últimas opções ficam lembradas.
- Ferramentas que só leem o modelo (relatórios, verificações, quantitativos) **não alteram nada**.
- Em modelos grandes, comece por um nível ou alguns ambientes para testar as opções.
