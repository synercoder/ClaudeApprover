using System.Text;

namespace Synercoding.ClaudeApprover.Shells.Parsers;

/// <summary>
/// Recursive descent parser for bash command strings, producing a linked list of <see cref="Pipeline"/> objects.
/// </summary>
public class BashParser
{
    private string _input = string.Empty;
    private int _position;

    /// <summary>
    /// Parses a bash command line string into a <see cref="Pipeline"/>.
    /// </summary>
    /// <param name="commandLine">The bash command line to parse.</param>
    /// <returns>The parsed <see cref="Pipeline"/> representing the command structure.</returns>
    public Pipeline Parse(string commandLine)
    {
        _input = commandLine;
        _position = 0;

        return _parsePipeline();
    }

    private Pipeline _parsePipeline()
    {
        // Skip any leading separators, blank lines and comment-only lines so the
        // pipeline starts at a real command.
        _skipSeparatorsAndWhitespace();

        var pipeline = new Pipeline();
        pipeline.Commands.Add(_parseCommand());

        _skipInlineWhitespace();

        // Check for pipe/control operators and statement separators
        if (_position < _input.Length)
        {
            if (_peek(2) == "||")
            {
                _consume(2);
                pipeline.Operator = "||";
                pipeline.NextPipeline = _parsePipeline();
            }
            else if (_peek(2) == "&&")
            {
                _consume(2);
                pipeline.Operator = "&&";
                pipeline.NextPipeline = _parsePipeline();
            }
            else if (_peek() == '|')
            {
                _consume(1);
                pipeline.Operator = "|";
                _skipInlineWhitespace();
                var nextCommand = _parseCommand();
                pipeline.Commands.Add(nextCommand);
            }
            else if (_isStatementSeparator(_peek()))
            {
                // A ';' or newline separates statements. Consume the separator(s)
                // and, if any command follows, chain it as the next pipeline.
                _skipSeparatorsAndWhitespace();
                if (_position < _input.Length)
                {
                    pipeline.Operator = ";";
                    pipeline.NextPipeline = _parsePipeline();
                }
            }
        }

        return pipeline;
    }

    private Command _parseCommand()
    {
        var tokens = new List<string>();
        var redirections = new List<Redirection>();

        while (_position < _input.Length)
        {
            _skipInlineWhitespace();

            if (_position >= _input.Length)
                break;

            // A statement separator (';' or newline) ends this command
            if (_isStatementSeparator(_peek()))
                break;

            // Check for pipeline operators
            var next2 = _peek(2);
            if (next2 == "||" || next2 == "&&")
                break;

            if (_peek() == '|')
                break;

            // Check for redirections
            if (char.IsDigit(_peek()) && _position + 1 < _input.Length && _input[_position + 1] == '>')
            {
                var redirType = _input[_position].ToString() + ">";
                _consume(2);
                _skipInlineWhitespace();
                var target = _parseToken();
                redirections.Add(new Redirection { Type = redirType, Target = target });
                continue;
            }

            if (_peek() == '>')
            {
                _consume(1);
                if (_peek() == '>')
                {
                    _consume(1);
                    _skipInlineWhitespace();
                    var target = _parseToken();
                    redirections.Add(new Redirection { Type = ">>", Target = target });
                }
                else
                {
                    _skipInlineWhitespace();
                    var target = _parseToken();
                    redirections.Add(new Redirection { Type = ">", Target = target });
                }
                continue;
            }

            if (_peek() == '<')
            {
                _consume(1);
                _skipInlineWhitespace();
                var target = _parseToken();
                redirections.Add(new Redirection { Type = "<", Target = target });
                continue;
            }

            // Parse regular token
            var token = _parseToken();
            if (!string.IsNullOrEmpty(token))
                tokens.Add(token);
        }

        if (tokens.Count == 0)
            throw new InvalidOperationException("Command must have an executable");

        return new Command
        {
            Executable = tokens[0],
            Arguments = tokens.GetRange(1, tokens.Count - 1),
            Redirections = redirections
        };
    }

    private string _parseToken()
    {
        if (_position >= _input.Length)
            return string.Empty;

        var result = new StringBuilder();

        // Handle quoted strings
        if (_peek() == '"')
        {
            _consume(1); // Skip opening quote
            while (_position < _input.Length && _peek() != '"')
            {
                if (_peek() == '\\' && _position + 1 < _input.Length && _isDoubleQuoteEscapable(_peekAt(1)))
                {
                    _consume(1);
                    result.Append(_peek());
                    _consume(1);
                }
                else
                {
                    result.Append(_peek());
                    _consume(1);
                }
            }
            if (_position < _input.Length)
                _consume(1); // Skip closing quote
            return result.ToString();
        }

        if (_peek() == '\'')
        {
            _consume(1); // Skip opening quote
            while (_position < _input.Length && _peek() != '\'')
            {
                result.Append(_peek());
                _consume(1);
            }
            if (_position < _input.Length)
                _consume(1); // Skip closing quote
            return result.ToString();
        }

        // Handle unquoted tokens
        while (_position < _input.Length)
        {
            var ch = _peek();

            if (char.IsWhiteSpace(ch) || ch == '|' || ch == '>' || ch == '<' || ch == ';')
                break;

            if (ch == '\\' && _position + 1 < _input.Length && _isUnquotedEscapable(_peekAt(1)))
            {
                _consume(1);
                result.Append(_peek());
                _consume(1);
            }
            else
            {
                result.Append(ch);
                _consume(1);
            }
        }

        return result.ToString();
    }

    private char _peek()
    {
        return _position < _input.Length ? _input[_position] : '\0';
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

    private char _peekAt(int offset)
    {
        var index = _position + offset;
        return index < _input.Length ? _input[index] : '\0';
    }

    private static bool _isUnquotedEscapable(char c)
        => char.IsWhiteSpace(c) || c is '|' or '>' or '<' or '\\' or '"' or '\'' or '&' or ';' or '(' or ')' or '`';

    private static bool _isDoubleQuoteEscapable(char c)
        => c is '\\' or '"' or '`' or '$';

    private static bool _isStatementSeparator(char c)
        => c is ';' or '\n' or '\r';

    /// <summary>
    /// Skips spaces, tabs and comments, but stops at a newline so it can be
    /// recognised as a statement separator.
    /// </summary>
    private void _skipInlineWhitespace()
    {
        while (_position < _input.Length)
        {
            var c = _input[_position];
            if (c is '\n' or '\r')
            {
                break;
            }
            else if (char.IsWhiteSpace(c))
            {
                _position++;
            }
            else if (c == '#')
            {
                // Skip comment to end of line (the newline itself is left in place)
                while (_position < _input.Length && _input[_position] != '\n')
                    _position++;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Skips statement separators (<c>;</c>, newlines), surrounding whitespace and
    /// comment-only lines so parsing can resume at the next real command.
    /// </summary>
    private void _skipSeparatorsAndWhitespace()
    {
        while (_position < _input.Length)
        {
            var c = _input[_position];
            if (char.IsWhiteSpace(c) || c == ';')
            {
                _position++;
            }
            else if (c == '#')
            {
                // Skip comment to end of line
                while (_position < _input.Length && _input[_position] != '\n')
                    _position++;
            }
            else
            {
                break;
            }
        }
    }
}
