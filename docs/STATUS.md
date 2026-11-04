# O que foi feito do pedido original

Legenda: ✅ implementado · 🟡 implementado com limites (explicados) · ⏳ planejado para depois

Lembrete: tudo foi compilado contra a API do Revit 2027, mas **ainda não foi executado dentro do Revit**. Os primeiros testes reais podem pedir ajustes.

## 1. Interface e gerenciamento do plugin

| Item | Situação | Onde |
|---|---|---|
| Aba própria na faixa de opções | ✅ | Aba **Téra Tools**, com um painel por módulo |
| Painel lateral (dockable) | ✅ | **Geral ▸ Painel Téra** |
| Organização por módulos e categorias | ✅ | 14 módulos; menus agrupados por assunto |
| Ícones próprios | ✅ | Ícones vetoriais desenhados para cada ferramenta, adaptados ao tema claro/escuro do Revit |
| Busca de ferramentas | ✅ | Painel Téra (com ou sem acento, por nome ou palavra-chave) |
| Favoritos e recentes | ✅ | Painel Téra |
| Configurações globais e por projeto | ✅ | **Geral ▸ Configurações** e **Projeto ▸ Configurações do projeto** |
| Presets reutilizáveis | ✅ | Em todos os diálogos de opções: pessoal, do projeto ou da equipe |
| Salvar e carregar configurações | ✅ | **Projeto ▸ Presets e bibliotecas** (exportar e importar arquivos) |
| Mensagens de erro claras e relatórios de execução | ✅ | Relatório ao final de cada operação; janela de erro com "Copiar detalhes" |
| Histórico de operações | ✅ | **Geral ▸ Histórico** e aba Histórico do painel |
| Português e inglês | ✅ | Automático pelo idioma do Revit, ou escolhido nas configurações |
| Ajuda contextual | ✅ | Página de ajuda por ferramenta (botão **Ajuda** ou **F1**) e **Geral ▸ Ajuda** |

## 2. Ferramentas gerais de modelagem

| Item | Situação | Onde |
|---|---|---|
| Alinhar, distribuir | ✅ | Modelagem |
| Matrizes linear/retangular e radial | ✅ | Modelagem |
| Copiar com deslocamento e rotação | ✅ | Modelagem ▸ Copiar com deslocamento |
| Espelhar, rotacionar | ✅ | Modelagem |
| Elevação / posição | ✅ | Modelagem ▸ Posição e elevação |
| Redimensionar | ✅ | Modelagem ▸ Redimensionar |
| Agrupar e desagrupar | ✅ | Modelagem |
| Substituir família/tipo mantendo parâmetros | ✅ | Modelagem ▸ Substituir família/tipo |
| Selecionar por categoria, família, tipo, material ou parâmetro | ✅ | Seleção ▸ Filtro avançado, Selecionar semelhantes |
| Isolar, ocultar, revelar | ✅ | Seleção |
| Filtrar por ambiente, nível ou vista | ✅ | Seleção ▸ Filtro avançado, Elementos do ambiente |
| Verificação de colisões | ✅ | Análise ▸ Verificar colisões |
| Medir | ✅ | Análise ▸ Medir seleção, Medir distância |
| Pontos e linhas de referência | ✅ | Modelagem ▸ Pontos e linhas auxiliares |

## 3. Geradores paramétricos (inspirados no Pixy Tools)

| Item | Situação | Onde |
|---|---|---|
| Padrões paramétricos | ✅ | Geradores ▸ Gerador de padrões (9 malhas, variação por gradiente, ruído, fórmula ou imagem) |
| Imagem para blocos | ✅ | Geradores ▸ Imagem para blocos (relevo e cores em materiais) |
| Aletas e brises | ✅ | Geradores ▸ Aletas e brises |
| Quadros espaciais | ✅ | Geradores ▸ Quadro espacial |
| Cinético | 🟡 | Painéis cinéticos com estados paramétricos de abertura. Animação em tempo real e simulação física são escopos separados, como previsto no pedido |
| Temas e acabamentos visuais | ✅ | Paleta de cores, Colorir por parâmetro (filtros e legenda), Estilo de apresentação |
| Resultado como elemento BIM editável | ✅ | Famílias paramétricas TT_Bloco/TT_Chapa e **Editar sistema gerado** |

## 4. Modificadores geométricos

| Item | Situação | Onde |
|---|---|---|
| Booleanas | 🟡 | Unir, desunir e recortar elementos nativos; booleanas completas em forma livre. Dependem do tipo de elemento, como previsto no pedido; o plugin avisa quando não é possível |
| Partes | ✅ | Geometria ▸ Dividir em partes, Deslocar face de parte |
| Cortes e aberturas | ✅ | Geometria ▸ Recortar com sólido/vazio, Aberturas e vazios |
| Sólidos por perfil | ✅ | Geometria ▸ Sólido por perfil |
| Espessura em face | ✅ | Geometria ▸ Aplicar espessura em face |
| Converter geometria de referência (linhas/CAD) | ✅ | Geometria ▸ Converter linhas/CAD em elementos |
| Simplificar | ✅ | Geometria ▸ Volume simplificado |
| Validar geometria | ✅ | Geometria ▸ Verificar geometria |

## 5. Pisos e revestimentos

| Item | Situação | Onde |
|---|---|---|
| Pisos por contorno ou ambiente | ✅ | Pisos ▸ Piso por ambiente |
| Regiões de piso | ✅ | Pisos ▸ Dividir piso em regiões |
| Paginação com layouts, juntas e ponto de partida | ✅ | Pisos ▸ Paginação de revestimento (peças reais do Revit, por partes) |
| Rodapés | ✅ | Pisos ▸ Rodapés automáticos (recortados nas portas) |
| Faixas, bordas e soleiras | ✅ | Pisos ▸ Faixas, bordas e soleiras |
| Quantitativos (peças, recortes, perdas, caixas) | ✅ | Pisos ▸ Quantitativo de revestimento |
| Ambientes sem acabamento | ✅ | Pisos ▸ Ambientes sem revestimento |
| Vistas e legendas de paginação | ✅ | Pisos ▸ Vistas e tabelas de paginação |

## 6. Interiores

| Item | Situação | Onde |
|---|---|---|
| Ambientes | ✅ | Criar, numerar, padronizar nomes e códigos, parâmetros via planilha, tabela, verificação, vistas por ambiente, organização de vistas |
| Mobiliário e presets de layout | ✅ | Inserir, distribuir, encostar na parede, salvar e aplicar layouts, áreas de uso, lista por ambiente |
| Ergonomia e acessibilidade com regras editáveis | 🟡 | Interiores ▸ Ergonomia e acessibilidade. Regras e nota de referência suas; **nunca declara conformidade**, como pedido. Pegadas dos móveis são aproximadas |
| Iluminação | 🟡 | Grade por espaçamento ou método dos lúmens (estimativa), luminárias sobre mobiliário, grupos e circuitos. Não substitui o cálculo luminotécnico |
| Forros | ✅ | Forro por ambiente, sanca e rebaixo (com fenda de luz), plantas de forro, aberturas |
| Paredes e painéis | ✅ | Pintura por ambiente, revestimento recortando vãos, ripado, boiserie |

## 7. Marcenaria planejada

| Item | Situação | Onde |
|---|---|---|
| Módulos (12 tipos) | ✅ | Marcenaria ▸ Módulo de marcenaria |
| Parametrização e edição | ✅ | Opções completas; **Editar módulo** refaz no lugar |
| Portas, gavetas e ferragens | ✅ | Portas de abrir, duplas, de correr e basculantes; gavetas; dobradiças, corrediças, puxadores, suportes e pés por regras |
| Composição ao longo da parede | ✅ | Marcenaria ▸ Compor módulos na parede |
| Vistas de detalhamento | ✅ | Marcenaria ▸ Detalhamento do módulo (inclui 3D explodida e prancha) |
| Plano de corte e lista de peças | ✅ | Marcenaria ▸ Plano de corte e ferragens |
| Excel e CSV | ✅ | Planilha completa e CSV genérico de fabricação |
| Dados para fabricação | 🟡 | CSV genérico. Formatos específicos de cada máquina CNC **não** são gerados, como previsto no pedido |
| Nesting de chapas | 🟡 | Algoritmo próprio (guilhotina ou livre, com veio, serra e refilo) e planos desenhados. É heurístico: confira com o fornecedor |

## 8. Materiais

| Item | Situação | Onde |
|---|---|---|
| Biblioteca de materiais | ✅ | Materiais ▸ Biblioteca (edição via planilha) |
| Aplicar e substituir | ✅ | Materiais ▸ Aplicar material, Substituir material |
| Acabamentos por ambiente | ✅ | Materiais ▸ Acabamentos por ambiente (regras por nome) |
| Especificação e relatório | ✅ | Ficha técnica de materiais, Relatório de uso |

## 9. Documentação

| Item | Situação | Onde |
|---|---|---|
| Vistas | ✅ | Conjunto de plantas, cortes por ambiente, axonométricas, vistas por ambiente, configuração em lote |
| Pranchas | ✅ | Criar em lote, montar automaticamente, renumerar, dados do carimbo, índice |
| Anotações | ✅ | Cotas automáticas, etiquetar tudo, legendas, notas padrão |
| Exportação PDF e DWG | ✅ | Documentação ▸ Exportar PDF / Exportar DWG |

## 10. Quantitativos e orçamento

| Item | Situação | Onde |
|---|---|---|
| Tabelas de quantitativos | ✅ | Quantitativos ▸ Tabelas de quantitativos |
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
| Configurações do projeto | ✅ | Projeto ▸ Configurações do projeto |
| Presets e bibliotecas | ✅ | Projeto ▸ Presets e bibliotecas |
| Transferência de padrões | ✅ | Projeto ▸ Transferir padrões entre projetos |
| Processamento em lote | ✅ | Projeto ▸ Processamento em lote |

## 13. Recursos avançados

| Item | Situação |
|---|---|
| Recursos avançados | ⏳ Como combinado no pedido, ficam para depois da estabilização dos módulos principais (após os testes reais no Revit) |
