# Desenvolvimento do Téra Tools

Guia técnico para quem for manter ou ampliar o plugin.

## Estrutura

```
TeraTools.sln
Directory.Build.props        versão, idioma C#, versão do Revit e dos pacotes da API
src/
  TeraTools.Core/            núcleo sem dependência do Revit (net10.0), testável
    Geometry/                Vec2/Vec3, polígonos, regiões, caminhos, offsets, recortes
    Mathx/ Units/ Text/      matemática, expressões, unidades (pés ↔ mm/m), nomes e numeração
    Localization/            Loc.T(pt, en): textos bilíngues
    Serialization/ Settings/ JSON, presets, configurações globais e do projeto
    Reporting/ Export/       relatório de operação, histórico, CSV, XLSX, HTML, OBJ
    Patterns/ Imaging/ Facade/ Structure/ Kinetic/   algoritmos dos geradores
    Tiling/ Layout/ Checks/  paginação, distribuição em grade, ergonomia, motor de regras
    Joinery/ Nesting/        construtor de módulos, lista de corte, otimizador de chapas
    Costing/ Materials/ Documentation/   orçamento, biblioteca de materiais, empacotamento de vistas
  TeraTools.Revit/           add-in (net10.0-windows, WPF), saída TeraTools.dll
    App.cs                   inicialização: catálogo, faixa de opções, painel, tema
    Infrastructure/          comandos, contexto, transações, consultas, geometria Revit, tabelas, filtros
    Families/                parâmetros compartilhados, famílias TT_Bloco/TT_Chapa geradas, BoxSink
    Ui/                      diálogo de opções genérico, relatório, painel, ícones, temas, ajuda
    Tools/<Módulo>/          as ferramentas
tests/TeraTools.Core.Tests/  testes xUnit do núcleo
deploy/                      TeraTools.addin, Instalar.bat, Desinstalar.bat
tools/                       scripts auxiliares (gera docs/FERRAMENTAS.md)
.github/workflows/build.yml  compila, testa e publica o .zip (Artifacts; Release em tags v*)
```

## Compilar

Requisitos: .NET SDK 10. A API do Revit vem dos pacotes `Nice3point.Revit.Api.RevitAPI` e `RevitAPIUI` 2027.x, usados só como referência. O Revit não precisa estar instalado para compilar.

```bash
dotnet test tests/TeraTools.Core.Tests/TeraTools.Core.Tests.csproj
```

```bash
dotnet build src/TeraTools.Revit/TeraTools.Revit.csproj -c Release
```

O pacote pronto para instalar sai em `dist/TeraTools-Revit2027/`. No Windows, em Debug, o build também copia o add-in para `%AppData%\Autodesk\Revit\Addins\2027`.

## Como criar uma ferramenta

1. Crie a classe em `src/TeraTools.Revit/Tools/<Módulo>/`:

```csharp
public sealed class MinhaOpcoes
{
    [Opt("Distância", "Distance", Kind = OptKind.Length, Min = 0)]
    public double Distancia { get; set; } = U.Mm(100);
}

[Transaction(TransactionMode.Manual)]
[Tool("mod.minha", Modules.Modeling, "Minha ferramenta", "My tool", Order = 50,
    DescPt = "O que faz.", DescEn = "What it does.", Keywords = "palavras de busca")]
public sealed class MinhaCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = ctx.SelectedOrPick("Selecione", "Select");
        var o = ctx.Ask<MinhaOpcoes>();               // diálogo com presets e últimas opções
        ctx.Modify(ctx.Tool.Name, () =>               // uma transação = um Desfazer
        {
            // ... alterações ...
            ctx.Report.Created();
        });
    }
}
```

2. Pronto: a faixa de opções, o painel, a busca, a página de ajuda, o histórico e o tratamento de erros são gerados automaticamente a partir do atributo `[Tool]`.
3. Opcional: desenhe um ícone em `Ui/Icons.cs`, com a mesma chave do id.
4. Rode `python3 tools/gerar_lista_ferramentas.py .` para atualizar `docs/FERRAMENTAS.md`.

### Convenções

- **Unidades:** internamente, tudo em pés (unidade do Revit). Use `U.Mm()`, `U.M()`, `U.ToMm()` e similares. Campos `OptKind.Length` aparecem e são digitados sempre em **mm** (aceitam `m` e `cm` escritos). A interface é sempre em português (`Loc.T(pt, en)` continua bilíngue no código).
- **Falhas por item:** nunca engula exceções de criação. Use `ctx.ItemFailed("o quê", "what", ex)` (agrupa no relatório com o motivo) e `ctx.RequireCreated(n, ...)` para terminar com mensagem clara quando nada foi criado.
- **Contornos para o Revit:** use `Polygon2.CleanForSketch(tol)` ou funda vértices; nunca pule um segmento curto (o laço fica aberto e o Revit recusa).
- **Textos:** sempre bilíngues: `Loc.T("pt", "en")` e `Loc.F(...)` com parâmetros. Enums do add-in usam `[Text(pt, en)]`; enums do núcleo são registrados em `Ui/EnumTexts.cs`.
- **Erros esperados:** use `ToolContext.Fail(pt, en)` ou `throw new ToolException(...)`: mensagem simples, sem detalhes técnicos. Exceções inesperadas abrem a janela de erro com "Copiar detalhes".
- **Cancelamento:** `ctx.Cancel()`. Esc durante a seleção também cancela.
- **Volume:** chame `ctx.CheckCount(n, ...)` antes de criar muitos elementos.
- **Várias transações em um único Desfazer:** use `ctx.Group(...)`.
- **APIs:** confira na documentação da API do Revit 2027 (`RevitAPI.xml`). Por exemplo, `ElementId.IntegerValue` não existe mais; use `.Value`.

### Sistemas gerados

Geradores registram opções e contexto com `GenSystem.Register(...)` e marcam os elementos com `GenSystem.Mark(...)` (Extensible Storage e `TT_Sistema`). Para permitir "Editar sistema gerado", adicione um regenerador em `GenRegistry.Regenerators` (`Tools/Generators/KineticTool.cs`).

### Armazenamento no projeto

`ProjectStorage.GetSection<T>(doc, chave)` e `SetSection(...)` guardam JSON dentro do `.rvt`. A gravação precisa de transação. Chaves em uso: `presets`, `systems`, `info`, `themes`, `qa.rules`, `joinery.rules`, `qty.snapshots`, `tt.markers`.

## Testes no Revit

Este código ainda não foi executado no Revit. Roteiro sugerido para a primeira rodada de testes:

1. Abrir o Revit com o add-in e conferir a aba, o painel e as páginas de ajuda.
2. Num projeto de teste com alguns ambientes, rodar uma ferramenta de cada módulo.
3. Para cada erro, copiar os detalhes da janela de erro e registrar junto com os passos para reproduzir.
