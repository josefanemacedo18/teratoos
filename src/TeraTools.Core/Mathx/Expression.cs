using System.Globalization;
using TeraTools.Core.Localization;

namespace TeraTools.Core.Mathx;

public sealed class ExpressionException : Exception
{
    public int Position { get; }

    public ExpressionException(string message, int position) : base(message) => Position = position;
}

/// <summary>
/// Avaliador de fórmulas seguro (sem execução de código), usado para controlar variações
/// paramétricas: abertura de painéis cinéticos, rotação de aletas, densidade de padrões etc.
///
/// Sintaxe:
///  • Operadores: + - * / % ^, comparações &lt; &gt; &lt;= &gt;= = == != e lógicos and/or/not (&amp;&amp; || !).
///  • Funções trigonométricas usam GRAUS: sin(30) = 0.5.
///  • Separador de argumentos: vírgula; se a fórmula usar ponto-e-vírgula, a vírgula vira decimal
///    (estilo Excel em português): if(i &gt; 3; 0,5; 1).
///  • Funções: sin cos tan asin acos atan atan2 sqrt abs min max floor ceil round pow exp ln log log10
///    clamp lerp remap sign if mod rnd noise step smoothstep deg rad hypot dist.
///  • Constantes: pi, e, tau.
/// </summary>
public sealed class Expression
{
    private readonly Node _root;

    public string Source { get; }

    /// <summary>Variáveis livres referenciadas pela fórmula (sem constantes).</summary>
    public IReadOnlyCollection<string> Variables { get; }

    private Expression(string source, Node root, IReadOnlyCollection<string> vars)
    {
        Source = source;
        _root = root;
        Variables = vars;
    }

    public static Expression Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ExpressionException(Loc.T("Fórmula vazia.", "Empty formula."), 0);
        var semicolonMode = text.Contains(';');
        var tokens = Tokenize(text, semicolonMode);
        var parser = new Parser(tokens);
        var root = parser.ParseExpression();
        if (!parser.AtEnd)
            throw new ExpressionException(Loc.F("Símbolo inesperado '{0}'.", "Unexpected token '{0}'.", parser.Current.Text), parser.Current.Pos);
        var vars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        root.CollectVariables(vars);
        return new Expression(text, root, vars);
    }

    public static bool TryParse(string text, out Expression? expression, out string? error)
    {
        try
        {
            expression = Parse(text);
            error = null;
            return true;
        }
        catch (ExpressionException ex)
        {
            expression = null;
            error = ex.Message;
            return false;
        }
    }

    public double Evaluate(IReadOnlyDictionary<string, double>? variables = null)
    {
        Dictionary<string, double> dict;
        if (variables is Dictionary<string, double> d && ReferenceEquals(d.Comparer, StringComparer.OrdinalIgnoreCase))
        {
            dict = d;
        }
        else
        {
            dict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (variables != null)
                foreach (var kv in variables)
                    dict[kv.Key] = kv.Value;
        }
        return _root.Eval(dict);
    }

    /// <summary>Atalho: avalia uma fórmula com variáveis nomeadas.</summary>
    public static double Eval(string text, params (string Name, double Value)[] vars)
    {
        var d = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (n, v) in vars) d[n] = v;
        return Parse(text).Evaluate(d);
    }

    #region Tokenizer

    private enum TokKind { Number, Ident, Op, LParen, RParen, Comma, End }

    private readonly record struct Token(TokKind Kind, string Text, double Value, int Pos);

    private static List<Token> Tokenize(string s, bool semicolonMode)
    {
        var list = new List<Token>();
        var i = 0;
        while (i < s.Length)
        {
            var ch = s[i];
            if (char.IsWhiteSpace(ch)) { i++; continue; }
            var start = i;
            if (char.IsDigit(ch) || ((ch == '.' || (semicolonMode && ch == ',')) && i + 1 < s.Length && char.IsDigit(s[i + 1])))
            {
                var sb = new System.Text.StringBuilder();
                var seenDot = false;
                while (i < s.Length)
                {
                    var c = s[i];
                    if (char.IsDigit(c)) sb.Append(c);
                    else if ((c == '.' || (semicolonMode && c == ',')) && !seenDot) { sb.Append('.'); seenDot = true; }
                    else if ((c == 'e' || c == 'E') && i + 1 < s.Length &&
                             (char.IsDigit(s[i + 1]) || ((s[i + 1] == '-' || s[i + 1] == '+') && i + 2 < s.Length && char.IsDigit(s[i + 2]))))
                    {
                        sb.Append('e');
                        i++;
                        sb.Append(s[i]);
                    }
                    else break;
                    i++;
                }
                if (!double.TryParse(sb.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    throw new ExpressionException(Loc.F("Número inválido '{0}'.", "Invalid number '{0}'.", sb), start);
                list.Add(new Token(TokKind.Number, sb.ToString(), v, start));
                continue;
            }
            if (char.IsLetter(ch) || ch == '_')
            {
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                var id = s[start..i];
                var lower = id.ToLowerInvariant();
                if (lower is "and" or "or" or "not")
                    list.Add(new Token(TokKind.Op, lower, 0, start));
                else
                    list.Add(new Token(TokKind.Ident, id, 0, start));
                continue;
            }
            switch (ch)
            {
                case '(':
                    list.Add(new Token(TokKind.LParen, "(", 0, i++));
                    continue;
                case ')':
                    list.Add(new Token(TokKind.RParen, ")", 0, i++));
                    continue;
                case ';':
                    list.Add(new Token(TokKind.Comma, ";", 0, i++));
                    continue;
                case ',':
                    if (semicolonMode)
                        throw new ExpressionException(Loc.T("Vírgula inesperada (use ';' para separar argumentos).", "Unexpected comma (use ';' to separate arguments)."), i);
                    list.Add(new Token(TokKind.Comma, ",", 0, i++));
                    continue;
            }
            string? op = null;
            if (i + 1 < s.Length)
            {
                var two = s.Substring(i, 2);
                if (two is "<=" or ">=" or "==" or "!=" or "&&" or "||" or "<>")
                    op = two == "<>" ? "!=" : two;
            }
            if (op != null)
            {
                list.Add(new Token(TokKind.Op, op, 0, i));
                i += 2;
                continue;
            }
            if ("+-*/%^<>=!".IndexOf(ch) >= 0)
            {
                list.Add(new Token(TokKind.Op, ch == '=' ? "==" : ch.ToString(), 0, i++));
                continue;
            }
            throw new ExpressionException(Loc.F("Caractere inválido '{0}'.", "Invalid character '{0}'.", ch), i);
        }
        list.Add(new Token(TokKind.End, "", 0, s.Length));
        return list;
    }

    #endregion

    #region Parser

    private sealed class Parser
    {
        private readonly List<Token> _t;
        private int _p;

        public Parser(List<Token> tokens) => _t = tokens;

        public Token Current => _t[_p];
        public bool AtEnd => Current.Kind == TokKind.End;

        private bool IsOp(params string[] ops) => Current.Kind == TokKind.Op && ops.Contains(Current.Text);

        private Token Take() => _t[_p++];

        public Node ParseExpression() => ParseOr();

        private Node ParseOr()
        {
            var left = ParseAnd();
            while (IsOp("||", "or"))
            {
                Take();
                left = new Binary("or", left, ParseAnd());
            }
            return left;
        }

        private Node ParseAnd()
        {
            var left = ParseComparison();
            while (IsOp("&&", "and"))
            {
                Take();
                left = new Binary("and", left, ParseComparison());
            }
            return left;
        }

        private Node ParseComparison()
        {
            var left = ParseAdditive();
            while (IsOp("<", ">", "<=", ">=", "==", "!="))
            {
                var op = Take().Text;
                left = new Binary(op, left, ParseAdditive());
            }
            return left;
        }

        private Node ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (IsOp("+", "-"))
            {
                var op = Take().Text;
                left = new Binary(op, left, ParseMultiplicative());
            }
            return left;
        }

        private Node ParseMultiplicative()
        {
            var left = ParseUnary();
            while (IsOp("*", "/", "%"))
            {
                var op = Take().Text;
                left = new Binary(op, left, ParseUnary());
            }
            return left;
        }

        private Node ParseUnary()
        {
            if (IsOp("-", "+", "!", "not"))
            {
                var op = Take().Text;
                var operand = ParseUnary();
                return op == "+" ? operand : new Unary(op == "-" ? "-" : "!", operand);
            }
            return ParsePower();
        }

        private Node ParsePower()
        {
            var b = ParsePrimary();
            if (IsOp("^"))
            {
                Take();
                var exp = ParseUnary(); // associativa à direita
                return new Binary("^", b, exp);
            }
            return b;
        }

        private Node ParsePrimary()
        {
            var tok = Current;
            switch (tok.Kind)
            {
                case TokKind.Number:
                    Take();
                    return new Num(tok.Value);
                case TokKind.Ident:
                    Take();
                    if (Current.Kind == TokKind.LParen)
                    {
                        Take();
                        var args = new List<Node>();
                        if (Current.Kind != TokKind.RParen)
                        {
                            args.Add(ParseExpression());
                            while (Current.Kind == TokKind.Comma)
                            {
                                Take();
                                args.Add(ParseExpression());
                            }
                        }
                        Expect(TokKind.RParen, ")");
                        return Call.Create(tok.Text, args, tok.Pos);
                    }
                    return Var.Create(tok.Text);
                case TokKind.LParen:
                    Take();
                    var inner = ParseExpression();
                    Expect(TokKind.RParen, ")");
                    return inner;
                case TokKind.End:
                    throw new ExpressionException(Loc.T("Fórmula incompleta.", "Incomplete formula."), tok.Pos);
                default:
                    throw new ExpressionException(Loc.F("Símbolo inesperado '{0}'.", "Unexpected token '{0}'.", tok.Text), tok.Pos);
            }
        }

        private void Expect(TokKind kind, string text)
        {
            if (Current.Kind != kind)
                throw new ExpressionException(Loc.F("Esperado '{0}'.", "Expected '{0}'.", text), Current.Pos);
            Take();
        }
    }

    #endregion

    #region AST

    private abstract class Node
    {
        public abstract double Eval(Dictionary<string, double> vars);
        public virtual void CollectVariables(HashSet<string> set) { }
    }

    private sealed class Num(double value) : Node
    {
        public override double Eval(Dictionary<string, double> vars) => value;
    }

    private sealed class Var : Node
    {
        private readonly string _name;
        private Var(string name) => _name = name;

        public static Node Create(string name) => name.ToLowerInvariant() switch
        {
            "pi" => new Num(Math.PI),
            "tau" => new Num(2 * Math.PI),
            "e" => new Num(Math.E),
            "true" or "verdadeiro" => new Num(1),
            "false" or "falso" => new Num(0),
            _ => new Var(name),
        };

        public override double Eval(Dictionary<string, double> vars)
        {
            if (vars.TryGetValue(_name, out var v)) return v;
            throw new ExpressionException(Loc.F("Variável desconhecida '{0}'.", "Unknown variable '{0}'.", _name), 0);
        }

        public override void CollectVariables(HashSet<string> set) => set.Add(_name);
    }

    private sealed class Unary(string op, Node operand) : Node
    {
        public override double Eval(Dictionary<string, double> vars)
        {
            var v = operand.Eval(vars);
            return op == "-" ? -v : (v == 0 ? 1 : 0);
        }

        public override void CollectVariables(HashSet<string> set) => operand.CollectVariables(set);
    }

    private sealed class Binary(string op, Node left, Node right) : Node
    {
        public override double Eval(Dictionary<string, double> vars)
        {
            if (op == "and") return left.Eval(vars) != 0 && right.Eval(vars) != 0 ? 1 : 0;
            if (op == "or") return left.Eval(vars) != 0 || right.Eval(vars) != 0 ? 1 : 0;
            var a = left.Eval(vars);
            var b = right.Eval(vars);
            const double eq = 1e-9;
            return op switch
            {
                "+" => a + b,
                "-" => a - b,
                "*" => a * b,
                "/" => b == 0 ? throw new ExpressionException(Loc.T("Divisão por zero.", "Division by zero."), 0) : a / b,
                "%" => b == 0 ? throw new ExpressionException(Loc.T("Divisão por zero.", "Division by zero."), 0) : a - b * Math.Floor(a / b),
                "^" => Math.Pow(a, b),
                "<" => a < b ? 1 : 0,
                ">" => a > b ? 1 : 0,
                "<=" => a <= b + eq ? 1 : 0,
                ">=" => a >= b - eq ? 1 : 0,
                "==" => Math.Abs(a - b) <= eq ? 1 : 0,
                "!=" => Math.Abs(a - b) > eq ? 1 : 0,
                _ => throw new ExpressionException(op, 0),
            };
        }

        public override void CollectVariables(HashSet<string> set)
        {
            left.CollectVariables(set);
            right.CollectVariables(set);
        }
    }

    private sealed class Call : Node
    {
        private readonly string _name;
        private readonly Node[] _args;
        private readonly Func<double[], double>? _fn;

        private Call(string name, Node[] args, Func<double[], double>? fn)
        {
            _name = name;
            _args = args;
            _fn = fn;
        }

        private static readonly Dictionary<string, (int Min, int Max, Func<double[], double> Fn)> Functions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["sin"] = (1, 1, a => Math.Sin(a[0] * MathUtil.Deg)),
                ["cos"] = (1, 1, a => Math.Cos(a[0] * MathUtil.Deg)),
                ["tan"] = (1, 1, a => Math.Tan(a[0] * MathUtil.Deg)),
                ["asin"] = (1, 1, a => Math.Asin(a[0]) / MathUtil.Deg),
                ["acos"] = (1, 1, a => Math.Acos(a[0]) / MathUtil.Deg),
                ["atan"] = (1, 1, a => Math.Atan(a[0]) / MathUtil.Deg),
                ["atan2"] = (2, 2, a => Math.Atan2(a[0], a[1]) / MathUtil.Deg),
                ["sqrt"] = (1, 1, a => Math.Sqrt(a[0])),
                ["raiz"] = (1, 1, a => Math.Sqrt(a[0])),
                ["abs"] = (1, 1, a => Math.Abs(a[0])),
                ["min"] = (1, int.MaxValue, a => a.Min()),
                ["max"] = (1, int.MaxValue, a => a.Max()),
                ["floor"] = (1, 1, a => Math.Floor(a[0])),
                ["ceil"] = (1, 1, a => Math.Ceiling(a[0])),
                ["round"] = (1, 2, a => a.Length == 2 ? MathUtil.RoundTo(a[0], a[1]) : Math.Round(a[0], MidpointRounding.AwayFromZero)),
                ["pow"] = (2, 2, a => Math.Pow(a[0], a[1])),
                ["exp"] = (1, 1, a => Math.Exp(a[0])),
                ["ln"] = (1, 1, a => Math.Log(a[0])),
                ["log"] = (1, 2, a => a.Length == 2 ? Math.Log(a[0], a[1]) : Math.Log(a[0])),
                ["log10"] = (1, 1, a => Math.Log10(a[0])),
                ["clamp"] = (3, 3, a => Math.Clamp(a[0], Math.Min(a[1], a[2]), Math.Max(a[1], a[2]))),
                ["lerp"] = (3, 3, a => MathUtil.Lerp(a[0], a[1], a[2])),
                ["remap"] = (5, 5, a => MathUtil.Remap(a[0], a[1], a[2], a[3], a[4])),
                ["sign"] = (1, 1, a => Math.Sign(a[0])),
                ["if"] = (3, 3, a => a[0] != 0 ? a[1] : a[2]),
                ["se"] = (3, 3, a => a[0] != 0 ? a[1] : a[2]),
                ["mod"] = (2, 2, a => a[1] == 0 ? 0 : a[0] - a[1] * Math.Floor(a[0] / a[1])),
                ["rnd"] = (1, 2, a => Rng.Hash((int)Math.Round(a[0]), 0, a.Length > 1 ? (int)a[1] : 0)),
                ["noise"] = (2, 3, a => Rng.ValueNoise(a[0], a[1], a.Length > 2 ? (int)a[2] : 0)),
                ["step"] = (2, 2, a => a[1] >= a[0] ? 1 : 0),
                ["smoothstep"] = (3, 3, a => MathUtil.SmoothStep(MathUtil.InverseLerp(a[0], a[1], a[2]))),
                ["deg"] = (1, 1, a => a[0] / MathUtil.Deg),
                ["rad"] = (1, 1, a => a[0] * MathUtil.Deg),
                ["hypot"] = (2, 2, a => Math.Sqrt(a[0] * a[0] + a[1] * a[1])),
                ["dist"] = (4, 4, a => Math.Sqrt((a[2] - a[0]) * (a[2] - a[0]) + (a[3] - a[1]) * (a[3] - a[1]))),
            };

        public static IReadOnlyCollection<string> FunctionNames => Functions.Keys;

        public static Node Create(string name, List<Node> args, int pos)
        {
            if (!Functions.TryGetValue(name, out var f))
                throw new ExpressionException(Loc.F("Função desconhecida '{0}'.", "Unknown function '{0}'.", name), pos);
            if (args.Count < f.Min || args.Count > f.Max)
                throw new ExpressionException(Loc.F("Número de argumentos inválido em '{0}'.", "Wrong number of arguments for '{0}'.", name), pos);
            return new Call(name, args.ToArray(), f.Fn);
        }

        public override double Eval(Dictionary<string, double> vars)
        {
            // "if"/"se" avaliam só o ramo necessário (evita divisão por zero no ramo descartado).
            if (_name.Equals("if", StringComparison.OrdinalIgnoreCase) || _name.Equals("se", StringComparison.OrdinalIgnoreCase))
                return _args[0].Eval(vars) != 0 ? _args[1].Eval(vars) : _args[2].Eval(vars);
            var values = new double[_args.Length];
            for (var i = 0; i < values.Length; i++)
                values[i] = _args[i].Eval(vars);
            return _fn!(values);
        }

        public override void CollectVariables(HashSet<string> set)
        {
            foreach (var a in _args) a.CollectVariables(set);
        }
    }

    #endregion

    /// <summary>Lista de funções disponíveis (para ajuda na interface).</summary>
    public static IReadOnlyCollection<string> AvailableFunctions => Call.FunctionNames;
}
