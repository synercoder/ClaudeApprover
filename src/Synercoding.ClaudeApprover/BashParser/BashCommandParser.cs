using System.Text;

namespace Synercoding.ClaudeApprover.BashParser;

/// <summary>
/// Recursive descent parser for bash command strings, producing a linked list of <see cref="Pipeline"/> objects.
/// </summary>
public class BashCommandParser
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
        var pipeline = new Pipeline();
        pipeline.Commands.Add(_parseCommand());

        _skipWhitespace();

        // Check for pipe operators
        if (_position < _input.Length)
        {
            if (_peek(2) == "||")
            {
                _consume(2);
                pipeline.Operator = "||";
                _skipWhitespace();
                pipeline.NextPipeline = _parsePipeline();
            }
            else if (_peek(2) == "&&")
            {
                _consume(2);
                pipeline.Operator = "&&";
                _skipWhitespace();
                pipeline.NextPipeline = _parsePipeline();
            }
            else if (_peek() == '|')
            {
                _consume(1);
                pipeline.Operator = "|";
                _skipWhitespace();
                var nextCommand = _parseCommand();
                pipeline.Commands.Add(nextCommand);
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
            _skipWhitespace();

            if (_position >= _input.Length)
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
                _skipWhitespace();
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
                    _skipWhitespace();
                    var target = _parseToken();
                    redirections.Add(new Redirection { Type = ">>", Target = target });
                }
                else
                {
                    _skipWhitespace();
                    var target = _parseToken();
                    redirections.Add(new Redirection { Type = ">", Target = target });
                }
                continue;
            }

            if (_peek() == '<')
            {
                _consume(1);
                _skipWhitespace();
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

            if (char.IsWhiteSpace(ch) || ch == '|' || ch == '>' || ch == '<')
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

    private void _skipWhitespace()
    {
        while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
            _position++;
    }
}
