using System;
using System.Collections.Generic;
using System.Text;
using Raven.Client.Documents.Linq;
using Raven.Server.Documents.Queries.AST;
using Sparrow;

namespace Raven.Server.Documents.Queries.Parser
{
    public class QueryParser
    {
        private static readonly string[] OperatorStartMatches = { ">=", "<=", "<>", "<", ">",  "==", "=", "!=",  "BETWEEN", "IN", "ALL IN", "(" };
        private static readonly string[] BinaryOperators = { "OR", "AND" };
        private static readonly string[] StaticValues = { "true", "false", "null" };
        private static readonly string[] OrderByOptions = { "ASC", "DESC", "ASCENDING", "DESCENDING" };
        private static readonly string[] OrderByAsOptions = { "string", "long", "double", "alphaNumeric" };


        private int _depth;
        private NextTokenOptions _state = NextTokenOptions.Parenthesis;

        private int _statePos;

        public QueryScanner Scanner = new QueryScanner();

        public void Init(string q)
        {
            _depth = 0;
            Scanner.Init(q);
        }

        public Query Parse(QueryType queryType = QueryType.Select)
        {
            var q = new Query
            {
                QueryText = Scanner.Input
            };

            while (Scanner.TryScan("DECLARE"))
            {
                var (name, func) = DeclaredFunction();

                if (q.TryAddFunction(name, func) == false)
                    ThrowParseException(name + " function was declared multiple times");
            }

            q.From = FromClause();

            if (Scanner.TryScan("GROUP BY"))
                q.GroupBy = GroupBy();

            if (Scanner.TryScan("WHERE") && Expression(out q.Where) == false)
                ThrowParseException("Unable to parse WHERE clause");

            if (Scanner.TryScan("ORDER BY"))
                q.OrderBy = OrderBy();

            if (Scanner.TryScan("LOAD"))
                q.Load = SelectClauseExpressions("LOAD", false);

            switch (queryType)
            {
                case QueryType.Select:
                    if (Scanner.TryScan("SELECT"))
                        q.Select = SelectClause("SELECT", q);
                    if (Scanner.TryScan("INCLUDE"))
                        q.Include = IncludeClause();
                    break;
                case QueryType.Update:
                    if (Scanner.TryScan("UPDATE") == false)
                        ThrowParseException("Update operations must end with UPDATE clause");

                    var functionStart = Scanner.Position;
                    if (Scanner.FunctionBody() == false)
                        ThrowParseException("Update clause must have a single function body");

                    q.UpdateBody = new StringSegment(Scanner.Input, functionStart, Scanner.Position - functionStart);
                    break;
                default:
                    ThrowUnknownQueryType(queryType);
                    break;
            }

            if (Scanner.AtEndOfInput() == false)
                ThrowParseException("Expected end of query");

            return q;
        }

        private static void ThrowUnknownQueryType(QueryType queryType)
        {
            throw new ArgumentOutOfRangeException(nameof(queryType), queryType, "Unknown query type");
        }

        private List<QueryExpression> IncludeClause()
        {
            List<QueryExpression> includes = new List<QueryExpression>();

            do
            {
                if (Value(out var val))
                {
                    includes.Add(val);
                }
                else if (Field(out var field))
                {
                    includes.Add(field);
                }
                else
                {
                    ThrowParseException("Unable to understand include clause expression");
                }
            } while (Scanner.TryScan(","));
            return includes;
        }

        private (StringSegment Name, StringSegment FunctionText) DeclaredFunction()
        {
            // becuase of how we are processing them, we don't actually care for
            // parsing the function directly. We have implemented a minimal parser
            // here that find the _boundary_ of the function call, and then we hand
            // all of that code directly to the js code. 

            var functionStart = Scanner.Position;

            if (Scanner.TryScan("function") == false)
                ThrowParseException("DECLARE clause found but missing 'function' keyword");

            if (Scanner.Identifier() == false)
                ThrowParseException("DECLARE functions require a name and cannot be anonymous");

            var name = new StringSegment(Scanner.Input, Scanner.TokenStart, Scanner.TokenLength);

            // this reads the signature of the method: (a,b,c), etc.
            // we are technically allow more complex stuff there that isn't
            // allowed by JS, but that is fine, since the JS parser will break 
            // when it try it, so we are good with false positives here

            if (Scanner.TryScan('(') == false)
                ThrowParseException("Unable to parse function " + name + " signature");

            if (Method(null, out _) == false)
                ThrowParseException("Unable to parse function " + name + " signature");

            if (Scanner.FunctionBody() == false)
                ThrowParseException("Unable to get function body for " + name);

            return (name, new StringSegment(Scanner.Input, functionStart, Scanner.Position - functionStart));
        }

        private List<FieldExpression> GroupBy()
        {
            var fields = new List<FieldExpression>();
            do
            {
                if (Field(out var field) == false)
                    ThrowParseException("Unable to get field for GROUP BY");

                fields.Add(field);

                if (Scanner.TryScan(",") == false)
                    break;
            } while (true);
            return fields;
        }

        private List<(QueryExpression Expression, OrderByFieldType OrderingType, bool Ascending)> OrderBy()
        {
            var orderBy = new List<(QueryExpression Expression, OrderByFieldType OrderingType, bool Ascending)>();
            do
            {
                if (Field(out var field) == false)
                    ThrowParseException("Unable to get field for ORDER BY");

                var type = OrderByFieldType.Implicit;

                QueryExpression op;
                if (Scanner.TryScan('('))
                {
                    if (Method(field, out var method) == false)
                        ThrowParseException($"Unable to parse method call {field} for ORDER BY");
                    op = method;
                }
                else
                {
                    op = field;
                }

                if (Scanner.TryScan("AS") && Scanner.TryScan(OrderByAsOptions, out var asMatch))
                {
                    switch (asMatch)
                    {
                        case "string":
                            type = OrderByFieldType.String;
                            break;
                        case "long":
                            type = OrderByFieldType.Long;
                            break;
                        case "double":
                            type = OrderByFieldType.Double;
                            break;
                        case "alphaNumeric":
                            type = OrderByFieldType.AlphaNumeric;
                            break;
                    }
                }

                var asc = true;

                if (Scanner.TryScan(OrderByOptions, out var match))
                {
                    if (match == "DESC" || match == "DESCENDING")
                        asc = false;
                }

                orderBy.Add((op, type, asc));

                if (Scanner.TryScan(",") == false)
                    break;
            } while (true);
            return orderBy;
        }

        private List<(QueryExpression, StringSegment?)> SelectClause(string clause, Query query)
        {
            query.IsDistinct = Scanner.TryScan("DISTINCT");

            if (Scanner.TryScan("*"))
                return null;

            var functionStart = Scanner.Position;
            if (Scanner.FunctionBody())
            {
                query.SelectFunctionBody = new StringSegment(Scanner.Input, 
                    functionStart, Scanner.Position - functionStart);

                return new List<(QueryExpression, StringSegment?)>();
            }

            return SelectClauseExpressions(clause, true);
        }

        private List<(QueryExpression, StringSegment?)> SelectClauseExpressions(string clause, bool aliasAsRequired)
        {
            var select = new List<(QueryExpression Expr, StringSegment? Alias)>();

            do
            {
                QueryExpression expr;
                if (Field(out var field))
                {
                    if (Scanner.TryScan('('))
                    {
                        if (Method(field, out var method) == false)
                            ThrowParseException("Expected method call in " + clause);
                        expr = method;
                    }
                    else
                    {
                        expr = field;
                    }
                }
                else if (Value(out var v))
                {
                    expr = v;
                }
                else
                {
                    ThrowParseException("Unable to get field for " + clause);
                    return null; // never callsed
                }

                if (Alias(aliasAsRequired, out var alias) == false && expr is ValueExpression ve)
                {
                    alias = ve.Token;
                }

                @select.Add((expr, alias));

                if (Scanner.TryScan(",") == false)
                    break;
            } while (true);
            return @select;
        }



        private (FieldExpression From, StringSegment? Alias, QueryExpression Filter, bool Index) FromClause()
        {
            if (Scanner.TryScan("FROM") == false)
                ThrowParseException("Expected FROM clause");

            FieldExpression field;
            QueryExpression filter = null;
            bool index = false;
            bool isQuoted;
            if (Scanner.TryScan("INDEX"))
            {
                isQuoted = false;
                if (!Scanner.Identifier() && !(isQuoted = Scanner.String()))
                    ThrowParseException("Expected FROM INDEX source");

                field = new FieldExpression(
                    isQuoted
                        ? new StringSegment(Scanner.Input, Scanner.TokenStart + 1, Scanner.TokenLength - 2)
                        : new StringSegment(Scanner.Input, Scanner.TokenStart, Scanner.TokenLength),
                    Scanner.EscapeChars != 0
                );

                index = true;
            }
            else
            {
                isQuoted = false;
                if (!Scanner.Identifier() && !(isQuoted = Scanner.String()))
                    ThrowParseException("Expected FROM source");

                field = new FieldExpression(
                    isQuoted
                        ? new StringSegment(Scanner.Input, Scanner.TokenStart + 1, Scanner.TokenLength - 2)
                        : new StringSegment(Scanner.Input, Scanner.TokenStart, Scanner.TokenLength),
                    Scanner.EscapeChars != 0
                );
                
                if (Scanner.TryScan('(')) // FROM  Collection ( filter )
                {
                    if (Expression(out filter) == false)
                        ThrowParseException("Expected filter in filtered FORM clause");

                    if (Scanner.TryScan(')') == false)
                        ThrowParseException("Expected closing parenthesis in filtered FORM clause after filter");
                }


            }


            Alias(false, out var alias);

            return (field, alias, filter, index);
        }

        private static readonly string[] AliasKeywords =
        {
            "AS",
            "SELECT",
            "WHERE",
            "LOAD",
            "GROUP",
            "ORDER",
            "INCLUDE",
            "UPDATE"
        };

        private bool Alias(bool aliasAsRequired, out StringSegment? alias)
        {
            bool required = false;
            if (Scanner.TryScan(AliasKeywords, out var match))
            {
                required = true;
                if (match != "AS")
                {
                    // found a keyword
                    Scanner.GoBack(match.Length);
                    alias = null;
                    return false;
                }
            }
            if (aliasAsRequired && required == false)
            {
                alias = null;
                return false;
            }

            if (Field(out var f))
            {
                alias = f.Field;
                return true;
            }

            if (required)
                ThrowParseException("Expected field alias after AS in SELECT");

            alias = null;
            return false;
        }

        internal bool Parameter(out int tokenStart, out int tokenLength)
        {
            if (Scanner.TryScan('$') == false)
            {
                tokenStart = 0;
                tokenLength = 0;
                return false;
            }

            Scanner.TokenStart = Scanner.Position;

            tokenStart = Scanner.TokenStart;

            if (Scanner.Identifier(false) == false)
                ThrowParseException("Expected parameter name");

            tokenLength = Scanner.TokenLength;
            return true;
        }

        internal bool Expression(out QueryExpression op)
        {
            if (++_depth > 128)
                ThrowQueryException("Query is too complex, over 128 nested clauses are not allowed");
            if (Scanner.Position != _statePos)
            {
                _statePos = Scanner.Position;
                _state = NextTokenOptions.Parenthesis;
            }
            var result = Binary(out op);
            _depth--;
            return result;
        }

        private bool Binary(out QueryExpression op)
        {
            switch (_state)
            {
                case NextTokenOptions.Parenthesis:
                    if (Parenthesis(out op) == false)
                        return false;
                    break;
                case NextTokenOptions.BinaryOp:
                    _state = NextTokenOptions.Parenthesis;
                    if (Operator(true, out op) == false)
                        return false;
                    break;
                default:
                    op = null;
                    return false;
            }


            if (Scanner.TryScan(BinaryOperators, out var found) == false)
                return true; // found simple

            var negate = Scanner.TryScan("NOT");
            var type = found == "OR"
                ? (negate ? OperatorType.OrNot : OperatorType.Or)
                : (negate ? OperatorType.AndNot : OperatorType.And);

            _state = NextTokenOptions.Parenthesis;

            var parenthesis = Scanner.TryPeek('(');

            if (Binary(out var right) == false)
                ThrowParseException($"Failed to find second part of {type} expression");

            if (parenthesis == false)
            {
                // if the other arg isn't parenthesis, use operator precedence rules
                // to re-write the query
                switch (type)
                {
                    case OperatorType.And:
                    case OperatorType.AndNot:
                        var rightOp = (BinaryExpression)right;

                        switch (rightOp.Operator)
                        {
                            case OperatorType.AndNot:
                            case OperatorType.OrNot:
                            case OperatorType.Or:
                            case OperatorType.And:

                                rightOp.Left = new BinaryExpression(op, rightOp.Left, type);
                                op = right;
                                return true;
                        }

                        break;
                }
            }


            op = new BinaryExpression(op, right, type);

            return true;
        }

        private bool Parenthesis(out QueryExpression op)
        {
            if (Scanner.TryScan('(') == false)
            {
                _state = NextTokenOptions.BinaryOp;
                return Binary(out op);
            }

            if (Expression(out op) == false)
                return false;

            if (Scanner.TryScan(')') == false)
                ThrowParseException("Unmatched parenthesis, expected ')'");
            return true;
        }

        private bool Operator(bool fieldRequired, out QueryExpression op)
        {
            OperatorType type;
            FieldExpression field = null;

            if (Scanner.TryScan("true"))
            {
                op = new TrueExpression();
                return true;
            }
            else
            {
                if (fieldRequired && Field(out field) == false)
                {
                    op = null;
                    return false;
                }

                if (Scanner.TryScan(OperatorStartMatches, out var match) == false)
                {
                    if (fieldRequired == false)
                    {
                        op = null;
                        return false;
                    }
                    ThrowParseException("Invalid operator expected any of (In, Between, =, <, >, <=, >=)");
                }


                switch (match)
                {
                    case "<":
                        type = OperatorType.LessThan;
                        break;
                    case ">":
                        type = OperatorType.GreaterThan;
                        break;
                    case "<=":
                        type = OperatorType.LessThanEqual;
                        break;
                    case ">=":
                        type = OperatorType.GreaterThanEqual;
                        break;
                    case "=":
                    case "==":
                        type = OperatorType.Equal;
                        break;
                    case "!=":
                    case "<>":
                        type = OperatorType.NotEqual;
                        break;
                    case "BETWEEN":
                        if (Value(out var fst) == false)
                            ThrowParseException("parsing Between, expected value (1st)");
                        if (Scanner.TryScan("AND") == false)
                            ThrowParseException("parsing Between, expected AND");
                        if (Value(out var snd) == false)
                            ThrowParseException("parsing Between, expected value (2nd)");

                        if (fst.Type != snd.Type)
                            ThrowQueryException(
                                $"Invalid Between expression, values must have the same type but got {fst.Type} and {snd.Type}");

                        op = new BetweenExpression(field, fst, snd);
                        return true;
                    case "IN":
                    case "ALL IN":
                        if (Scanner.TryScan('(') == false)
                            ThrowParseException("parsing In, expected '('");

                        var list = new List<QueryExpression>();
                        do
                        {
                            if (Scanner.TryScan(')'))
                                break;

                            if (list.Count != 0)
                                if (Scanner.TryScan(',') == false)
                                    ThrowParseException("parsing In expression, expected ','");

                            if (Value(out var inVal) == false)
                                ThrowParseException("parsing In, expected a value");

                            if (list.Count > 0)
                                if (list[0].Type != inVal.Type)
                                    ThrowQueryException(
                                        $"Invalid In expression, all values must have the same type, expected {list[0].Type} but got {inVal.Type}");
                            list.Add(inVal);
                        } while (true);

                        op = new InExpression(field, list, match == "ALL IN");

                        return true;
                    case "(":
                        var isMethod = Method(field, out var method);
                        op = method;

                        if (isMethod && Operator(false, out var methodOperator))
                        {
                            if (method.Arguments == null)
                                method.Arguments = new List<QueryExpression>();

                            method.Arguments.Add(methodOperator);
                            return true;
                        }

                        return isMethod;
                    default:
                        op = null;
                        return false;
                }
            }
            
            if (Value(out var val) == false)
                ThrowParseException($"parsing {type} expression, expected a value (operators only work on scalar / parameters values)");

            op = new BinaryExpression(field, val, type);
            return true;
        }

        private bool Method(FieldExpression field, out MethodExpression op)
        {
            var args = new List<QueryExpression>();
            do
            {
                if (Scanner.TryScan(')'))
                    break;

                if (args.Count != 0)
                    if (Scanner.TryScan(',') == false)
                        ThrowParseException("parsing method expression, expected ','");

                if (Value(out var argVal))
                {
                    args.Add(argVal);
                    continue;
                }

                if (Field(out var fieldRef))
                {
                    if (Scanner.TryPeek(',') == false && Scanner.TryPeek(')') == false)
                    {
                        // this is not a simple field ref, let's parse as full expression

                        Scanner.Reset(fieldRef.Field.Offset);
                    }
                    else
                    {
                        args.Add(fieldRef);
                        continue;
                    }
                }

                if (Expression(out var expr))
                    args.Add(expr);
                else
                    ThrowParseException("parsing method, expected an argument");
            } while (true);

            op = new MethodExpression(field.Field, args);
            return true;
        }

        private void ThrowParseException(string msg)
        {
            var sb = new StringBuilder()
                .Append(Scanner.Line)
                .Append(":")
                .Append(Scanner.Column)
                .Append(" ")
                .Append(msg)
                .Append(" but got");

            if (Scanner.NextToken())
                sb.Append(": ")
                    .Append(Scanner.CurrentToken);
            else
                sb.Append(" to the end of the query");


            sb.AppendLine();
            sb.AppendLine("Query: ");
            sb.Append(Scanner.Input);

            throw new ParseException(sb.ToString());
        }

        private void ThrowQueryException(string msg)
        {
            var sb = new StringBuilder()
                .Append(Scanner.Column)
                .Append(":")
                .Append(Scanner.Line)
                .Append(" ")
                .Append(msg);

            throw new ParseException(sb.ToString());
        }

        private bool Value(out ValueExpression val)
        {
            var numberToken = Scanner.TryNumber();
            if (numberToken != null)
            {
                val = new ValueExpression(
                    new StringSegment(Scanner.Input, Scanner.TokenStart, Scanner.TokenLength),
                    numberToken.Value == NumberToken.Long ? ValueTokenType.Long : ValueTokenType.Double
                );
                return true;
            }
            if (Scanner.String())
            {
                val = new ValueExpression(
                    new StringSegment(Scanner.Input, Scanner.TokenStart, Scanner.TokenLength),
                    ValueTokenType.String,
                    Scanner.EscapeChars != 0
                );
                return true;
            }
            if (Scanner.TryScan(StaticValues, out var match))
            {
                ValueTokenType type;
                switch (match)
                {
                    case "true":
                        type  = ValueTokenType.True;
                        break;
                    case "false":
                        type  = ValueTokenType.False;
                        break;
                    case "null":
                        type  = ValueTokenType.Null;
                        break;
                    default:
                        type = ValueTokenType.String;
                        break;
                }

                val = new ValueExpression(
                    new StringSegment(Scanner.Input, Scanner.TokenStart, Scanner.TokenLength),
                    type);
                return true;
            }

            if (Parameter(out int tokenStart, out int tokenLength))
            {
                val = new ValueExpression(
                    new StringSegment(Scanner.Input, Scanner.TokenStart, Scanner.TokenLength),
                    ValueTokenType.Parameter
                );
                return true;
            }
            val = null;
            return false;
        }

        internal bool Field(out FieldExpression token)
        {
            var tokenStart = -1;
            var tokenLength = 0;
            var escapeChars = 0;
            var part = 0;
            var isQuoted = false;

            while (true)
            {
                if (Scanner.Identifier(beginning: part++ == 0) == false)
                {
                    if (Scanner.String())
                    {
                        isQuoted = true;
                        escapeChars += Scanner.EscapeChars;
                    }
                    else
                    {
                        token = null;
                        return false;
                    }
                }
                if (part == 1 && isQuoted == false)
                {
                    // need to ensure that this isn't a keyword
                    if (Scanner.CurrentTokenMatchesAnyOf(AliasKeywords))
                    {
                        Scanner.GoBack(Scanner.TokenLength);
                        token = null;
                        return false;
                    }
                }
                if (tokenStart == -1)
                    tokenStart = Scanner.TokenStart;
                tokenLength += Scanner.TokenLength;

                if (Scanner.TryScan('['))
                {
                    switch (Scanner.TryNumber())
                    {
                        case NumberToken.Long:
                        case null:
                            if (Scanner.TryScan(']') == false)
                                ThrowParseException("Expected to find closing ]");
                            tokenLength = Scanner.Position - tokenStart;
                            break;
                        case NumberToken.Double:
                            ThrowParseException("Array indexer must be integer, but got double");
                            break;
                    }
                }

                if (Scanner.TryScan('.') == false)
                    break;

                tokenLength += 1;
            }

            token = new FieldExpression(
                isQuoted ? 
                    new StringSegment(Scanner.Input, tokenStart + 1, tokenLength -2 ) : 
                    new StringSegment(Scanner.Input, tokenStart, tokenLength),
                escapeChars != 0
            );
            return true;
        }

        private enum NextTokenOptions
        {
            Parenthesis,
            BinaryOp
        }

        public class ParseException : Exception
        {
            public ParseException(string msg) : base(msg)
            {
            }
        }
    }
}
