using System;
using System.Collections.Generic;
using Raven.Server.Documents.Queries.Parser;
using Sparrow;

namespace Raven.Server.Documents.Queries.AST
{
    public abstract class QueryVisitor
    {

        public void Visit(Query q)
        {
            if (q.DeclaredFunctions != null)
            {
                VisitDeclaredFunctions(q.DeclaredFunctions);
            }

            VisitFromClause(ref q.From, q.IsDistinct);

            if (q.GroupBy != null)
            {
                VisitGroupByExpression(q.GroupBy);
            }

            if (q.Where is BinaryExpression be)
            {
                VisitWhereClause(be);
            }

            if (q.OrderBy != null)
            {
                VisitOrderBy(q.OrderBy);
            }

            if (q.Load != null)
            {
                VisitLoad(q.Load);
            }

            if (q.Select != null)
            {
                VisitSelect(q.Select);
            }

            if (q.SelectFunctionBody != null)
            {
                VisitSelectFunctionBody(q.SelectFunctionBody.Value);
            }

            if (q.UpdateBody != null)
            {
                VisitUpdate(q.UpdateBody.Value);
            }

            if (q.Include != null)
            {
                VisitInclude(q.Include);
            }
        }

        public virtual void VisitInclude(List<QueryExpression> includes)
        {
            foreach (var queryExpression in includes)
            {
                VisitExpression(queryExpression);
            }
        }

        public virtual void VisitUpdate(StringSegment update)
        {
            
        }

        public virtual void VisitSelectFunctionBody(StringSegment func)
        {
            
        }

        public virtual void VisitSelect(List<(QueryExpression Expression, StringSegment? Alias)> select)
        {
            foreach (var s in select)
            {
                VisitExpression(s.Expression);
            }
        }

        public virtual void VisitLoad(List<(QueryExpression Expression, StringSegment? Alias)> load)
        {
            foreach (var l in load)
            {
                VisitExpression(l.Expression);
            }
        }

        public virtual void VisitOrderBy(List<(QueryExpression Expression, OrderByFieldType FieldType, bool Ascending)> orderBy)
        {
            foreach (var tuple in orderBy)
            {
                VisitExpression(tuple.Expression);
            }
        }

        public virtual void VisitDeclaredFunctions(Dictionary<StringSegment, StringSegment> declaredFunctions)
        {
            foreach (var kvp in declaredFunctions)
            {
                VisitDeclaredFunction(kvp.Key, kvp.Value);
            }
        }

        public virtual void VisitWhereClause(BinaryExpression where)
        {
            switch (where.Operator)
            {
                case OperatorType.Equal:
                case OperatorType.NotEqual:
                case OperatorType.LessThan:
                case OperatorType.GreaterThan:
                case OperatorType.LessThanEqual:
                case OperatorType.GreaterThanEqual:
                    VisitSimpleWhereExpression(where);
                    break;
                case OperatorType.And:
                case OperatorType.AndNot:
                case OperatorType.Or:
                case OperatorType.OrNot:
                    VisitCompoundWhereExpression(where);
                    break;
                default:
                    ThrowInvalidOperationType(@where);
                    break;
            }
        }

        public virtual void VisitCompoundWhereExpression(BinaryExpression @where)
        {
            VisitExpression(where.Left);
            VisitExpression(where.Right);
        }


        protected void VisitExpression(QueryExpression expr)
        {
            switch (expr.Type)
            {
                case ExpressionType.Field:
                    VisitField((FieldExpression)expr);
                    break;
                case ExpressionType.Between:
                    VisitBetween((BetweenExpression)expr);
                    break;
                case ExpressionType.Binary:
                    VisitBinary((BinaryExpression)expr);
                    break;
                case ExpressionType.In:
                    VisitIn((InExpression)expr);
                    break;
                case ExpressionType.Value:
                    VisitValue((ValueExpression)expr);
                    break;
                case ExpressionType.Method:
                    VisitMethod((MethodExpression)expr);
                    break;
                case ExpressionType.True:
                    VisitTrue();
                    break;
                default:
                    GetValueThrowInvalidExprType(expr);
                    break;
            }
        }

        public virtual void VisitMethod(MethodExpression expr)
        {
            foreach (var expression in expr.Arguments)
            {
                VisitExpression(expression);
            }
        }

        public virtual void VisitValue(ValueExpression expr)
        {
            
        }

        public virtual void VisitIn(InExpression expr)
        {
            foreach (var value in expr.Values)
            {
                VisitExpression(value);
            }
        }

        private void VisitBinary(BinaryExpression expr)
        {
            VisitExpression(expr.Left);
            VisitExpression(expr.Right);
        }

        public virtual void VisitBetween(BetweenExpression expr)
        {
            VisitExpression(expr.Source);
            VisitExpression(expr.Min);
            VisitExpression(expr.Max);
        }

        public virtual void VisitField(FieldExpression field)
        {
            
        }

        public virtual void VisitTrue()
        {
            
        }

        private static void GetValueThrowInvalidExprType(QueryExpression expr)
        {
            throw new ArgumentOutOfRangeException(expr.Type.ToString());
        }

        private static void ThrowInvalidOperationType(BinaryExpression @where)
        {
            throw new ArgumentOutOfRangeException(@where.Operator.ToString());
        }

        public virtual void VisitSimpleWhereExpression(BinaryExpression expr)
        {
        }

        public virtual void VisitGroupByExpression(List<FieldExpression> expressions)
        {
            
        }

        public virtual void VisitFromClause(ref (FieldExpression From, StringSegment? Alias, QueryExpression Filter, bool Index) from, bool isDistinct)
        {
            
        }

        public virtual void VisitDeclaredFunction(StringSegment name, StringSegment fund)
        {
            
        }

        public virtual void VisitWhere(QueryExpression where)
        {
            
        }
    }
}
