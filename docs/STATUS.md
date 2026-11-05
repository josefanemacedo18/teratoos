# O que foi feito do pedido original

Legenda: ✅ implementado · 🟡 implementado com limites (explicados) · ➖ retirado porque o Revit já faz nativamente · ⏳ planejado para depois

Lembrete: tudo foi compilado contra a API do Revit 2027, mas **ainda não foi executado dentro do Revit**. Os primeiros testes reais podem pedir ajustes.

Na versão 0.2.0, cada ferramenta foi auditada ([AUDITORIA.md](AUDITORIA.md)); a versão 0.3.0 corrigiu as falhas de geração e acrescentou catálogo, padrões de piso, marcenaria livre e NBR 9050; a versão 0.4.0 trouxe o módulo Orgânicos, a biblioteca com texturas, puxadores e verificação de sobreposições na marcenaria, autoteste, referências visuais e histórico de versões. As que repetiam um comando nativo do Revit com a mesma precisão foram retiradas, e o item correspondente aparece aqui como ➖, com o comando nativo que faz o mesmo.

## 1. Interface e gerenciamento do plugin

| Item | Situação | Onde |
|---|---|---|
| Aba própria na faixa de opções | ✅ | Aba **Téra Tools**, com um painel por módulo |
| Painel lateral (dockable) | ✅ | **Geral ▸ Painel Téra** |
| Organização por módulos e categorias | ✅ | 11 módulos; menus agrupados por assunto |
| Ícones próprios | ✅ | Ícones vetoriais desenhados para cada ferramenta, adaptados ao tema claro/escuro do Revit |
| Busca de ferramentas | ✅ | Painel Téra (com ou sem acento, por nome ou palavra-chave) |
| Favoritos e recentes | ✅ | Painel Téra |
| Configurações globais | ✅ | **Geral ▸ Configurações** |
| Presets reutilizáveis | ✅ | Em todos os diálogos de opções: pessoal, do projeto ou da equipe |
| Salvar e carregar configurações | ✅ | **Projeto ▸ Presets e bibliotecas** (exportar e importar arquivos) |
| Pré-visualização antes de aplicar | ✅ | Abas **3D** e **2D** nos diálogos dos geradores, marcenaria, forros, iluminação, ripado, boiserie e distribuição |
| Mensagens de erro claras e relatórios de execução | ✅ | Relatório ao final de cada operação, com a quantidade de itens que falharam e o motivo informado pelo Revit; aviso quando nada é criado; janela de erro com "Copiar detalhes" |
| Proteção contra fechamento do Revit | ✅ | Erros do plugin na interface são capturados e mostrados, em vez de derrubar o Revit |
| Histórico de operações | ✅ | Aba **Histórico** do Painel Téra |
| Idioma | ✅ | Interface 100% em português; medidas sempre em mm |
| Ajuda contextual | ✅ | Página de ajuda por ferramenta (botão **Ajuda** ou **F1**) e **Geral ▸ Ajuda** |

## 2. Ferramentas gerais de modelagem

| Item | Situação | Onde |
|---|---|---|
| Distribuir | ✅ | Modelagem ▸ Distribuir. Mede pelo **ponto de inserção** (exato) ou pela geometria real; ignora elementos fixados; mostra antes e depois |
| Rotacionar | ✅ | Modelagem ▸ Rotacionar. Gira **cada elemento no próprio ponto de inserção**, o que o Revit não faz em lote |
| Selecionar por categoria, família, tipo, material, parâmetro, ambiente ou nível | ✅ | Modelagem ▸ Filtro avançado, Elementos do ambiente |
| Alinhar | ➖ | Nativo: **Alinhar (AL)**, que usa as referências reais e é exato |
| Matrizes linear e radial | ➖ | Nativo: **Matriz (AR)**, associativa e editável |
| Copiar, espelhar, elevação, redimensionar | ➖ | Nativos: **Copiar (CO)**, **Espelhar (MM/DM)** e edição múltipla nas **Propriedades** |
| Agrupar, desagrupar, substituir tipo | ➖ | Nativos: **Agrupar (GP)** e **Seletor de tipo** |
| Isolar, ocultar, revelar, selecionar semelhantes | ➖ | Nativos: **HI/EH/VH**, **Revelar elementos ocultos** e **Selecionar todas as instâncias (SA)** |
| Verificação de colisões e medições | ➖ | Nativos: **Colaborar ▸ Verificação de interferência** e **Medir** |
| Pontos e linhas de referência | ➖ | Nativo: **Plano de referência** |

## 3. Geradores paramétricos (inspirados no Pixy Tools)

| Item | Situação | Onde |
|---|---|---|
| Padrões paramétricos | ✅ | Geradores ▸ Gerador de padrões: **hexágono, Voronoi, estrela (Star Burst), círculos, losangos, triângulos, quadrados, ondas e tijolos**; furos reais (vazio) ou peças em relevo (sólido); proporção de abertura; **fade** linear, radial ou por ruído; variação aleatória de tamanho, rotação, posição e falhas; espessura que acompanha o fade |
| Imagem para blocos | ✅ | Geradores ▸ Imagem para blocos (relevo e cores em materiais) |
| Aletas e brises | ✅ | Geradores ▸ Aletas e brises |
| Quadros espaciais | ✅ | Geradores ▸ Quadro espacial (embaixo da cobertura escolhida ou acima da base, abre a vista 3D criada; quadrado sobre quadrado deslocado ou alinhado e triangular, de 2 a 6 camadas, nós esféricos, barras como forma livre, família TT_Bloco ou vigas estruturais do projeto; quantitativo de barras por comprimento) |
| Cinético | 🟡 | Painéis cinéticos e **Orgânicos ▸ Lâminas de fachada** (giro em onda/atrator/fórmula, torção, arco de entrada, faixas) com **estado de abertura de 0 a 100%**, editados por **Editar sistema gerado**. Animação em tempo real e simulação física são escopos separados, como previsto no pedido |
| Colunas orgânicas | ✅ | Orgânicos ▸ Colunas orgânicas: silhueta, seção e eixo por parâmetros ou desenhados; torção, caneluras e ondas; cheia, oca, nervuras, treliça, grelha, lâminas e pétalas; base e capitel; quantitativos e esbeltez indicativa |
| Coberturas orgânicas | ✅ | Orgânicos ▸ Coberturas orgânicas: 9 superfícies, pele contínua/painéis/lâminas, estrutura em grelha, diagrid ou radial, apoios em árvore e ligação opcional a colunas no topo e em ponto intermediário; empoçamento e altura livre |
| Versões dos sistemas | ✅ | Editar sistema gerado ▸ Voltar a uma versão anterior (até 20 versões por sistema) |
| Temas e acabamentos visuais | ✅ | **Gerador de temas** (claro, croqui, maquete branca, monocromático, realista, técnico ou personalizado, aplicado à vista ativa, a vistas escolhidas ou a cópias), Paleta de cores, Colorir por parâmetro (filtros e legenda) |
| Resultado como elemento BIM editável | ✅ | Famílias paramétricas TT_Bloco/TT_Chapa, família de padrão própria e **Editar sistema gerado** |

## 4. Modificadores geométricos

| Item | Situação | Onde |
|---|---|---|
| Converter linhas/CAD em elementos | ✅ | Modelagem ▸ Converter linhas/CAD em elementos |
| Validar geometria | ✅ | Qualidade ▸ Verificação de qualidade (opção **Geometria**) |
| Unir, desunir e recortar | ➖ | Nativos: **Unir geometria** e **Recortar geometria** |
| Booleanas, sólidos por perfil, espessura em face, volume simplificado | ➖ | Nativos: **Modelo no local** e pisos/paredes **por face**, que geram geometria editável (as versões do plugin geravam forma "morta" e eram as mais arriscadas para o Revit) |
| Partes | ➖ | Nativo: **Criar partes**. Para revestimentos, a Paginação faz a divisão com juntas e ponto de partida |
| Aberturas | ➖ | Nativos: **Abertura por face**, **Shaft**; no forro, use Interiores ▸ Aberturas no forro |

## 5. Pisos e revestimentos

| Item | Situação | Onde |
|---|---|---|
| Pisos por ambiente | ✅ | Pisos ▸ Piso por ambiente (Floor Tracer); só pisos de acabamento contam como "já tem piso" (a laje estrutural não conta) |
| Dividir piso | ✅ | Pisos ▸ Dividir piso (Floor Splitter) |
| Unir pisos | ✅ | Pisos ▸ Unir pisos (Floor Merger): vários pisos viram um, com o contorno da união |
| Preencher pisos | ✅ | Pisos ▸ Preencher pisos (Floor Filler): cria piso só onde o ambiente ainda não tem, ou lista as áreas descobertas |
| Paginação com layouts, juntas e ponto de partida | ✅ | Pisos ▸ Paginação de revestimento: 13 padrões (alinhada, amarração, diagonal, espinha de peixe, chevron, trama, hexágono, octógono com taco, opus romano, madeira com réguas aleatórias, losangos 3D, modulares), peças reais do Revit, tonalidades do catálogo distribuídas entre as peças, prévia 2D/3D, liga a exibição de partes na vista |
| Rodapés | ✅ | Pisos ▸ Rodapés automáticos (recortados nas portas) |
| Faixas, bordas e soleiras | ✅ | Pisos ▸ Faixas, bordas e soleiras |
| Quantitativos (peças, recortes, perdas, caixas) | ✅ | Pisos ▸ Quantitativo de revestimento |
| Vistas e legendas de paginação | ✅ | Pisos ▸ Vistas e tabelas de paginação |

## 6. Interiores

| Item | Situação | Onde |
|---|---|---|
| Ambientes | ✅ | Numerar, padronizar nomes e códigos, parâmetros via planilha, verificação, vistas por ambiente, organização de vistas. Criar ambientes é nativo (**Colocar ambientes automaticamente**) |
| Mobiliário e presets de layout | ✅ | Distribuir ao longo de parede/linha, encostar na parede, salvar e aplicar layouts, áreas de uso, lista por ambiente. Inserir é nativo (**Componente**) |
| Ergonomia e acessibilidade com regras editáveis | 🟡 | Interiores ▸ Ergonomia e acessibilidade, com valores de referência da **ABNT NBR 9050:2020** (circulação 0,90 m, manobra Ø 1,50 m, aproximação 0,80 × 1,20 m, vão de porta ≥ 0,80 m, lavatório 0,78–0,80 m, bancadas 0,75–0,85 m, faixa de 0,90 m ao lado da cama, tomadas 0,40–1,00 m e interruptores 0,60–1,00 m). Tudo editável; **nunca declara conformidade**. Confira os valores no texto vigente da norma |
| Iluminação | 🟡 | Grade por espaçamento ou método dos lúmens (estimativa, com 3D), luminárias sobre mobiliário, grupos e circuitos. Não substitui o cálculo luminotécnico |
| Forros | ✅ | Forro por ambiente, sanca e rebaixo (com fenda de luz e 3D), aberturas (ignora furos sobrepostos). Plantas de forro são nativas ou saem do **Conjunto de plantas** |
| Paredes e painéis | ✅ | Pintura por ambiente, revestimento recortando vãos, ripado e boiserie (com 3D) |

## 7. Marcenaria planejada

| Item | Situação | Onde |
|---|---|---|
| Qualquer móvel (construtor livre) | ✅ | Marcenaria ▸ Marcenaria livre: caixa dividida em vãos (colunas/linhas com medida fixa ou automática), prateleiras reguláveis ou fixas, cabideiro, gavetas aparentes ou internas, portas de abrir (esquerda/direita/dupla), basculante, escamoteável, de correr, camarão e vidro; frentes sobrepostas ou embutidas; base com rodapé, pés, suspensa ou no piso; elevação clicável e 3D. Peças TT_Chapa editáveis |
| Módulos (12 tipos) | ✅ | Marcenaria ▸ Módulo de marcenaria (com 3D de todas as chapas) |
| Parametrização e edição | ✅ | Opções completas; **Editar módulo** refaz no lugar |
| Portas, gavetas e ferragens | ✅ | Portas de abrir, duplas, de correr e basculantes; gavetas; dobradiças, corrediças, suportes e pés por regras; **puxadores modelados** (barra, botão, perfil) na posição certa de cada frente |
| Peças que se sobrepõem | ✅ | A marcenaria livre e os módulos verificam peças que se invadem (fora dos encaixes de fundo); o designer marca em vermelho e pergunta antes de gerar |
| Composição ao longo da parede | ✅ | Marcenaria ▸ Compor módulos na parede (com 3D da parede inteira) |
| Vistas de detalhamento | ✅ | Marcenaria ▸ Detalhamento do módulo (inclui 3D explodida e prancha) |
| Plano de corte e lista de peças | ✅ | Marcenaria ▸ Plano de corte e ferragens |
| Excel e CSV | ✅ | Planilha completa e CSV genérico de fabricação |
| Dados para fabricação | 🟡 | CSV genérico. Formatos específicos de cada máquina CNC **não** são gerados, como previsto no pedido |
| Nesting de chapas | 🟡 | Algoritmo próprio (guilhotina ou livre, com veio, serra e refilo) e planos desenhados. É heurístico: confira com o fornecedor |

## 8. Materiais

| Item | Situação | Onde |
|---|---|---|
| Catálogo de acabamentos do projeto | ✅ | Materiais ▸ Catálogo de acabamentos: dentro do .rvt, com planilha CSV; cria materiais (com hachura das juntas), tonalidades e tipos de piso/parede/rodapé/forro. Aplicar acabamento do catálogo e Quantitativo e custo dos acabamentos |
| Biblioteca de materiais | ✅ | Materiais ▸ Biblioteca (edição via planilha): **98 materiais**, 56 com **textura de imagem** (ambientCG, domínio público CC0) |
| Substituir | ✅ | Materiais ▸ Substituir material |
| Aplicar | ➖ | Nativos: **Propriedades** (edição múltipla) e **Pintar** |
| Acabamentos por ambiente | ✅ | Materiais ▸ Acabamentos por ambiente (regras por nome) |
| Especificação e relatório de uso | ✅ | Materiais ▸ Ficha técnica de materiais (área, volume, elementos e uso por categoria) |

## 9. Documentação

| Item | Situação | Onde |
|---|---|---|
| Vistas | ✅ | Conjunto de plantas, cortes por ambiente, axonométricas cortadas, vistas por ambiente |
| Pranchas | ✅ | Criar em lote, montar automaticamente, renumerar pranchas e detalhes |
| Anotações | ✅ | Cotas internas dos ambientes, legendas de acabamentos e cores, notas padrão |
| Carimbo, índice, etiquetar tudo, configurar vistas em lote | ➖ | Nativos: **Informações do projeto**, **Lista de folhas**, **Etiquetar todos não etiquetados** e seleção múltipla no navegador |
| Exportação PDF e DWG | ➖ | Nativos: **Exportar ▸ PDF** e **Exportar ▸ CAD** do Revit 2027 |

## 10. Quantitativos e orçamento

| Item | Situação | Onde |
|---|---|---|
| Tabelas de quantitativos | ✅ | Quantitativos ▸ Tabelas de quantitativos (inclusive tabela de ambientes) |
| Exportação Excel/CSV | ✅ | Quantitativos ▸ Levantamento; e todo relatório |
| Orçamento | ✅ | Quantitativos ▸ Orçamento (com a sua tabela de preços, perdas e mão de obra) |
| Comparação entre versões | ✅ | Quantitativos ▸ Comparar quantitativos |

## 11. Controle de qualidade

| Item | Situação | Onde |
|---|---|---|
| Verificações com regras personalizadas | ✅ | Qualidade ▸ Verificação de qualidade e Regras de verificação (planilha) |

## 12. Gestão do projeto

| Item | Situação | Onde |
|---|---|---|
| Presets e bibliotecas | ✅ | Projeto ▸ Presets e bibliotecas |
| Transferência de padrões | ✅ | Projeto ▸ Transferir configurações do Téra Tools (presets, regras e temas). Tipos, materiais e vistas: nativo **Gerenciar ▸ Transferir padrões de projeto** |
| Processamento em lote | ✅ | Projeto ▸ Processamento em lote |
| Referências visuais | ✅ | Projeto ▸ Referências visuais (imagens com notas dentro do .rvt, ligadas aos sistemas gerados) |
| Autoteste | ✅ | Geral ▸ Autoteste do plugin (cenário temporário, desfeito no fim) |
| Configurações do projeto | ➖ | Nativo: **Gerenciar ▸ Informações do projeto** |

## 13. Recursos avançados

| Item | Situação |
|---|---|
| Recursos avançados | ⏳ Como combinado no pedido, ficam para depois da estabilização dos módulos principais (após os testes reais no Revit) |
