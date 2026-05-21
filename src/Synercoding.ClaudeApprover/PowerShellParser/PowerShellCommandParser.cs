using System.Text;

namespace Synercoding.ClaudeApprover.PowerShellParser;

/// <summary>
/// Recursive descent parser for PowerShell command strings, producing a linked list of <see cref="Pipeline"/> objects.
/// </summary>
/// <remarks>
/// The parser recognises cmdlet invocations, script paths, parameters (<c>-Name value</c>), pipelines (<c>|</c>),
/// statement chains (<c>;</c>, newline, <c>&amp;&amp;</c>, <c>||</c>), quoted strings (single/double), here-strings
/// (<c>@'...'@</c>, <c>@"..."@</c>), grouping literals (<c>(...)</c>, <c>@(...)</c>, <c>@{...}</c>, <c>$(...)</c>,
/// <c>[...]</c>), redirections (including <c>2&gt;&amp;1</c>), and variable-assignment prefixes
/// (<c>$var = ...</c>). Pure-literal assignments (e.g. <c>$x = 'foo'</c>) emit a command whose executable is
/// the sentinel <see cref="ASSIGNMENT_SENTINEL"/>.
/// </remarks>
public class PowerShellCommandParser
{
    /// <summary>
    /// Executable name used for variable-assignment statements whose right-hand side is a pure literal expression.
    /// </summary>
    public const string ASSIGNMENT_SENTINEL = "<variable-assignment>";

    private string _input = string.Empty;
    private int _position;

    /// <summary>
    /// Parses a PowerShell command line string into a <see cref="Pipeline"/>.
    /// </summary>
    /// <param name="commandLine">The PowerShell command line to parse.</param>
    /// <returns>The parsed <see cref="Pipeline"/> representing the command structure.</returns>
    public Pipeline Parse(string commandLine)
    {
        _input = commandLine;
        _position = 0;

        _skipTrivia();

        if (_position >= _input.Length)
            throw new InvalidOperationException("Command must have an executable");

        return _parseStatementChain();
    }

    private Pipeline _parseStatementChain()
    {
        var pipeline = _parsePipeline();

        _skipInlineWhitespace();
        if (_position >= _input.Length)
            return pipeline;

        string? op = null;

        if (_peek() == ';')
        {
            _consume(1);
            op = ";";
        }
        else if (_peek(2) == "&&")
        {
            _consume(2);
            op = "&&";
        }
        else if (_peek(2) == "||")
        {
            _consume(2);
            op = "||";
        }
        else if (_peek() == '\n' || _peek() == '\r')
        {
            op = ";";
            while (_position < _input.Length && ( _peek() == '\n' || _peek() == '\r' ))
                _consume(1);
        }

        if (op is null)
            return pipeline;

        _skipTrivia();
        if (_position >= _input.Length)
            return pipeline;

        pipeline.Operator = op;
        pipeline.NextPipeline = _parseStatementChain();
        return pipeline;
    }

    private Pipeline _parsePipeline()
    {
        var pipeline = new Pipeline();
        pipeline.Commands.Add(_parseCommand());

        _skipInlineWhitespace();

        while (_position < _input.Length && _peek() == '|' && _peekAt(1) != '|')
        {
            _consume(1);
            pipeline.Operator = "|";
            _skipTrivia();
            pipeline.Commands.Add(_parseCommand());
            _skipInlineWhitespace();
        }

        return pipeline;
    }

    private Command _parseCommand()
    {
        _skipInlineWhitespace();

        var tokens = new List<string>();
        var redirections = new List<Redirection>();
        var isPureAssignment = false;

        if (_tryConsumeAssignmentPrefix())
        {
            if (_position < _input.Length && _isLiteralRhsStart(_peek()))
                isPureAssignment = true;
        }

        while (true)
        {
            _skipInlineWhitespace();
            if (_position >= _input.Length) break;

            var ch = _peek();
            if (ch == '\n' || ch == '\r') break;
            if (ch == ';') break;
            if (_peek(2) == "&&" || _peek(2) == "||") break;
            if (ch == '|' && _peekAt(1) != '|') break;

            if (_tryParseRedirection(redirections))
                continue;

            var tok = _parseToken();
            if (!string.IsNullOrEmpty(tok))
                tokens.Add(tok);
        }

        if (isPureAssignment)
        {
            return new Command
            {
                Executable = ASSIGNMENT_SENTINEL,
                Arguments = tokens,
                Redirections = redirections
            };
        }

        if (tokens.Count == 0)
            throw new InvalidOperationException("Command must have an executable");

        if (tokens[0] == "&" && tokens.Count >= 2)
            tokens.RemoveAt(0);

        return new Command
        {
            Executable = tokens[0],
            Arguments = tokens.GetRange(1, tokens.Count - 1),
            Redirections = redirections
        };
    }

    private bool _tryConsumeAssignmentPrefix()
    {
        var save = _position;
        if (_peek() != '$') return false;
        _consume(1);
        var idStart = _position;
        while (_position < _input.Length && _isVarChar(_peek()))
            _consume(1);
        if (_position == idStart)
        {
            _position = save;
            return false;
        }
        _skipSpacesAndTabs();
        if (_peek() != '=' || _peekAt(1) == '=')
        {
            _position = save;
            return false;
        }
        _consume(1);
        _skipSpacesAndTabs();
        return true;
    }

    private bool _tryParseRedirection(List<Redirection> redirections)
    {
        string? type = null;
        var ch = _peek();

        if (ch == '*' && _peekAt(1) == '>')
        {
            _consume(2);
            type = "*>";
            if (_peek() == '>')
            {
                _consume(1);
                type = "*>>";
            }
        }
        else if (char.IsDigit(ch) && _peekAt(1) == '>')
        {
            type = ch + ">";
            _consume(2);
            if (_peek() == '>')
            {
                _consume(1);
                type += ">";
            }
        }
        else if (ch == '>')
        {
            _consume(1);
            type = ">";
            if (_peek() == '>')
            {
                _consume(1);
                type = ">>";
            }
        }
        else if (ch == '<')
        {
            _consume(1);
            type = "<";
        }

        if (type is null) return false;

        // Stream-merge form: &<digit> immediately after the redirection operator
        if (_peek() == '&' && char.IsDigit(_peekAt(1)))
        {
            var target = "&" + _peekAt(1);
            _consume(2);
            redirections.Add(new Redirection { Type = type, Target = target });
            return true;
        }

        _skipSpacesAndTabs();
        var pathTarget = _parseToken();
        redirections.Add(new Redirection { Type = type, Target = pathTarget });
        return true;
    }

    private string _parseToken()
    {
        if (_position >= _input.Length)
            return string.Empty;

        var sb = new StringBuilder();

        // A token starting with a single outer quote is a fully-quoted token.
        if (_peek() == '"')
        {
            _consume(1);
            _readDoubleQuoteContent(sb);
            if (_position < _input.Length && _peek() == '"') _consume(1);
            if (_position >= _input.Length || _isTokenBoundary(_peek()))
                return sb.ToString();
            // Mixed-adjacency token: continue into unquoted mode.
        }
        else if (_peek() == '\'')
        {
            _consume(1);
            _readSingleQuoteContent(sb);
            if (_position < _input.Length && _peek() == '\'') _consume(1);
            if (_position >= _input.Length || _isTokenBoundary(_peek()))
                return sb.ToString();
        }
        else if (_peek() == '@' && ( _peekAt(1) == '\'' || _peekAt(1) == '"' ))
        {
            sb.Append(_readHereString(_peekAt(1)));
            return sb.ToString();
        }

        while (_position < _input.Length)
        {
            var ch = _peek();

            if (_isTokenBoundary(ch)) break;
            if (_peek(2) == "&&" || _peek(2) == "||") break;

            if (ch == '\'')
            {
                _consume(1);
                _readSingleQuoteContent(sb);
                if (_position < _input.Length && _peek() == '\'') _consume(1);
                continue;
            }

            if (ch == '"')
            {
                _consume(1);
                _readDoubleQuoteContent(sb);
                if (_position < _input.Length && _peek() == '"') _consume(1);
                continue;
            }

            if (ch == '@' && ( _peekAt(1) == '\'' || _peekAt(1) == '"' ))
            {
                sb.Append(_readHereString(_peekAt(1)));
                continue;
            }

            if (ch == '(' || ch == '{' || ch == '['
                || ( ch == '@' && ( _peekAt(1) == '(' || _peekAt(1) == '{' ) )
                || ( ch == '$' && _peekAt(1) == '(' ))
            {
                sb.Append(_readBalanced());
                continue;
            }

            if (ch == '`' && _position + 1 < _input.Length)
            {
                _consume(1);
                sb.Append(_peek());
                _consume(1);
                continue;
            }

            sb.Append(ch);
            _consume(1);
        }

        return sb.ToString();
    }

    private void _readSingleQuoteContent(StringBuilder sb)
    {
        while (_position < _input.Length)
        {
            if (_peek() == '\'' && _peekAt(1) == '\'')
            {
                sb.Append('\'');
                _consume(2);
                continue;
            }
            if (_peek() == '\'') return;
            sb.Append(_peek());
            _consume(1);
        }
    }

    private void _readDoubleQuoteContent(StringBuilder sb)
    {
        while (_position < _input.Length && _peek() != '"')
        {
            if (_peek() == '`' && _position + 1 < _input.Length)
            {
                var escaped = _peekAt(1);
                _consume(1);
                sb.Append(_translateBacktickEscape(escaped));
                _consume(1);
                continue;
            }
            sb.Append(_peek());
            _consume(1);
        }
    }

    private string _readHereString(char quote)
    {
        // Currently positioned at '@'
        _consume(2); // @'
        if (_position < _input.Length && _peek() == '\r') _consume(1);
        if (_position < _input.Length && _peek() == '\n') _consume(1);

        var sb = new StringBuilder();
        while (_position < _input.Length)
        {
            var ch = _peek();
            if (ch == '\r' && _peekAt(1) == '\n' && _peekAt(2) == quote && _peekAt(3) == '@')
            {
                _consume(4);
                return sb.ToString();
            }
            if (ch == '\n' && _peekAt(1) == quote && _peekAt(2) == '@')
            {
                _consume(3);
                return sb.ToString();
            }
            sb.Append(ch);
            _consume(1);
        }
        return sb.ToString();
    }

    private string _readBalanced()
    {
        var sb = new StringBuilder();

        if (_peek() == '@' || _peek() == '$')
        {
            sb.Append(_peek());
            _consume(1);
        }

        var opener = _peek();
        char closer = opener switch
        {
            '(' => ')',
            '{' => '}',
            '[' => ']',
            _ => ')'
        };
        sb.Append(opener);
        _consume(1);

        var stack = new Stack<char>();
        stack.Push(closer);

        while (_position < _input.Length && stack.Count > 0)
        {
            var ch = _peek();

            if (ch == '\'')
            {
                sb.Append('\'');
                _consume(1);
                while (_position < _input.Length)
                {
                    if (_peek() == '\'' && _peekAt(1) == '\'')
                    {
                        sb.Append("''");
                        _consume(2);
                        continue;
                    }
                    if (_peek() == '\'')
                    {
                        sb.Append('\'');
                        _consume(1);
                        break;
                    }
                    sb.Append(_peek());
                    _consume(1);
                }
                continue;
            }

            if (ch == '"')
            {
                sb.Append('"');
                _consume(1);
                while (_position < _input.Length && _peek() != '"')
                {
                    if (_peek() == '`' && _position + 1 < _input.Length)
                    {
                        sb.Append('`');
                        _consume(1);
                        sb.Append(_peek());
                        _consume(1);
                        continue;
                    }
                    sb.Append(_peek());
                    _consume(1);
                }
                if (_position < _input.Length)
                {
                    sb.Append('"');
                    _consume(1);
                }
                continue;
            }

            if (ch == '@' && ( _peekAt(1) == '\'' || _peekAt(1) == '"' ))
            {
                var q = _peekAt(1);
                sb.Append('@');
                _consume(1);
                sb.Append(q);
                _consume(1);
                while (_position < _input.Length)
                {
                    var pc = _peek();
                    if (pc == '\r' && _peekAt(1) == '\n' && _peekAt(2) == q && _peekAt(3) == '@')
                    {
                        sb.Append("\r\n");
                        sb.Append(q);
                        sb.Append('@');
                        _consume(4);
                        break;
                    }
                    if (pc == '\n' && _peekAt(1) == q && _peekAt(2) == '@')
                    {
                        sb.Append('\n');
                        sb.Append(q);
                        sb.Append('@');
                        _consume(3);
                        break;
                    }
                    sb.Append(pc);
                    _consume(1);
                }
                continue;
            }

            if (ch == '(' || ch == '{' || ch == '[')
            {
                char cls = ch switch
                {
                    '(' => ')',
                    '{' => '}',
                    _ => ']'
                };
                stack.Push(cls);
                sb.Append(ch);
                _consume(1);
                continue;
            }

            if (ch == ')' || ch == '}' || ch == ']')
            {
                sb.Append(ch);
                _consume(1);
                if (stack.Count > 0 && stack.Peek() == ch)
                    stack.Pop();
                continue;
            }

            if (ch == '`' && _position + 1 < _input.Length)
            {
                sb.Append('`');
                _consume(1);
                sb.Append(_peek());
                _consume(1);
                continue;
            }

            sb.Append(ch);
            _consume(1);
        }

        return sb.ToString();
    }

    private char _peek()
        => _position < _input.Length ? _input[_position] : '\0';

    private char _peekAt(int offset)
    {
        var index = _position + offset;
        return index < _input.Length ? _input[index] : '\0';
    }

    private string _peek(int count)
    {
        if (_position + count > _input.Length)
            return _input.Substring(_position);
        return _input.Substring(_position, count);
    }

    private void _consume(int count = 1)
    {
        _position += count;
    }

    private void _skipInlineWhitespace()
    {
        while (_position < _input.Length)
        {
            var ch = _peek();
            if (ch == ' ' || ch == '\t')
            {
                _consume(1);
            }
            else if (ch == '#')
            {
                while (_position < _input.Length && _peek() != '\n' && _peek() != '\r')
                    _consume(1);
            }
            else
            {
                break;
            }
        }
    }

    private void _skipSpacesAndTabs()
    {
        while (_position < _input.Length && ( _peek() == ' ' || _peek() == '\t' ))
            _consume(1);
    }

    private void _skipTrivia()
    {
        while (_position < _input.Length)
        {
            var ch = _peek();
            if (ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r')
            {
                _consume(1);
            }
            else if (ch == '#')
            {
                while (_position < _input.Length && _peek() != '\n' && _peek() != '\r')
                    _consume(1);
            }
            else
            {
                break;
            }
        }
    }

    private static bool _isTokenBoundary(char c)
        => c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '|' || c == '<' || c == '>' || c == ';';

    private static bool _isVarChar(char c)
        => char.IsLetterOrDigit(c) || c == '_' || c == ':';

    private static bool _isLiteralRhsStart(char c)
    {
        if (c == '\'' || c == '"' || c == '[' || c == '(' || c == '{' || c == '$' || c == '@') return true;
        if (char.IsDigit(c)) return true;
        if (c == '-') return true;
        return false;
    }

    private static char _translateBacktickEscape(char c) => c switch
    {
        '0' => '\0',
        'a' => '\a',
        'b' => '\b',
        'f' => '\f',
        'n' => '\n',
        'r' => '\r',
        't' => '\t',
        'v' => '\v',
        _ => c
    };
}
