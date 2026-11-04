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

Tudo fica em `%AppData%\TeraTools` (use **Geral ▸ Abrir pasta de dados**):

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
   - **Medidas:** um número sozinho usa a unidade do projeto. Você também pode escrever a unidade (`1,20 m`, `120 cm`, `1200 mm`).
   - **Pré-visualização:** muitos diálogos mostram um desenho do resultado que se atualiza enquanto você muda as opções.
   - **Presets:** salve as opções com um nome para reutilizar. Um preset pode ser **pessoal** (só no seu computador), **do projeto** (fica dentro do .rvt e vai junto com o arquivo) ou de **biblioteca da equipe** (somente leitura, numa pasta compartilhada).
   - O plugin lembra as **últimas opções** que você usou em cada ferramenta.
3. **Execução:** tudo vira **um único Desfazer**. Avisos do Revit que apareceriam dezenas de vezes são recolhidos e listados no relatório.
4. **Relatório:** mostra contagens (criados, modificados, ignorados), avisos, erros e tabelas. Nele você pode:
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

Padrões, mosaicos, brises, quadros espaciais, painéis cinéticos, ripados, boiseries e módulos de marcenaria ficam **registrados no projeto** com as opções usadas. Para alterar:

- **Geradores ▸ Editar sistema gerado:** selecione uma peça. As opções originais reabrem e o sistema é refeito no mesmo lugar.
- **Marcenaria ▸ Editar módulo:** faz o mesmo para módulos de marcenaria.

### Parâmetros compartilhados TT_*

O plugin cria e vincula sozinho os parâmetros `TT_` de que precisa: código, ambiente, grupo, sistema, status e outros. Use-os em tabelas e filtros como qualquer parâmetro do Revit.

---

## 4. Fluxos de trabalho sugeridos

### Projeto de interiores, do zero

1. **Interiores ▸ Criar ambientes automaticamente**, depois **Numerar ambientes** e **Padronizar nomes e códigos**.
2. **Materiais ▸ Biblioteca de materiais:** crie no projeto os materiais que você usa.
3. **Materiais ▸ Acabamentos por ambiente:** regras como "Banh* → porcelanato" preenchem piso, parede, forro e rodapé de cada ambiente.
4. **Pisos:** use **Piso por ambiente**, depois **Paginação de revestimento** (peças reais com juntas e ponto de partida) e **Rodapés automáticos**.
5. **Interiores ▸ Forros:** **Forro por ambiente**, ou **Sanca e rebaixo** para forros com rebaixo perimetral e fenda de luz.
6. **Interiores ▸ Paredes:** **Revestimento de parede** (barrados e revestimentos que recortam portas e janelas), **Ripado** e **Boiserie**.
7. **Interiores ▸ Mobiliário:** use **Inserir mobiliário**, **Distribuir**, **Encostar na parede** e **Layouts salvos**; depois confira com **Áreas de uso**.
8. **Interiores ▸ Ergonomia e acessibilidade:** ajuste as regras de referência para o seu projeto.
9. **Interiores ▸ Iluminação:** use **Luminárias em grade** (por espaçamento ou lux estimado), **Sobre mobiliário** e **Grupos e circuitos**.
10. **Documentação:** use **Conjunto de plantas**, **Vistas por ambiente** (planta e quatro elevações), **Montar pranchas**, **Cotas** e **Etiquetas**, e por fim **Exportar PDF**.
11. **Quantitativos:** use **Levantamento** e **Orçamento**.
12. **Qualidade ▸ Verificação de qualidade:** rode antes de cada entrega.

### Marcenaria planejada

1. **Regras de marcenaria:** defina dobradiças por altura, corrediças e folgas. As regras ficam salvas no projeto.
2. **Módulo de marcenaria** para peças avulsas, ou **Compor módulos na parede** para dividir uma parede em módulos equilibrados, com complemento e tampo contínuo.
3. **Editar módulo** para mudar medidas, frentes, gavetas e materiais.
4. **Detalhamento do módulo:** frontal, lateral, planta, corte, 3D, 3D explodida, lista de peças e prancha montada.
5. **Plano de corte e ferragens:** lista de peças com medidas de corte, fitas e veio; consumo de chapas e fitas; ferragens; Excel e CSV de fabricação.
6. **Otimizar chapas (nesting):** distribui as peças nas chapas e desenha os planos de corte.

### Geradores paramétricos

- **Gerador de padrões:** grade, desencontrado, losango, triangular, hexagonal, radial, espiral e aleatório controlado, com variação por gradiente linear ou radial, ruído, fórmula ou imagem. Funciona em face plana ou curva, retângulo ou ambiente.
- **Imagem para blocos:** transforma uma imagem em mosaico de peças, com relevo pela tonalidade e cores convertidas em materiais.
- **Aletas e brises:** ao longo de parede ou linha, com rotação variável (linear, onda, aleatória, atrator ou fórmula) e interrupção nas janelas.
- **Quadro espacial** e **Painéis cinéticos**: este último tem estados de abertura (atualize com "Atualizar estado cinético").
- **Temas:** **Paleta de cores**, **Colorir por parâmetro** (filtros e legenda) e **Estilo de apresentação**.

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
