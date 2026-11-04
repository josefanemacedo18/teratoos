# Téra Tools — referência das ferramentas

Lista gerada a partir do código-fonte: **124 ferramentas** em 14 módulos. No Revit, cada ferramenta também tem uma página de ajuda (botão **Ajuda** ou tecla **F1** no diálogo, ou no painel Téra).

## Índice

- [Geral](#geral) — Painel, busca, configurações e ajuda (6)
- [Modelagem](#modelagem) — Alinhar, distribuir, matrizes e transformações (13)
- [Seleção](#seleção) — Seleção, filtros e visibilidade (6)
- [Análise](#análise) — Colisões e medições (3)
- [Geradores](#geradores) — Padrões, imagem, brises, quadros espaciais, cinético e temas (11)
- [Geometria](#geometria) — Booleanas, partes, aberturas e sólidos (12)
- [Pisos](#pisos) — Pisos, paginação, rodapés e revestimentos (8)
- [Interiores](#interiores) — Ambientes, mobiliário, ergonomia, iluminação, forros e paredes (27)
- [Marcenaria](#marcenaria) — Módulos planejados, detalhamento e plano de corte (7)
- [Materiais](#materiais) — Biblioteca, aplicação e especificação de materiais (6)
- [Documentação](#documentação) — Vistas, pranchas, anotações e exportação (15)
- [Quantitativos](#quantitativos) — Tabelas, quantitativos e orçamento (4)
- [Qualidade](#qualidade) — Verificações e regras personalizadas (2)
- [Projeto](#projeto) — Configurações, presets, bibliotecas e lote (4)

## Geral

_Painel, busca, configurações e ajuda_

### Painel Téra
`general.pane`

Mostra ou oculta o painel lateral com busca, favoritos, recentes e histórico.

> O painel lateral reúne todas as ferramentas do Téra Tools organizadas por módulo.
>
> • Digite na busca para filtrar por nome (com ou sem acento); Enter executa a primeira.
>
> • Clique na estrela para favoritar.
>
> • A aba Histórico mostra as últimas operações; clique para repetir.

### Configurações
`general.settings`

Idioma, painel, limites de desempenho, pastas e exportação.

> Configurações globais valem para todos os projetos e ficam em %AppData%\TeraTools.
>
> As configurações por projeto ficam em Projeto › Dados e padrões do projeto.

### Ajuda
`general.help`

Abre a documentação integrada com todas as ferramentas.

### Histórico de operações
`general.history`

Lista as operações executadas pelo plugin, com status, duração e resumo.

### Diagnóstico
`general.about`

Versão, pastas, templates de família encontrados e informações para suporte.

### Abrir pasta de dados
`general.logs`

Abre a pasta com configurações, presets, logs e exportações do plugin.

## Modelagem

_Alinhar, distribuir, matrizes e transformações_

### Alinhar
`modeling.align`

Alinha elementos por bordas, centros ou elevação, em relação a um elemento, ponto ou ao conjunto.

> Selecione os elementos (ou execute e selecione). Escolha a direção (horizontal/vertical da vista ou altura), a borda (início, centro, fim) e a referência.
>
> • Em plantas, horizontal = X e vertical = Y da vista.
>
> • Em cortes e elevações, a vertical é a altura.
>
> • Elementos fixados (pinned) ou travados são ignorados e listados no relatório.

### Distribuir
`modeling.distribute`

Distribui elementos uniformemente (centros ou vãos iguais) ou com espaçamento fixo.

> Os elementos são ordenados pela posição na direção escolhida. Nos modos 'iguais', o primeiro e o último ficam no lugar; nos modos 'fixos', o primeiro fica no lugar e os demais são reposicionados.

### Matriz linear/retangular
`modeling.arraylinear` · Matrizes

Cria cópias em linha ou em grade, por espaçamento ou distribuídas entre dois pontos.

> As cópias são elementos independentes (não é uma matriz associativa do Revit). Use 'Agrupar o resultado' para mover tudo junto.

### Matriz radial
`modeling.arrayradial` · Matrizes

Cria cópias em torno de um centro escolhido, com ângulo total configurável.

> Com 360°, as cópias se distribuem por toda a volta; com outros ângulos, a última cópia cai exatamente no ângulo final.

### Copiar com deslocamento
`modeling.copy` · Transformar

Copia (ou move) elementos com deslocamento numérico e rotação, quantas vezes quiser.

### Rotacionar
`modeling.rotate` · Transformar

Gira elementos em torno do próprio centro, do centro do conjunto ou de um ponto, com variação aleatória opcional.

### Espelhar
`modeling.mirror` · Transformar

Espelha elementos por um eixo do conjunto, por dois pontos ou por uma linha/grade/plano.

### Posição e elevação
`modeling.elevation` · Transformar

Ajusta a elevação de famílias, pisos, forros e paredes (por parâmetro) ou move outros elementos em Z.

> Usa o parâmetro nativo de deslocamento do elemento quando existe (elevação a partir do nível, deslocamento do hospedeiro, altura acima do nível, deslocamento da base). Caso contrário, move o elemento verticalmente.

### Redimensionar
`modeling.resize` · Transformar

Altera dimensões de elementos compatíveis via parâmetros (definir, somar ou escalar).

> Funciona com qualquer família que tenha parâmetros de comprimento (ex.: Largura, Altura, Comprimento). Elementos sem o parâmetro são listados no relatório.

### Agrupar
`modeling.group` · Grupos

Cria um grupo de modelo com os elementos selecionados, com nome opcional.

### Desagrupar
`modeling.ungroup` · Grupos

Desfaz todos os grupos selecionados (os membros ficam selecionados).

### Substituir família/tipo
`modeling.replacetype`

Troca o tipo (ou a família) dos elementos selecionados, mantendo posição e parâmetros de instância compatíveis.

> Selecione elementos de uma mesma categoria. A lista mostra todos os tipos compatíveis carregados no projeto (inclusive de outras famílias da mesma categoria). Valores de parâmetros de instância com o mesmo nome são restaurados após a troca.

### Pontos e linhas auxiliares
`modeling.refline`

Cria linhas de modelo/detalhe ou planos de referência por pontos, centros ou eixos dos elementos.

## Seleção

_Seleção, filtros e visibilidade_

### Filtro avançado
`selection.filter`

Seleciona, isola ou oculta por categoria, família, tipo, material, nível, ambiente ou fase, com condição de parâmetro.

> 1. Escolha onde procurar (vista, projeto ou seleção) e o critério de agrupamento.
>
> 2. Opcionalmente, defina uma condição de parâmetro (ex.: Comentários contém "demolir").
>
> 3. Na lista seguinte, marque os grupos que deseja (a quantidade aparece ao lado).
>
> Comparações 'maior/menor que' entendem as unidades do projeto (ex.: 2,10 m).

### Selecionar semelhantes
`selection.similar`

Seleciona todos os elementos do mesmo tipo dos selecionados (na vista ou no projeto).

### Isolar
`selection.isolate` · Visibilidade

Isola temporariamente os elementos selecionados na vista ativa.

### Ocultar
`selection.hide` · Visibilidade

Oculta os elementos (ou categorias) selecionados, temporária ou permanentemente.

### Revelar ocultos
`selection.reveal` · Visibilidade

Desativa isolamento/ocultação temporária e reexibe elementos ocultos na vista ativa.

### Elementos do ambiente
`selection.byroom`

Seleciona (ou isola) os elementos que estão dentro dos ambientes escolhidos.

## Análise

_Colisões e medições_

### Verificar colisões
`analysis.clash`

Encontra interferências geométricas entre os elementos selecionados ou contra categorias escolhidas.

> Usa a geometria real dos elementos (não só as caixas). No relatório, clique duas vezes em uma colisão para selecionar o par no modelo.

### Medir seleção
`analysis.measure`

Soma comprimentos, áreas, perímetros e volumes dos elementos selecionados.

### Medir distância
`analysis.distance`

Mede a distância entre dois pontos (total, em planta e em altura).

## Geradores

_Padrões, imagem, brises, quadros espaciais, cinético e temas_

### Gerador de padrões
`gen.pattern`

Padrões lineares, retangulares, triangulares, hexagonais, radiais, espirais e aleatórios em faces planas/curvas, com gradientes, atratores, relevo e perfurações.

> 1. Escolha onde aplicar (face plana, face curva, retângulo ou ambiente) e a saída.
>
> 2. Ajuste distribuição, módulo e gradiente vendo a pré-visualização.
>
> 3. Clique em Gerar.
>
> • Família paramétrica: cada módulo é um TT_Bloco editável (largura, profundidade, altura e material por instância).
>
> • Forma livre: formas exatas (círculos, hexágonos...) num elemento leve por material.
>
> • Perfurações: abre furos no piso/forro/telhado (formas exatas) ou na parede (retângulos).
>
> • Para editar depois: Geradores › Editar sistema gerado.
>
> Limitação: em faces curvas os módulos ficam tangentes à superfície no centro de cada célula; o espaçamento é medido sobre a superfície desenrolada (aproximado).

### Imagem para blocos
`gen.image`

Converte uma imagem em blocos (mosaicos, relevos e painéis), com altura por tom e cores mapeadas para materiais.

> A imagem é dividida em células; cada célula vira um bloco com altura pelo brilho e material pela cor.
>
> • Níveis de altura reduzem variações (mais fácil de fabricar).
>
> • Paleta automática agrupa as cores (k-means) e cria um material por cor.
>
> • O limite de blocos protege o desempenho do modelo: a resolução é reduzida se necessário.

### Aletas e brises
`gen.brise`

Gera aletas verticais ou horizontais ao longo de paredes ou linhas, com inclinação, variação paramétrica, materiais por conjunto e quantitativos.

> Escolha uma parede (o brise fica do lado externo, a partir da face) ou linhas de modelo (caminho livre, inclusive curvo). A variação altera a rotação (e opcionalmente a largura) de cada aleta: linear, onda, aleatória, atrator ou fórmula.
>
> Com 'Ajustar aos limites da fachada', as aletas são interrompidas nas janelas e portas da parede.

### Quadro espacial
`gen.spaceframe`

Gera malhas espaciais de duas ou mais camadas (quadradas ou triangulares) sobre pisos, coberturas, ambientes ou superfícies curvas, com quantitativos de barras e nós.

> A planta da estrutura vem do elemento escolhido (piso, cobertura, ambiente) ou de um retângulo. As barras podem ser geradas como forma livre (tubos), blocos paramétricos ou como vigas/contraventos estruturais do tipo escolhido — estes são elementos BIM nativos.
>
> O relatório traz a lista de barras agrupadas por função e comprimento e a contagem de nós por quantidade de barras conectadas.
>
> Limitação: é um gerador geométrico; dimensionamento estrutural deve ser feito em software de cálculo.

### Painéis cinéticos
`gen.kinetic`

Conjuntos de painéis móveis com eixos, ângulos de abertura, comportamentos (onda, atrator, fórmula), estados em vistas e exportação para animação.

> Escolha a face da fachada; o conjunto começa no canto inferior esquerdo da face. Cada estado (ex.: 0;50;100) gera um conjunto e, se pedido, uma vista 3D que mostra só aquele estado. O ângulo de cada painel fica no parâmetro TT_Abertura.
>
> Para mudar a abertura depois, use "Atualizar estado cinético".
>
> Limitação: a movimentação é paramétrica (estados). Animação contínua em tempo real e simulação física não fazem parte do Revit — use a exportação OBJ para animar em outro programa.

### Atualizar estado cinético
`gen.kineticstate`

Muda a abertura (ou o comportamento) de um conjunto cinético já gerado.

### Paleta de cores
`gen.palette` · Temas

Cria paletas harmônicas (análoga, complementar, tríade, neutros, degradê) e materiais com essas cores.

### Colorir por parâmetro
`gen.colorby` · Temas

Cria filtros de vista por categoria e valor de parâmetro, com cores, transparência e legenda; o tema fica salvo no projeto.

> Ex.: colorir mobiliário por fornecedor, ambientes por departamento, pisos por material ou paredes por fase. Depois de escolher as categorias, a lista mostra apenas parâmetros filtráveis em comum. Cada valor vira um filtro "TT Cor ..." editável normalmente em Visibilidade/Gráficos.

### Estilo de apresentação
`gen.viewstyle` · Temas

Aplica configurações gráficas de apresentação (estilo visual, linhas à mão, sombras, fundo, anotações) e salva como preset reutilizável.

### Editar sistema gerado
`gen.edit`

Reabre as opções de qualquer padrão, mosaico, brise, quadro espacial ou cinético gerado e o regenera no mesmo lugar.

> Selecione um elemento criado por um gerador. As opções usadas na criação são carregadas; ao confirmar, o sistema antigo é substituído (tudo em um único Desfazer).

### Selecionar sistema gerado
`gen.select`

Seleciona todos os elementos do sistema gerado ao qual o elemento escolhido pertence.

## Geometria

_Booleanas, partes, aberturas e sólidos_

### Unir geometria
`geo.join` · Unir e recortar

Une a geometria de todos os pares de elementos selecionados que se tocam (paredes, pisos, vigas, massas...).

### Desunir geometria
`geo.unjoin` · Unir e recortar

Desfaz as uniões de geometria entre os elementos selecionados (ou com qualquer elemento, se só um for selecionado).

### Recortar com sólido/vazio
`geo.cut` · Unir e recortar

Recorta elementos com um sólido ou instância de vazio (corte nativo e editável do Revit).

> Primeiro selecione os elementos a recortar; depois escolha o elemento que recorta. Funciona com famílias de vazio ("Cortar com vazios ao carregar") e com sólidos que permitem corte (massas, modelos genéricos). Combinações não suportadas pelo Revit são listadas no relatório.

### Booleanas (forma livre)
`geo.boolean`

União, subtração ou interseção dos sólidos dos elementos, gerando uma forma livre (DirectShape).

> Limitação: o resultado é uma forma livre (geometria de apresentação com categoria, material e parâmetros), não uma família paramétrica. Operações booleanas dependem da representação geométrica: elementos sem sólidos ou com geometria inválida são ignorados e listados.

### Dividir em partes
`geo.parts`

Cria partes do Revit e as divide em grade, com junta — base para paginação e fabricação.

> Partes são elementos nativos: cada peça pode ter material próprio, ser excluída, ter faces deslocadas e entrar em tabelas de partes. Funciona com pisos, forros, telhados e paredes.

### Aberturas e vazios
`geo.opening`

Cria aberturas retangulares ou circulares em pisos, forros e telhados, e aberturas retangulares em paredes.

> Escolha o hospedeiro e clique nos centros das aberturas (Esc para terminar). Em paredes, a abertura é posicionada pelo ponto clicado e pelo peitoril.

### Sólido por perfil
`geo.solid`

Cria sólidos por extrusão, revolução ou varredura a partir de linhas de modelo fechadas.

> Selecione as linhas do perfil (devem formar um ou mais contornos fechados e planos). Para revolução, depois escolha a linha do eixo; para varredura, escolha as linhas do caminho. O resultado é uma forma livre com categoria e material.

### Aplicar espessura em face
`geo.thicken`

Cria uma placa com espessura a partir de uma face plana (revestimentos, tampos, painéis).

### Converter linhas/CAD em elementos
`geo.convert`

Transforma linhas de modelo/detalhe ou de um DWG em paredes, pisos, forros, separadores de ambiente ou linhas de modelo.

> Selecione linhas ou um CAD importado/vinculado. Filtre pela camada do DWG (ex.: "PAREDE"). Para pisos e forros, as linhas são encadeadas em contornos fechados automaticamente (folga de até 3 mm).

### Volume simplificado
`geo.simplify`

Gera volumes leves (caixa ou contorno convexo) que representam elementos complexos — útil para estudos e desempenho.

### Deslocar face de parte
`geo.offsetface`

Desloca faces de partes (recurso nativo para ajustar espessuras de peças de revestimento).

> Limitação: o Revit só permite deslocar faces de PARTES. Crie partes primeiro (Dividir em partes ou Paginação).

### Verificar geometria
`geo.validate`

Procura elementos sem geometria, com volume nulo, linhas curtas demais, duplicados no mesmo lugar e avisos do Revit.

## Pisos

_Pisos, paginação, rodapés e revestimentos_

### Piso por ambiente
`floor.byroom`

Cria pisos pelo contorno dos ambientes (face de acabamento, eixo ou núcleo), com furos de pilares e shafts.

### Dividir piso em regiões
`floor.regions`

Usa contornos fechados de linhas de modelo para criar regiões de piso com outro tipo/material, recortando o piso base.

### Faixas, bordas e soleiras
`floor.border`

Cria faixa perimetral (borda) e piso interno por ambiente, e soleiras de transição sob as portas.

### Paginação de revestimento
`floor.paging`

Pagina pisos, paredes e forros com peças reais (partes do Revit) e juntas: alinhada, amarração, diagonal, espinha de peixe, trama e modular.

> O revestimento é dividido em PARTES nativas do Revit com a junta como folga: cada peça é um elemento com área, material e status (inteira/recortada), visível em vistas com 'Partes' e contável em tabelas.
>
> • Ponto inicial: canto, peça centralizada, junta centralizada ou ponto clicado.
>
> • 'Sugerir melhor ponto inicial' testa deslocamentos e escolhe o que evita filetes (recortes finos) e reduz compras.
>
> • Filetes menores que o recorte mínimo aparecem em vermelho na pré-visualização e no relatório.
>
> Para refazer: desfaça (Ctrl+Z) ou exclua as partes do elemento e rode de novo.

### Rodapés automáticos
`floor.baseboard`

Cria rodapés ao longo do contorno dos ambientes, interrompendo nas portas, sem alterar a área dos ambientes.

> O rodapé é criado como parede fina (tipo escolhido) junto à face de acabamento, com 'Delimitação de ambiente' desligada e sem unir com as paredes. Assim aparece em tabelas de paredes/materiais e pode ser editado normalmente.

### Quantitativo de revestimento
`floor.quantities`

Calcula peças inteiras, recortes, perdas, caixas e área a comprar por ambiente, piso ou forro — sem alterar o modelo.

### Ambientes sem revestimento
`floor.uncovered`

Lista ambientes sem piso modelado (ou sem acabamento de piso informado) para conferência.

### Vistas e tabelas de paginação
`floor.pagingviews`

Cria a planta de paginação (partes visíveis, recortes destacados) e a tabela de peças por material e situação.

## Interiores

_Ambientes, mobiliário, ergonomia, iluminação, forros e paredes_

### Criar ambientes automaticamente
`room.create` · Ambientes

Identifica todos os espaços fechados do nível e cria os ambientes com nome e numeração em sequência.

### Numerar ambientes
`room.number` · Ambientes

Renumera ambientes em sequência: por cliques, por posição, sentido horário ou por nível; evita números repetidos.

### Padronizar nomes e códigos
`room.rename` · Ambientes

Localiza/substitui, padroniza maiúsculas e aplica códigos de ambiente por dicionário e modelo (ex.: SAL-01).

### Parâmetros de ambientes
`room.params` · Ambientes

Exporta os ambientes para planilha, importa as alterações de volta (por Id) ou define valores em lote.

> Fluxo planilha: 1) Exportar CSV. 2) Edite no Excel (não altere a coluna Id). 3) Importar: cada coluna com nome de parâmetro editável é atualizada.

### Tabela de ambientes
`room.schedule` · Ambientes

Cria a tabela de ambientes com número, nome, nível, área, perímetro, departamento e acabamentos.

### Verificar ambientes
`room.check` · Ambientes

Encontra ambientes não colocados, não fechados/redundantes, sem nome, com número repetido ou área muito pequena.

### Vistas por ambiente
`room.views` · Ambientes

Cria para cada ambiente uma planta recortada e as quatro elevações internas, já enquadradas e nomeadas.

### Organizar vistas por setor
`views.organize` · Ambientes

Classifica vistas em grupos (layout, acabamentos, iluminação, forro...) por regras de nome, para organizar o navegador de projeto.

### Inserir mobiliário
`furn.place` · Mobiliário

Insere móveis por cliques ou no centro dos ambientes, já girados de costas para a parede e, se quiser, encostados nela.

> Famílias hospedadas em face (ex.: luminária de forro) usam a inserção nativa do Revit. As demais são posicionadas pelo plugin, num único Desfazer.

### Distribuir ao longo de parede/linha
`furn.distribute` · Mobiliário

Distribui móveis (cadeiras, mesas, armários) ao longo de uma parede ou linha, por quantidade ou espaçamento, alinhados a ela.

### Encostar na parede
`furn.alignwall` · Mobiliário

Gira cada móvel de costas para a parede mais próxima do ambiente e o encosta nela, com a folga indicada.

### Salvar layout de mobiliário
`furn.presetsave` · Mobiliário

Guarda os móveis selecionados como um layout reutilizável (posições relativas a um ponto base e direção).

### Aplicar layout de mobiliário
`furn.presetapply` · Mobiliário

Insere um layout salvo em outro ponto/direção — ideal para repetir quartos, estações de trabalho e banheiros.

### Áreas de uso do mobiliário
`furn.clearance` · Mobiliário

Desenha na planta as áreas de uso em frente (e nas laterais) dos móveis e aponta conflitos com outros móveis e paredes.

> As profundidades vêm das regras editáveis (palavra no nome da família/tipo). As áreas são regiões preenchidas na vista ativa; rodar de novo substitui as anteriores.

### Lista de mobiliário por ambiente
`furn.list` · Mobiliário

Lista móveis e equipamentos por ambiente (família, tipo, quantidade), grava o ambiente em cada peça e cria a tabela.

### Ergonomia e acessibilidade
`ergo.check` · Verificações

Verifica passagens, área de manobra, áreas de aproximação, abertura de portas e alturas de bancadas com regras editáveis — sem declarar conformidade.

> As regras são suas: ajuste os valores conforme a norma e a versão adotadas no projeto e salve como preset (pessoal ou do projeto). O relatório lista os pontos de atenção encontrados pelo modelo geométrico; ele NÃO substitui a verificação do profissional e NÃO declara conformidade.
>
> Limitações: as pegadas dos móveis são envoltórias convexas em planta; portas sem arco de abertura na planta têm a área estimada (quarto de círculo); vínculos (links) não são analisados.

### Luminárias em grade
`light.grid` · Iluminação

Distribui luminárias nos ambientes por espaçamento ou pelo método dos lúmens, alinhadas ao ambiente e hospedadas no forro quando possível.

> Método dos lúmens: N = (E × A) / (Φ × FU × FM). É uma estimativa de pré-dimensionamento; o cálculo luminotécnico final deve ser feito em software próprio.
>
> Fixação no forro: famílias baseadas em face são colocadas na face inferior do forro; famílias hospedadas em forro usam o forro como hospedeiro; as demais ficam na altura do forro encontrado (ou na altura indicada).

### Luminárias sobre mobiliário
`light.overfurniture` · Iluminação

Centraliza pendentes/luminárias sobre mesas, bancadas, camas e ilhas — uma ou várias ao longo do comprimento do móvel.

### Grupos e circuitos de iluminação
`light.groups` · Iluminação

Atribui grupos/circuitos às luminárias (TT_Grupo), manual ou automaticamente por ambiente, e os colore na vista com filtros.

### Forro por ambiente
`ceil.byroom` · Forros

Cria o forro de cada ambiente pelo contorno de acabamento, na altura escolhida e, se quiser, com tabica (afastamento) nas paredes.

### Sanca e rebaixo de forro
`ceil.cove` · Forros

Cria rebaixo perimetral (faixa) e forro central em alturas diferentes, com fechamento vertical (sanca fechada ou aberta com fenda de luz) e tabica.

> O fechamento vertical é feito com paredes finas (sem delimitar ambientes), contornando o forro central. Tudo fica num único Desfazer e editável como elementos nativos do Revit.

### Planta de forro por nível
`ceil.rcp` · Forros

Cria plantas de forro (RCP) para os níveis escolhidos, com modelo de vista, escala e etiquetas de altura dos forros.

### Aberturas no forro
`ceil.opening` · Forros

Abre furos no forro sob luminárias, difusores e outros objetos selecionados — retangulares, circulares ou pelo contorno, com folga.

### Pintar paredes do ambiente
`wall.paint` · Paredes

Aplica (ou remove) um material nas faces das paredes voltadas para os ambientes escolhidos, sem alterar o tipo das paredes.

> Se uma face continua em outro ambiente, o Revit pinta a face inteira. Para pintar só o trecho do ambiente, use antes "Dividir face" do Revit no limite entre os ambientes: o plugin pinta apenas as regiões voltadas para o ambiente escolhido.

### Revestimento de parede por ambiente
`wall.finish` · Paredes

Cria o revestimento (parede fina) em todo o contorno do ambiente, com a altura escolhida, recortando portas e janelas (acima e abaixo dos vãos).

> O revestimento não delimita ambientes e não se une às paredes existentes; cada trecho recebe TT_Ambiente. Use um tipo de parede fino com o material desejado — assim a área aparece nos quantitativos de parede.

### Ripado / painel ripado
`wall.slats` · Paredes

Gera ripado vertical ou horizontal sobre uma face, com painel de fundo, margens, recortes em vãos e materiais — como peças BIM editáveis.

> Escolha a face (parede, divisória, lateral de móvel). As ripas usam a família paramétrica TT_Bloco (largura, profundidade e altura editáveis). Use "Editar sistema gerado" para mudar o ripado depois.

### Boiserie / molduras
`wall.boiserie` · Paredes

Gera quadros de moldura (boiserie) em grade sobre uma face, com margens, roda-meio e desvio automático de portas e janelas.

## Marcenaria

_Módulos planejados, detalhamento e plano de corte_

### Módulo de marcenaria
`join.module` · Módulos

Cria um módulo paramétrico (armário, aéreo, torre, nicho, roupeiro, gabinete, bancada, ilha, painel, estante) com caixa, fundo, prateleiras, portas, gavetas, tampo e ferragens.

> Cada peça é uma chapa TT_Chapa (categoria Marcenaria) com dados de corte: código, módulo, comprimento, largura, espessura, fitas de borda e veio. As peças formam um conjunto (assembly) para vistas de detalhamento. Para alterar depois, use "Editar módulo": o módulo é refeito no mesmo lugar.
>
> Regras de ferragens (dobradiças por altura, corrediças, folgas) ficam em "Regras de marcenaria".

### Compor módulos na parede
`join.run` · Módulos

Divide um trecho de parede em módulos de larguras equilibradas (com arredondamento e complemento), cria todos e, se quiser, um tampo contínuo.

> 1) Escolha o tipo e configure o módulo-base. 2) Clique na face da parede no início e no fim do trecho. 3) Ajuste larguras mínima/máxima/preferida e o complemento: a pré-visualização mostra a divisão.

### Editar módulo
`join.edit` · Módulos

Reabre as opções de um módulo existente (medidas, frentes, gavetas, materiais) e o refaz no mesmo lugar, mantendo o código.

### Detalhamento do módulo
`join.views` · Detalhamento

Cria vistas de detalhamento do conjunto (frontal, lateral, planta, corte, 3D e 3D explodida), lista de peças e a prancha montada.

### Plano de corte e ferragens
`join.cutlist` · Produção

Gera a lista de peças (medidas de corte, fitas, veio), consumo de chapas e fitas, lista de ferragens e o arquivo para fabricação (Excel/CSV).

> As medidas vêm das peças do modelo (se você alterou uma peça no Revit, a alteração entra na lista). O CSV de fabricação usa colunas comuns (material, espessura, comprimento, largura, quantidade, veio, fitas); ajuste o mapeamento ao seu software de corte/máquina.

### Otimizar chapas (nesting)
`join.nesting` · Produção

Distribui as peças nas chapas (corte guilhotinado ou livre), respeitando veio, serra e refilo; mostra aproveitamento, sobras e desenha os planos de corte.

> É um otimizador heurístico: o resultado é bom para orçamento e planejamento, mas confira com o seu fornecedor/máquina. Arquivos específicos de CNC dependem do formato de cada máquina e não são gerados automaticamente.

### Regras de marcenaria
`join.rules` · Produção

Define as regras do projeto: dobradiças por altura, corrediças, folgas, suportes, conectores, larguras de porta e perdas.

> As regras ficam salvas no próprio arquivo do projeto e valem para novos módulos, edições e listas de ferragens. Salve presets para reutilizar em outros projetos.

## Materiais

_Biblioteca, aplicação e especificação de materiais_

### Biblioteca de materiais
`mat.library`

Biblioteca pessoal de materiais (cor, código, fabricante, referência, acabamento, fornecedor, custo) para criar no projeto, alimentar do projeto e editar no Excel.

> A biblioteca fica em %AppData%\TeraTools\bibliotecas\materiais.json. Para editar em massa: exporte para CSV, edite no Excel (mantenha os cabeçalhos) e importe de volta. Uma pasta de biblioteca compartilhada pode ser definida nas configurações.

### Aplicar material
`mat.apply`

Aplica um material aos elementos selecionados pelo parâmetro de material (TT_Material ou outro) ou pintando as faces.

### Acabamentos por ambiente
`mat.byroom`

Preenche piso, parede, forro e rodapé de cada ambiente por regras de nome (ex.: Banh* → porcelanato), pinta paredes se quiser e cria a tabela de acabamentos.

### Substituir material
`mat.replace`

Troca um material por outro em todo o projeto: parâmetros de tipos e instâncias, camadas de sistemas e faces pintadas.

### Ficha técnica de materiais
`mat.specs`

Gera o memorial/ficha de materiais: código, nome, categoria, fabricante, referência, acabamento, fornecedor, custo e onde cada um é usado.

### Relatório de uso de materiais
`mat.report`

Mostra área, volume e quantidade de elementos de cada material (por categoria), com seleção dos elementos — sem alterar o modelo.

## Documentação

_Vistas, pranchas, anotações e exportação_

### Conjunto de plantas
`doc.plans` · Vistas

Cria de uma vez as plantas do projeto de interiores por nível (layout, demolir/construir, elétrica, paginação, forro, acabamentos), com modelo de vista e recorte.

### Cortes por ambiente
`doc.sections` · Vistas

Cria cortes longitudinal e transversal passando pelo centro de cada ambiente, alinhados às paredes e enquadrados.

### Axonométricas (3D) cortadas
`doc.axo` · Vistas

Cria vistas 3D isométricas por ambiente ou por nível, com caixa de corte na altura escolhida — ideais para apresentação de interiores.

### Configurar vistas em lote
`doc.viewsettings` · Vistas

Aplica escala, nível de detalhe, estilo visual, recorte e modelo de vista a várias vistas de uma vez.

### Criar pranchas em lote
`doc.sheets` · Pranchas

Cria as pranchas do projeto a partir de uma lista ou planilha (número, nome e outros parâmetros), com o carimbo escolhido.

### Montar pranchas automaticamente
`doc.placeviews` · Pranchas

Coloca as vistas escolhidas nas pranchas sem sobreposição, usando o tamanho real de cada vista; cria novas pranchas quando não couber.

### Renumerar pranchas e detalhes
`doc.renumber` · Pranchas

Renumera pranchas em sequência (na ordem escolhida) ou os números de detalhe das vistas da prancha ativa, da esquerda para a direita e de cima para baixo.

### Dados do carimbo
`doc.titleblock` · Pranchas

Preenche os dados do projeto (cliente, endereço, número, status) e das pranchas (desenhado, verificado, aprovado, data) e troca o carimbo em lote.

> Campos vazios não alteram o valor atual. Os dados do projeto aparecem em todos os carimbos que usam as Informações do projeto.

### Índice de pranchas
`doc.sheetindex` · Pranchas

Cria a lista de pranchas (número, nome, revisão, data, responsáveis) como tabela do Revit e planilha, e a coloca numa prancha.

### Exportar PDF
`doc.pdf` · Exportação

Exporta pranchas e vistas para PDF (um arquivo por prancha com nome padronizado, ou um único PDF), em cores, cinza ou preto e branco.

### Exportar DWG
`doc.dwg` · Exportação

Exporta pranchas e vistas para DWG com nomes padronizados, usando uma configuração de exportação do projeto.

### Cotas automáticas
`doc.dims` · Anotação

Cota automaticamente a largura e o comprimento internos dos ambientes ou as paredes com seus vãos (portas e janelas), na planta ativa.

> As cotas são associativas (referenciam as faces das paredes e os eixos das aberturas): se a parede mudar, a cota acompanha. Paredes curvas são ignoradas.

### Etiquetar tudo na vista
`doc.tags` · Anotação

Coloca etiquetas em todos os elementos das categorias escolhidas visíveis na vista (ambientes, portas, janelas, móveis, luminárias...), sem duplicar.

### Legenda de acabamentos e cores
`doc.legend` · Anotação

Cria uma legenda com amostras de cor e textos: acabamentos dos ambientes, materiais escolhidos ou os filtros coloridos da vista ativa.

### Notas padrão
`doc.notes` · Anotação

Biblioteca de notas padrão (gerais, acabamentos, elétrica, marcenaria...) para inserir como bloco numerado e para salvar novos textos.

## Quantitativos

_Tabelas, quantitativos e orçamento_

### Tabelas de quantitativos
`qty.schedules`

Cria de uma vez as tabelas de quantitativos do projeto de interiores: pisos, paredes, forros, portas, janelas, mobiliário, luminárias, marcenaria e materiais.

### Levantamento de quantitativos
`qty.export`

Levanta áreas e quantidades do modelo (sistemas, famílias, chapas de marcenaria e acabamentos dos ambientes), por ambiente, e salva em Excel.

### Orçamento
`qty.budget`

Cruza os quantitativos do modelo com a sua tabela de preços (com perdas e mão de obra) e gera o orçamento por item, ambiente e categoria.

> 1) Use "Gerar modelo de tabela de preços": uma planilha com os itens do seu projeto é criada. 2) Preencha CustoUnitario (e Perda %, MaoDeObra) no Excel. 3) Rode "Calcular orçamento" escolhendo essa planilha. Itens sem preço aparecem destacados.
>
> É uma estimativa: confira quantidades e preços antes de usar comercialmente.

### Comparar quantitativos (antes × depois)
`qty.snapshot`

Salva levantamentos de referência no projeto e compara com o modelo atual, mostrando o que aumentou, diminuiu, entrou ou saiu.

## Qualidade

_Verificações e regras personalizadas_

### Verificação de qualidade
`qa.check`

Verifica o modelo: regras editáveis de parâmetros/nomes, avisos do Revit, duplicados, famílias no local, CAD importado, ambientes e vistas — com seleção dos problemas.

> As regras ficam no projeto (ou na sua biblioteca) e são editadas em "Regras de verificação" (planilha). O relatório pode ser salvo em HTML/Excel para enviar à equipe. Nenhum elemento é alterado.

### Regras de verificação
`qa.rules`

Edita as regras da verificação de qualidade numa planilha: obrigatório, faixa de valores, padrão de texto, lista permitida, valor único e padrão de nome.

> Colunas: Ativa (1/0); Nome; Severidade (Erro/Aviso/Info); Categorias (ex.: OST_Doors,OST_Windows); FiltroFamilia (expressão); Tipo (Obrigatorio, Faixa, Padrao, ValoresPermitidos, Unico, PadraoNome); Parametro (nome ou parâmetro interno, ex.: ALL_MODEL_MARK); ParametroDeTipo (1/0); Min/Max (comprimentos em mm, áreas em m²); Padrao (expressão regular); Valores (separados por |); Mensagem.

## Projeto

_Configurações, presets, bibliotecas e lote_

### Configurações do projeto
`proj.settings`

Dados do projeto guardados no próprio arquivo (código, cliente, responsável, etapa, pasta de exportação), usados por relatórios, nomes e carimbos.

### Presets e bibliotecas
`proj.presets`

Gerencia os presets de todas as ferramentas: lista, exporta/importa arquivos para compartilhar, copia entre pessoal e projeto e exclui.

> Presets pessoais ficam em %AppData%\TeraTools\presets; presets do projeto ficam dentro do .rvt; uma pasta de biblioteca da equipe pode ser definida nas configurações gerais (somente leitura).

### Transferir padrões entre projetos
`proj.transfer`

Copia de outro projeto aberto os modelos de vista, filtros, materiais, padrões, tipos, tabelas e as configurações do Téra Tools (presets, regras, temas).

> Abra o projeto de origem (ou o seu arquivo-modelo) no Revit, volte ao projeto de destino e rode a ferramenta. Itens com o mesmo nome no destino são mantidos (não são sobrescritos).

### Processamento em lote
`proj.batch`

Abre vários arquivos .rvt em segundo plano e executa uma ação em cada um: PDF das pranchas, verificação de qualidade, quantitativos ou atualização de versão.

> Os arquivos são abertos sem interface e fechados ao final (somente "Atualizar e salvar" grava). Arquivos com compartilhamento de trabalho são abertos desanexados para as ações de leitura. Os resultados ficam em exportacoes\lote.
