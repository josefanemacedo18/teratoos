# Auditoria das ferramentas do Téra Tools

Análise de cada uma das 124 ferramentas: o que faz, se o Revit já tem o mesmo comando, o valor real que acrescenta, riscos encontrados no código e a decisão proposta.

**Legenda:** MANTER (fica como está) · REFINAR (fica e será melhorada) · FUNDIR (vira parte de outra) · REMOVER (sai do plugin).

**Critério de remoção:** o Revit já faz o mesmo com igual ou maior precisão, e a ferramenta não automatiza um lote nem um fluxo de interiores/marcenaria.

## Causa do erro "Object of type 'System.Double' cannot be converted to type 'System.Int32'" e dos fechamentos

- **Erro do print:** no diálogo de opções (`src/TeraTools.Revit/Ui/OptionsDialog.cs`, editor numérico), uma expressão condicional misturava `int`, `long`, `float` e `double`. Nesse caso o C# converte todos os ramos para `double`, o que desfazia a conversão para inteiro. **Toda ferramenta com um campo inteiro** (quantidade, colunas, dígitos, linhas etc.) falhava. Corrigido na Fase 1, com 11 testes.
- **Fechamento do Revit:** o diálogo regrava os campos **a cada tecla digitada**, dentro de eventos da interface (WPF) sem proteção. A exceção acima escapava desses eventos direto para o Revit, que fechava. Corrigido na Fase 1: os eventos agora são protegidos, e há uma rede de segurança global (`CrashGuard` em `App.cs`) para exceções do plugin na interface.
- **Ainda precisa ser confirmado no Revit:** fechamentos causados pela própria API (sólidos/booleanas complexas, divisão de partes em grande quantidade). As ferramentas mais arriscadas desse tipo estão marcadas para remoção.

## Resumo

| Decisão | Quantidade |
|---|---|
| MANTER | 55 |
| REFINAR | 23 |
| FUNDIR | 6 |
| REMOVER | 40 |
| **Total** | **124** |

Depois das remoções e fusões, o plugin fica com **78 ferramentas**.

## Geral

| id | Nome | O que faz | Equivalente nativo | Valor adicional | Riscos / código | Decisão | Justificativa |
|---|---|---|---|---|---|---|---|
| `general.pane` | Painel Téra | Painel lateral com busca, favoritos, recentes e histórico | nenhum | Acesso rápido a todas as ferramentas | `General/GeneralTools.cs:14` — Eventos do painel agora cobertos pela proteção global (CrashGuard) | **MANTER** | É a porta de entrada do plugin. |
| `general.settings` | Configurações | Configurações globais do plugin | nenhum | Limites, idioma, pastas | `General/GeneralTools.cs:89` | **MANTER** | Necessária para o funcionamento. |
| `general.help` | Ajuda | Abre o índice de ajuda | nenhum | Ajuda em pt-BR | `General/GeneralTools.cs:139` | **MANTER** | Ajuda contextual pedida na especificação. |
| `general.history` | Histórico de operações | Lista o histórico de operações | nenhum | Repetir operações | `General/GeneralTools.cs:153` — Duplica a aba Histórico do painel | **FUNDIR com general.pane** | O painel já mostra o histórico; um botão a menos na faixa. |
| `general.about` | Diagnóstico | Diagnóstico: versão, pastas, famílias em cache | nenhum | Suporte e diagnóstico | `General/GeneralTools.cs:183` | **MANTER** | Útil para relatar erros. |
| `general.logs` | Abrir pasta de dados | Abre a pasta de dados/logs | nenhum | Atalho para logs | `General/GeneralTools.cs:220` — Função de 5 linhas | **FUNDIR com general.about** | Vira um botão dentro do Diagnóstico. |

## Modelagem

| id | Nome | O que faz | Equivalente nativo | Valor adicional | Riscos / código | Decisão | Justificativa |
|---|---|---|---|---|---|---|---|
| `modeling.align` | Alinhar | Alinha vários elementos por borda/centro | Alinhar (AL) | Alinhar vários de uma vez | `Modeling/ModelingTools.cs:135` — Usa caixa envolvente (inclui símbolos e folgas da família): resultado impreciso | **REMOVER** | O Alinhar nativo usa referências reais e é exato. |
| `modeling.distribute` | Distribuir | Distribui elementos com vãos/centros iguais | nenhum (Revit não tem 'distribuir') | Distribuir móveis, luminárias, painéis | `Modeling/ModelingTools.cs:213` — Hoje mede pela caixa envolvente (impreciso) | **REFINAR** | Sem equivalente nativo; precisa passar a usar o ponto de inserção/linha de locação. |
| `modeling.arraylinear` | Matriz linear/retangular | Cópias em linha ou grade | Matriz (AR) linear | Grade 2D em um passo | `Modeling/ModelingTools.cs:318` — Cópias soltas (não associativas); APIs sensíveis: Copy ModelingTools.cs:356 | **REMOVER** | A Matriz nativa cobre o caso e é associativa/editável. |
| `modeling.arrayradial` | Matriz radial | Cópias em torno de um centro | Matriz (AR) radial | — | `Modeling/ModelingTools.cs:383` — Cópias soltas; APIs sensíveis: Copy ModelingTools.cs:411 | **REMOVER** | Duplica a Matriz radial nativa. |
| `modeling.copy` | Copiar com deslocamento | Copia/move com deslocamento numérico e rotação | Copiar (CO) / Mover (MV) com valor digitado | Pouco | `Modeling/ModelingTools.cs:463` — APIs sensíveis: Copy ModelingTools.cs:485 | **REMOVER** | Nativo faz o mesmo com precisão. |
| `modeling.rotate` | Rotacionar | Gira elementos, inclusive cada um em torno do próprio centro | Girar (RO) — só em torno de um centro comum | Girar vários no lugar (cadeiras, luminárias) | `Modeling/ModelingTools.cs:534` — Centro calculado pela caixa envolvente | **REFINAR** | Girar cada peça no próprio ponto de inserção não existe no Revit; ajustar para usar o ponto de inserção. |
| `modeling.mirror` | Espelhar | Espelha por eixo/2 pontos/linha | Espelhar (MM/DM) | — | `Modeling/ModelingTools.cs:589` — APIs sensíveis: Mirror ModelingTools.cs:601 | **REMOVER** | Duplicado do nativo. |
| `modeling.elevation` | Posição e elevação | Ajusta elevação/deslocamento | Propriedades (edição múltipla) / Mover | — | `Modeling/ModelingTools.cs:671` | **REMOVER** | Edição múltipla nas Propriedades faz o mesmo. |
| `modeling.resize` | Redimensionar | Altera parâmetros de dimensão | Propriedades / Editar tipo | — | `Modeling/ModelingTools.cs:768` — Escala parâmetros às cegas; APIs sensíveis: Group ModelingTools.cs:833 | **REMOVER** | Edição múltipla nativa é mais segura. |
| `modeling.group` | Agrupar | Cria grupo de modelo | Agrupar (GP) | — | `Modeling/ModelingTools.cs:864` | **REMOVER** | Duplicado. |
| `modeling.ungroup` | Desagrupar | Desagrupa | Desagrupar | — | `Modeling/ModelingTools.cs:879` — APIs sensíveis: Group ModelingTools.cs:892 | **REMOVER** | Duplicado. |
| `modeling.replacetype` | Substituir família/tipo | Troca tipo/família mantendo parâmetros | Seletor de tipo | Pouco | `Modeling/ModelingTools.cs:903` | **REMOVER** | O Seletor de tipo já troca dentro da categoria preservando parâmetros de instância. |
| `modeling.refline` | Pontos e linhas auxiliares | Cria linhas/planos de referência | Plano de referência / Linha de modelo | — | `Modeling/ModelingTools.cs:1043` | **REMOVER** | Duplicado. |

## Seleção

| id | Nome | O que faz | Equivalente nativo | Valor adicional | Riscos / código | Decisão | Justificativa |
|---|---|---|---|---|---|---|---|
| `selection.filter` | Filtro avançado | Seleciona/isola por família, tipo, material, nível, ambiente, fase e condição de parâmetro | Filtro (só por categoria) / Selecionar todas as instâncias | Critérios combinados e condição de valor | `Selection/SelectionTools.cs:243` | **REFINAR** | Não existe nativo equivalente; refinar mensagens e casos vazios. |
| `selection.similar` | Selecionar semelhantes | Seleciona instâncias do mesmo tipo | Selecionar todas as instâncias (SA) | — | `Selection/SelectionTools.cs:295` | **REMOVER** | Duplicado exato. |
| `selection.isolate` | Isolar | Isola temporariamente | Ocultar/Isolar temporário (HI) | — | `Selection/SelectionTools.cs:316` | **REMOVER** | Duplicado exato. |
| `selection.hide` | Ocultar | Oculta elementos/categorias | Ocultar na vista (EH/VH) | — | `Selection/SelectionTools.cs:339` | **REMOVER** | Duplicado exato. |
| `selection.reveal` | Revelar ocultos | Reexibe ocultos | Revelar elementos ocultos / HR | — | `Selection/SelectionTools.cs:371` | **REMOVER** | Duplicado. |
| `selection.byroom` | Elementos do ambiente | Seleciona elementos dentro dos ambientes | nenhum | Muito usado em interiores | `Selection/SelectionTools.cs:428` | **MANTER** | Sem equivalente nativo. |

## Análise

| id | Nome | O que faz | Equivalente nativo | Valor adicional | Riscos / código | Decisão | Justificativa |
|---|---|---|---|---|---|---|---|
| `analysis.clash` | Verificar colisões | Interferências geométricas | Verificação de interferência (Colaborar) | — | `Selection/SelectionTools.cs:474` — Booleanas em sólidos (risco de falha em geometrias complexas); APIs sensíveis: Boolean SelectionTools.cs:519 | **REMOVER** | O nativo é mais confiável e gera relatório próprio. |
| `analysis.measure` | Medir seleção | Soma comprimentos/áreas da seleção | Propriedades / Tabelas | Pouco | `Selection/SelectionTools.cs:535` | **REMOVER** | Quantitativos e tabelas cobrem com mais controle. |
| `analysis.distance` | Medir distância | Mede distância entre 2 pontos | Medir (ferramenta nativa) | — | `Selection/SelectionTools.cs:577` | **REMOVER** | Duplicado exato. |

## Geradores

| id | Nome | O que faz | Equivalente nativo | Valor adicional | Riscos / código | Decisão | Justificativa |
|---|---|---|---|---|---|---|---|
| `gen.brise` | Aletas e brises | Aletas ao longo de parede/linha | nenhum (cortina/brise exige famílias manuais) | Variação paramétrica e quantitativos | `Generators/BriseTool.cs:153` | **REFINAR** | Manter. |
| `gen.image` | Imagem para blocos | Imagem → mosaico/relevo | nenhum | Exclusivo | `Generators/ImageTool.cs:123` — Contagem de blocos alta deixa o Revit lento | **REFINAR** | Manter; mensagens de limite mais claras. |
| `gen.kinetic` | Painéis cinéticos | Painéis cinéticos com estados | nenhum | Exclusivo | `Generators/KineticTool.cs:152` | **REFINAR** | Manter. |
| `gen.kineticstate` | Atualizar estado cinético | Muda abertura de um conjunto cinético | nenhum | — | `Generators/KineticTool.cs:327` — Faz o mesmo que Editar sistema gerado | **FUNDIR com gen.edit** | Um único comando de edição basta. |
| `gen.edit` | Editar sistema gerado | Reabre e regenera qualquer sistema gerado | nenhum | Edição paramétrica posterior | `Generators/KineticTool.cs:341` | **MANTER** | Essencial para manter os geradores editáveis. |
| `gen.select` | Selecionar sistema gerado | Seleciona todo o sistema gerado | nenhum (SA seleciona por tipo, não por sistema) | Selecionar um brise/padrão inteiro | `Generators/KineticTool.cs:359` | **MANTER** | Sem equivalente. |
| `gen.pattern` | Gerador de padrões | Padrões paramétricos em faces/ambientes, perfurações | nenhum | Núcleo da proposta Pixy | `Generators/PatternTool.cs:246` — Era a ferramenta do print (campos inteiros) — corrigido; perfurações chamam NewOpening em laço; APIs sensíveis: NewOpening PatternTool.cs:404 | **REFINAR** | Manter; limitar quantidade de furos e melhorar pré-visualização. |
| `gen.spaceframe` | Quadro espacial | Malha espacial | nenhum | Exclusivo | `Generators/SpaceFrameTool.cs:103` — Cria formas livres (DirectShape) para nós/barras; APIs sensíveis: DirectShape SpaceFrameTool.cs:287 | **REFINAR** | Manter; validar geometria antes de criar. |
| `gen.palette` | Paleta de cores | Paletas harmônicas → materiais | nenhum | Criar materiais coordenados | `Generators/ThemeTools.cs:44` | **MANTER** | Útil para temas de apresentação. |
| `gen.colorby` | Colorir por parâmetro | Filtros coloridos por valor de parâmetro + legenda | Esquema de cores (só ambientes/áreas/dutos) | Qualquer categoria, com legenda | `Generators/ThemeTools.cs:138` — APIs sensíveis: FilledRegion ThemeTools.cs:326 | **MANTER** | Estende o esquema de cores nativo a todas as categorias. |
| `gen.viewstyle` | Estilo de apresentação | Estilo visual, sombras, fundo | Opções de exibição de gráficos / Modelos de vista | Pouco | `Generators/ThemeTools.cs:397` | **REMOVER** | Modelos de vista nativos fazem isso e são reutilizáveis. |

## Geometria

| id | Nome | O que faz | Equivalente nativo | Valor adicional | Riscos / código | Decisão | Justificativa |
|---|---|---|---|---|---|---|---|
| `geo.join` | Unir geometria | Une geometria | Unir geometria | Unir em lote | `Geometry/GeometryTools.cs:20` — APIs sensíveis: Boolean GeometryTools.cs:40 | **REMOVER** | Duplicado do nativo. |
| `geo.unjoin` | Desunir geometria | Desune | Desunir geometria | — | `Geometry/GeometryTools.cs:50` — APIs sensíveis: Boolean GeometryTools.cs:64 | **REMOVER** | Duplicado. |
| `geo.cut` | Recortar com sólido/vazio | Recorta com sólido/vazio | Recortar geometria | — | `Geometry/GeometryTools.cs:78` — APIs sensíveis: Boolean GeometryTools.cs:96 | **REMOVER** | Duplicado. |
| `geo.boolean` | Booleanas (forma livre) | Booleanas gerando forma livre | Formas no local (vazio/sólido) | Pouco | `Geometry/GeometryTools.cs:165` — BooleanOperationsUtils em sólidos complexos pode falhar/derrubar; resultado vira forma 'morta'; APIs sensíveis: DirectShape GeometryTools.cs:206, Paint GeometryTools.cs:214, Boolean GeometryTools.cs:191 | **REMOVER** | Alto risco e resultado não-BIM. |
| `geo.parts` | Dividir em partes | Cria e divide partes em grade | Criar partes / Dividir partes | — | `Geometry/GeometryTools.cs:248` — Sobrepõe a Paginação de revestimento; APIs sensíveis: Parts GeometryTools.cs:268 | **REMOVER** | A Paginação já faz isso com juntas, layouts e ponto de partida. |
| `geo.opening` | Aberturas e vazios | Aberturas em pisos/forros/paredes | Abertura por face / Abertura em parede / Shaft | — | `Geometry/GeometryTools.cs:348` — APIs sensíveis: NewOpening GeometryTools.cs:389 | **REMOVER** | Duplicado do nativo. |
| `geo.solid` | Sólido por perfil | Sólido por perfil (forma livre) | Modelo no local (extrusão/revolução/varredura) | — | `Geometry/GeometryTools.cs:453` — Gera DirectShape não editável; APIs sensíveis: DirectShape GeometryTools.cs:504 | **REMOVER** | O nativo gera geometria editável. |
| `geo.thicken` | Aplicar espessura em face | Placa a partir de face | Piso/forro/parede por face | — | `Geometry/GeometryTools.cs:538` — DirectShape; APIs sensíveis: DirectShape GeometryTools.cs:586 | **REMOVER** | Resultado não-BIM; nativo resolve. |
| `geo.convert` | Converter linhas/CAD em elementos | Linhas/CAD → paredes, pisos, forros, separadores | Selecionar linhas (uma a uma) | Converte um DWG inteiro de uma vez | `Geometry/GeometryTools.cs:633` — APIs sensíveis: WallCreate GeometryTools.cs:672, FloorCreate GeometryTools.cs:696 | **REFINAR** | Ganho real de produtividade; refinar validação de laços. |
| `geo.simplify` | Volume simplificado | Volume simplificado (caixa/convexo) | nenhum | Pouco para interiores | `Geometry/GeometryTools.cs:763` — DirectShape; APIs sensíveis: DirectShape GeometryTools.cs:799 | **REMOVER** | Sem uso relevante no fluxo de interiores. |
| `geo.offsetface` | Deslocar face de parte | Desloca face de parte | Formas de parte (alças) nativas | — | `Geometry/GeometryTools.cs:819` | **REMOVER** | Duplicado. |
| `geo.validate` | Verificar geometria | Elementos sem geometria, linhas curtas, duplicados | Revisar avisos | Pouco | `Geometry/GeometryTools.cs:851` — Sobrepõe a Verificação de qualidade; APIs sensíveis: DirectShape GeometryTools.cs:874 | **FUNDIR com qa.check** | Uma verificação única é mais clara. |

## Pisos

| id | Nome | O que faz | Equivalente nativo | Valor adicional | Riscos / código | Decisão | Justificativa |
|---|---|---|---|---|---|---|---|
| `floor.byroom` | Piso por ambiente | Piso pelo contorno dos ambientes | Piso por paredes (um a um) | Lote de ambientes | `Floors/FloorTools.cs:91` | **REFINAR** | Manter; refinar escolha de contorno (acabamento/centro). |
| `floor.regions` | Dividir piso em regiões | Divide piso em regiões | Dividir partes / pisos separados | Regiões de material num piso | `Floors/FloorTools.cs:154` — Exclui e recria pisos | **REFINAR** | Manter; preservar parâmetros do piso original. |
| `floor.border` | Faixas, bordas e soleiras | Faixas, bordas e soleiras | nenhum | Exclusivo | `Floors/FloorTools.cs:218` | **MANTER** | Fluxo de interiores. |
| `floor.paging` | Paginação de revestimento | Paginação com peças reais, juntas e ponto de partida | nenhum (partes manuais) | Núcleo do módulo | `Floors/FloorTools.cs:419` — Divisão de partes em grande quantidade (FloorTools.cs:433) é pesada; APIs sensíveis: Parts FloorTools.cs:433 | **REFINAR** | Manter; limite e aviso de desempenho. |
| `floor.baseboard` | Rodapés automáticos | Rodapés por ambiente recortando portas | Moldura de parede (uma parede por vez) | Lote por ambiente | `Floors/FloorTools.cs:591` — APIs sensíveis: WallCreate FloorTools.cs:631 | **REFINAR** | Manter; refinar cantos. |
| `floor.quantities` | Quantitativo de revestimento | Peças, recortes, perdas, caixas | nenhum | Exclusivo | `Floors/FloorTools.cs:726` | **MANTER** | Fluxo de compra de revestimento. |
| `floor.uncovered` | Ambientes sem revestimento | Ambientes sem acabamento | nenhum | Verificação rápida | `Floors/FloorTools.cs:766` | **MANTER** | Útil. |
| `floor.pagingviews` | Vistas e tabelas de paginação | Vistas/tabelas de paginação | nenhum | Exclusivo | `Floors/FloorTools.cs:811` | **MANTER** | Documentação da paginação. |

## Interiores

| id | Nome | O que faz | Equivalente nativo | Valor adicional | Riscos / código | Decisão | Justificativa |
|---|---|---|---|---|---|---|---|
| `ceil.byroom` | Forro por ambiente | Forro por ambiente com tabica | Forro automático (um clique por ambiente) | Lote e tabica | `Interiors/CeilingTools.cs:63` — APIs sensíveis: FloorCreate CeilingTools.cs:97 | **MANTER** | Útil. |
| `ceil.cove` | Sanca e rebaixo de forro | Sanca e rebaixo com fechamento | nenhum | Exclusivo | `Interiors/CeilingTools.cs:155` — APIs sensíveis: WallCreate CeilingTools.cs:216, FloorCreate CeilingTools.cs:188 | **MANTER** | Útil. |
| `ceil.rcp` | Planta de forro por nível | Plantas de forro por nível | Vista ▸ Planta de forro (vários níveis de uma vez) | Só a etiqueta | `Interiors/CeilingTools.cs:274` | **REMOVER** | Nativo cria em lote; 'Conjunto de plantas' também cria plantas de forro. |
| `ceil.opening` | Aberturas no forro | Furos no forro sob luminárias | Abertura por face (uma a uma) | Lote | `Interiors/CeilingTools.cs:341` — NewOpening em forro (CeilingTools.cs:404); APIs sensíveis: NewOpening CeilingTools.cs:404 | **REFINAR** | Manter; tratar furos sobrepostos. |
| `ergo.check` | Ergonomia e acessibilidade | Ergonomia/acessibilidade com regras editáveis | nenhum | Exclusivo | `Interiors/ErgonomicsTool.cs:109` — Pegadas aproximadas; APIs sensíveis: FilledRegion ErgonomicsTool.cs:224 | **REFINAR** | Manter; sem declarar conformidade. |
| `furn.place` | Inserir mobiliário | Insere móveis por cliques | Componente (CM) | Pouco | `Interiors/FurnitureTools.cs:54` — PromptForFamilyInstancePlacement e orientação por caixa; APIs sensíveis: PromptPlacement FurnitureTools.cs:70 | **REMOVER** | Inserção nativa + 'Encostar na parede' cobrem. |
| `furn.distribute` | Distribuir ao longo de parede/linha | Distribui móveis ao longo de parede/linha | Matriz (sem afastamento/orientação) | Afastamento e orientação | `Interiors/FurnitureTools.cs:177` — APIs sensíveis: Copy FurnitureTools.cs:238 | **REFINAR** | Manter. |
| `furn.alignwall` | Encostar na parede | Encosta o móvel na parede, de costas | nenhum | Exclusivo | `Interiors/FurnitureTools.cs:266` — Pegada por envoltória convexa | **REFINAR** | Manter. |
| `furn.presetsave` | Salvar layout de mobiliário | Salva layout de mobiliário | Grupo de modelo (parcial) | Layouts entre projetos | `Interiors/FurnitureTools.cs:354` | **MANTER** | Útil. |
| `furn.presetapply` | Aplicar layout de mobiliário | Aplica layout salvo | Grupo de modelo | Sem as restrições de grupo | `Interiors/FurnitureTools.cs:404` — APIs sensíveis: Mirror FurnitureTools.cs:445 | **MANTER** | Útil. |
| `furn.clearance` | Áreas de uso do mobiliário | Áreas de uso e conflitos | nenhum | Exclusivo | `Interiors/FurnitureTools.cs:480` — APIs sensíveis: FilledRegion FurnitureTools.cs:526 | **MANTER** | Fluxo de interiores. |
| `furn.list` | Lista de mobiliário por ambiente | Lista de mobiliário por ambiente | Tabela (parcial) | Grava o ambiente em cada peça | `Interiors/FurnitureTools.cs:570` | **MANTER** | Útil. |
| `light.grid` | Luminárias em grade | Luminárias em grade / método dos lúmens | nenhum | Exclusivo | `Interiors/LightingTools.cs:180` | **MANTER** | Útil. |
| `light.overfurniture` | Luminárias sobre mobiliário | Luminárias centralizadas sobre móveis | nenhum | Exclusivo | `Interiors/LightingTools.cs:302` | **MANTER** | Útil. |
| `light.groups` | Grupos e circuitos de iluminação | Grupos/circuitos com cores | nenhum | Exclusivo | `Interiors/LightingTools.cs:372` | **MANTER** | Útil. |
| `room.create` | Criar ambientes automaticamente | Cria ambientes em todos os espaços fechados | Colocar ambientes automaticamente (nativo) | Só a numeração inicial | `Interiors/RoomTools.cs:51` | **REMOVER** | O comando nativo faz o mesmo; numeração fica com 'Numerar ambientes'. |
| `room.number` | Numerar ambientes | Renumera em sequência (cliques, posição, nível) | nenhum em sequência | Exclusivo | `Interiors/RoomTools.cs:127` | **MANTER** | Sem equivalente. |
| `room.rename` | Padronizar nomes e códigos | Padroniza nomes e códigos | nenhum | Exclusivo | `Interiors/RoomTools.cs:219` | **MANTER** | Útil em lote. |
| `room.params` | Parâmetros de ambientes | Exporta/importa ambientes por planilha | nenhum | Exclusivo | `Interiors/RoomTools.cs:282` | **MANTER** | Edição em massa via Excel. |
| `room.schedule` | Tabela de ambientes | Cria tabela de ambientes | Nova tabela (nativo) | Pouco | `Interiors/RoomTools.cs:399` | **FUNDIR com qty.schedules** | Fica como opção das Tabelas de quantitativos. |
| `room.check` | Verificar ambientes | Ambientes não colocados/redundantes/duplicados | Avisos do Revit (parcial) | Relatório consolidado | `Interiors/RoomTools.cs:438` | **MANTER** | Útil. |
| `room.views` | Vistas por ambiente | Planta recortada + 4 elevações por ambiente | nenhum em lote | Exclusivo | `Interiors/RoomTools.cs:491` | **MANTER** | Grande ganho em interiores. |
| `views.organize` | Organizar vistas por setor | Agrupa vistas por setor (TT_Grupo) | Organização do navegador (exige parâmetro) | Preenche o parâmetro por regras | `Interiors/RoomTools.cs:602` | **MANTER** | Útil. |
| `wall.paint` | Pintar paredes do ambiente | Pinta faces voltadas ao ambiente | Pintar (face a face) | Lote por ambiente | `Interiors/WallTools.cs:31` — APIs sensíveis: Paint WallTools.cs:49 | **MANTER** | Útil. |
| `wall.finish` | Revestimento de parede por ambiente | Revestimento de parede recortando vãos | nenhum | Exclusivo | `Interiors/WallTools.cs:307` — APIs sensíveis: WallCreate WallTools.cs:358 | **MANTER** | Útil. |
| `wall.slats` | Ripado / painel ripado | Ripado paramétrico | nenhum | Exclusivo | `Interiors/WallTools.cs:456` | **MANTER** | Útil. |
| `wall.boiserie` | Boiserie / molduras | Boiserie/molduras | nenhum | Exclusivo | `Interiors/WallTools.cs:650` | **MANTER** | Útil. |

## Marcenaria

| id | Nome | O que faz | Equivalente nativo | Valor adicional | Riscos / código | Decisão | Justificativa |
|---|---|---|---|---|---|---|---|
| `join.module` | Módulo de marcenaria | Módulo paramétrico com dados de corte | nenhum | Núcleo do módulo | `Joinery/JoineryTools.cs:175` — Cria família em tempo de execução (FamilyFactory) | **REFINAR** | Manter. |
| `join.run` | Compor módulos na parede | Composição de módulos na parede | nenhum | Exclusivo | `Joinery/JoineryTools.cs:254` | **MANTER** | Manter. |
| `join.edit` | Editar módulo | Edita módulo no lugar | nenhum | Exclusivo | `Joinery/JoineryTools.cs:405` | **MANTER** | Manter. |
| `join.views` | Detalhamento do módulo | Vistas do conjunto, 3D explodida, prancha | Vistas de conjunto (sem explodida/prancha automática) | Prancha pronta | `Joinery/JoineryTools.cs:455` — DisplacementElement (JoineryTools.cs:536); APIs sensíveis: Displacement JoineryTools.cs:536, Assembly JoineryTools.cs:465 | **REFINAR** | Manter. |
| `join.cutlist` | Plano de corte e ferragens | Plano de corte, ferragens, Excel/CSV | nenhum | Exclusivo | `Joinery/JoineryTools.cs:586` | **MANTER** | Manter. |
| `join.nesting` | Otimizar chapas (nesting) | Otimização de chapas | nenhum | Exclusivo | `Joinery/JoineryTools.cs:702` | **MANTER** | Manter. |
| `join.rules` | Regras de marcenaria | Regras de ferragens e folgas | nenhum | Exclusivo | `Joinery/JoineryTools.cs:859` | **MANTER** | Manter. |

## Materiais

| id | Nome | O que faz | Equivalente nativo | Valor adicional | Riscos / código | Decisão | Justificativa |
|---|---|---|---|---|---|---|---|
| `mat.library` | Biblioteca de materiais | Biblioteca pessoal de materiais | Navegador de materiais (sem edição em planilha) | Planilha e entre projetos | `Materials/MaterialTools.cs:217` | **MANTER** | Útil. |
| `mat.apply` | Aplicar material | Aplica material por parâmetro ou pintura | Propriedades / Pintar | Pouco | `Materials/MaterialTools.cs:307` — Pintura em faces de instâncias costuma falhar; APIs sensíveis: Paint MaterialTools.cs:334 | **REMOVER** | Edição múltipla e Pintar nativos cobrem. |
| `mat.byroom` | Acabamentos por ambiente | Acabamentos por ambiente por regras | nenhum | Exclusivo | `Materials/MaterialTools.cs:385` — APIs sensíveis: Paint MaterialTools.cs:418 | **MANTER** | Útil. |
| `mat.replace` | Substituir material | Troca material em todo o projeto | nenhum | Exclusivo | `Materials/MaterialTools.cs:463` — Varre todas as faces do modelo (lento em modelos grandes); APIs sensíveis: Paint MaterialTools.cs:544 | **REFINAR** | Manter; mostrar progresso/limitar escopo. |
| `mat.specs` | Ficha técnica de materiais | Ficha técnica de materiais | nenhum | Exclusivo | `Materials/MaterialTools.cs:584` — Varredura de todo o modelo (MaterialTools.cs:166) | **MANTER** | Útil. |
| `mat.report` | Relatório de uso de materiais | Área/volume por material | Tabela de levantamento de material | Pouco | `Materials/MaterialTools.cs:640` | **FUNDIR com mat.specs** | Uma ferramenta de materiais em vez de duas. |

## Documentação

| id | Nome | O que faz | Equivalente nativo | Valor adicional | Riscos / código | Decisão | Justificativa |
|---|---|---|---|---|---|---|---|
| `doc.dims` | Cotas automáticas | Cotas internas de ambientes / paredes com vãos | Cota alinhada 'paredes inteiras com aberturas' (nativa) | Medidas internas de ambientes em lote | `Documentation/DocAnnotate.cs:39` — APIs sensíveis: Dimension DocAnnotate.cs:72 | **REFINAR** | Manter só as cotas internas de ambientes; paredes com vãos é nativo. |
| `doc.tags` | Etiquetar tudo na vista | Etiqueta tudo na vista | Etiquetar todos não etiquetados (nativo) | — | `Documentation/DocAnnotate.cs:229` | **REMOVER** | Duplicado exato. |
| `doc.legend` | Legenda de acabamentos e cores | Legenda de acabamentos/cores | Legenda (manual) | Gerada automaticamente | `Documentation/DocAnnotate.cs:307` | **MANTER** | Útil. |
| `doc.notes` | Notas padrão | Biblioteca de notas padrão | Notas-chave (parcial) | Simples e entre projetos | `Documentation/DocAnnotate.cs:431` | **MANTER** | Útil. |
| `doc.pdf` | Exportar PDF | Exporta PDF | Exportar PDF (nativo, com regras de nome e combinação) | — | `Documentation/DocExport.cs:95` — APIs sensíveis: Export DocExport.cs:130 | **REMOVER** | O exportador nativo do Revit 2027 faz o mesmo. |
| `doc.dwg` | Exportar DWG | Exporta DWG | Exportar CAD (nativo) | — | `Documentation/DocExport.cs:173` — APIs sensíveis: Export DocExport.cs:203 | **REMOVER** | Duplicado. |
| `doc.sheets` | Criar pranchas em lote | Cria pranchas a partir de lista/CSV | Nova prancha (uma a uma) | Lote | `Documentation/DocSheets.cs:54` | **MANTER** | Útil. |
| `doc.placeviews` | Montar pranchas automaticamente | Monta pranchas sem sobreposição | nenhum | Exclusivo | `Documentation/DocSheets.cs:163` | **MANTER** | Útil. |
| `doc.renumber` | Renumerar pranchas e detalhes | Renumera pranchas e detalhes | nenhum em sequência | Exclusivo | `Documentation/DocSheets.cs:255` | **MANTER** | Útil. |
| `doc.titleblock` | Dados do carimbo | Dados do carimbo e troca de carimbo | Informações do projeto + seleção múltipla de pranchas | Pouco | `Documentation/DocSheets.cs:343` | **REMOVER** | Nativo cobre. |
| `doc.sheetindex` | Índice de pranchas | Índice de pranchas | Lista de folhas (nativa) | — | `Documentation/DocSheets.cs:417` | **REMOVER** | Duplicado. |
| `doc.plans` | Conjunto de plantas | Conjunto de plantas por nível | Vista ▸ Planta (sem conjuntos/nomes) | Lote com modelos e recorte | `Documentation/DocViews.cs:90` | **MANTER** | Útil. |
| `doc.sections` | Cortes por ambiente | Cortes por ambiente | Corte (manual) | Lote | `Documentation/DocViews.cs:150` | **MANTER** | Útil. |
| `doc.axo` | Axonométricas (3D) cortadas | 3D cortado por ambiente/nível | 3D + caixa de corte (manual) | Lote | `Documentation/DocViews.cs:244` | **MANTER** | Útil. |
| `doc.viewsettings` | Configurar vistas em lote | Escala/detalhe/estilo em várias vistas | Seleção múltipla no navegador + Propriedades | — | `Documentation/DocViews.cs:361` | **REMOVER** | Nativo faz em lote. |

## Quantitativos

| id | Nome | O que faz | Equivalente nativo | Valor adicional | Riscos / código | Decisão | Justificativa |
|---|---|---|---|---|---|---|---|
| `qty.schedules` | Tabelas de quantitativos | Conjunto de tabelas de quantitativos | Nova tabela (uma a uma) | Lote | `Quantities/QuantityTools.cs:201` | **MANTER** | Recebe também a tabela de ambientes. |
| `qty.export` | Levantamento de quantitativos | Levantamento em Excel | Exportar tabela (uma a uma) | Consolidado por ambiente | `Quantities/QuantityTools.cs:269` | **MANTER** | Útil. |
| `qty.budget` | Orçamento | Orçamento com tabela de preços | nenhum | Exclusivo | `Quantities/QuantityTools.cs:322` | **MANTER** | Útil. |
| `qty.snapshot` | Comparar quantitativos (antes × depois) | Comparação antes × depois | nenhum | Exclusivo | `Quantities/QuantityTools.cs:399` | **MANTER** | Útil. |

## Qualidade

| id | Nome | O que faz | Equivalente nativo | Valor adicional | Riscos / código | Decisão | Justificativa |
|---|---|---|---|---|---|---|---|
| `qa.check` | Verificação de qualidade | Verificação com regras + saúde do modelo | Revisar avisos (parcial) | Regras editáveis | `Quality/QualityTools.cs:197` | **MANTER** | Recebe as verificações de geometria. |
| `qa.rules` | Regras de verificação | Edita regras em planilha | nenhum | Exclusivo | `Quality/QualityTools.cs:327` | **MANTER** | Útil. |

## Projeto

| id | Nome | O que faz | Equivalente nativo | Valor adicional | Riscos / código | Decisão | Justificativa |
|---|---|---|---|---|---|---|---|
| `proj.settings` | Configurações do projeto | Dados do projeto no arquivo | Informações do projeto (nativo) | Pouco | `Project/ProjectTools.cs:38` | **REMOVER** | Informações do projeto nativas cobrem. |
| `proj.presets` | Presets e bibliotecas | Gerencia presets | nenhum | Exclusivo | `Project/ProjectTools.cs:98` — APIs sensíveis: Export ProjectTools.cs:123 | **MANTER** | Útil. |
| `proj.transfer` | Transferir padrões entre projetos | Transfere padrões e configurações Téra | Transferir padrões de projeto (nativo) | Configurações do Téra Tools | `Project/ProjectTools.cs:207` — Duplica a parte nativa; APIs sensíveis: Copy ProjectTools.cs:253 | **REFINAR** | Manter só a transferência das configurações do Téra Tools. |
| `proj.batch` | Processamento em lote | Lote de arquivos .rvt | nenhum | Exclusivo | `Project/ProjectTools.cs:304` — Abre documentos em segundo plano; APIs sensíveis: Export ProjectTools.cs:382, OpenDoc ProjectTools.cs:346 | **REFINAR** | Manter; garantir o fechamento dos arquivos mesmo após falhas e detalhar o relatório. |

## Ferramentas a REMOVER

- `modeling.align` — O Alinhar nativo usa referências reais e é exato.
- `modeling.arraylinear` — A Matriz nativa cobre o caso e é associativa/editável.
- `modeling.arrayradial` — Duplica a Matriz radial nativa.
- `modeling.copy` — Nativo faz o mesmo com precisão.
- `modeling.mirror` — Duplicado do nativo.
- `modeling.elevation` — Edição múltipla nas Propriedades faz o mesmo.
- `modeling.resize` — Edição múltipla nativa é mais segura.
- `modeling.group` — Duplicado.
- `modeling.ungroup` — Duplicado.
- `modeling.replacetype` — O Seletor de tipo já troca dentro da categoria preservando parâmetros de instância.
- `modeling.refline` — Duplicado.
- `selection.similar` — Duplicado exato.
- `selection.isolate` — Duplicado exato.
- `selection.hide` — Duplicado exato.
- `selection.reveal` — Duplicado.
- `analysis.clash` — O nativo é mais confiável e gera relatório próprio.
- `analysis.measure` — Quantitativos e tabelas cobrem com mais controle.
- `analysis.distance` — Duplicado exato.
- `gen.viewstyle` — Modelos de vista nativos fazem isso e são reutilizáveis.
- `geo.join` — Duplicado do nativo.
- `geo.unjoin` — Duplicado.
- `geo.cut` — Duplicado.
- `geo.boolean` — Alto risco e resultado não-BIM.
- `geo.parts` — A Paginação já faz isso com juntas, layouts e ponto de partida.
- `geo.opening` — Duplicado do nativo.
- `geo.solid` — O nativo gera geometria editável.
- `geo.thicken` — Resultado não-BIM; nativo resolve.
- `geo.simplify` — Sem uso relevante no fluxo de interiores.
- `geo.offsetface` — Duplicado.
- `room.create` — O comando nativo faz o mesmo; numeração fica com 'Numerar ambientes'.
- `furn.place` — Inserção nativa + 'Encostar na parede' cobrem.
- `ceil.rcp` — Nativo cria em lote; 'Conjunto de plantas' também cria plantas de forro.
- `mat.apply` — Edição múltipla e Pintar nativos cobrem.
- `doc.viewsettings` — Nativo faz em lote.
- `doc.titleblock` — Nativo cobre.
- `doc.sheetindex` — Duplicado.
- `doc.pdf` — O exportador nativo do Revit 2027 faz o mesmo.
- `doc.dwg` — Duplicado.
- `doc.tags` — Duplicado exato.
- `proj.settings` — Informações do projeto nativas cobrem.

## Ferramentas a FUNDIR

- `general.history` → general.pane — O painel já mostra o histórico; um botão a menos na faixa.
- `general.logs` → general.about — Vira um botão dentro do Diagnóstico.
- `gen.kineticstate` → gen.edit — Um único comando de edição basta.
- `geo.validate` → qa.check — Uma verificação única é mais clara.
- `room.schedule` → qty.schedules — Fica como opção das Tabelas de quantitativos.
- `mat.report` → mat.specs — Uma ferramenta de materiais em vez de duas.

## Ferramentas a REFINAR (Fase 4)

- `modeling.distribute` — Sem equivalente nativo; precisa passar a usar o ponto de inserção/linha de locação.
- `modeling.rotate` — Girar cada peça no próprio ponto de inserção não existe no Revit; ajustar para usar o ponto de inserção.
- `selection.filter` — Não existe nativo equivalente; refinar mensagens e casos vazios.
- `gen.pattern` — Manter; limitar quantidade de furos e melhorar pré-visualização.
- `gen.image` — Manter; mensagens de limite mais claras.
- `gen.brise` — Manter.
- `gen.spaceframe` — Manter; validar geometria antes de criar.
- `gen.kinetic` — Manter.
- `geo.convert` — Ganho real de produtividade; refinar validação de laços.
- `floor.byroom` — Manter; refinar escolha de contorno (acabamento/centro).
- `floor.regions` — Manter; preservar parâmetros do piso original.
- `floor.paging` — Manter; limite e aviso de desempenho.
- `floor.baseboard` — Manter; refinar cantos.
- `furn.distribute` — Manter.
- `furn.alignwall` — Manter.
- `ergo.check` — Manter; sem declarar conformidade.
- `ceil.opening` — Manter; tratar furos sobrepostos.
- `join.module` — Manter.
- `join.views` — Manter.
- `mat.replace` — Manter; mostrar progresso/limitar escopo.
- `doc.dims` — Manter só as cotas internas de ambientes; paredes com vãos é nativo.
- `proj.transfer` — Manter só a transferência das configurações do Téra Tools.
- `proj.batch` — Manter; garantir o fechamento dos arquivos mesmo após falhas e detalhar o relatório.

## Resultado depois da aprovação (Fases 3 e 4)

O que foi aplicado difere da proposta acima em três pontos:

- `gen.viewstyle` **não foi removido**: virou o **Gerador de temas**, que aplica de uma vez estilo visual, sombras, linhas de croqui, fundo e cores por categoria, e trabalha em várias vistas ou em cópias delas. Isso o modelo de vista nativo não faz num passo só.
- `floor.uncovered` (Ambientes sem revestimento) foi substituído por **Preencher pisos (Floor Filler)**, que lista as áreas descobertas **e** pode criar o piso que falta.
- Entrou **Unir pisos (Floor Merger)**, que não tem equivalente nativo.

Resultado: **80 ferramentas em 11 módulos**. A versão 0.3.0 acrescentou o catálogo de acabamentos (3 ferramentas) e a marcenaria livre: **84 ferramentas**. A versão 0.4.0 acrescentou o módulo Orgânicos (colunas, coberturas e lâminas de fachada), as referências visuais e o autoteste: **89 ferramentas em 12 módulos** (a lista completa está em [FERRAMENTAS.md](FERRAMENTAS.md)).

Refinamentos da Fase 4 já feitos:

- `modeling.distribute`: mede pelo ponto de inserção (exato) ou pelos vértices da geometria, nunca pela caixa envolvente; ignora elementos fixados; mostra antes e depois.
- `modeling.rotate`: gira cada elemento no próprio ponto de inserção; ignora elementos fixados.
- `gen.pattern`: reescrito no modelo do Pixy (9 padrões, furos reais ou relevo, fade, aleatoriedade, limite de quantidade), com pré-visualização 2D e 3D.
- `gen.spaceframe`, `gen.image`, `gen.brise`, `gen.kinetic`, `wall.slats`, `wall.boiserie`, `join.module`, `join.run`, `ceil.cove` e `light.grid`: pré-visualização 3D.
- `floor.paging`: recusa divisões grandes demais, que travariam o Revit.
- `ceil.opening`: ignora furos sobrepostos.
- `doc.dims`: só cotas internas de ambientes.
- `proj.transfer`: só as configurações do Téra Tools.
