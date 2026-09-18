using DaJet.Scripting.Model;
using System.Collections;
using System.Reflection;

namespace DaJet.Scripting
{
    //TODO: make SayHello and SayGoodbye cancelable
    public interface IScriptVisitor
    {
        void SayHello(in SyntaxNode node);
        void SayGoodbye(in SyntaxNode node);
    }
    public static class Visitor
    {
        private static HashSet<SyntaxNode> _visited;
        public static void Visit(in SyntaxNode node, in IScriptVisitor visitor)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(visitor);

            _visited = new HashSet<SyntaxNode>();
            
            VisitNode(in node, in visitor);

            _visited = null;
        }
        private static void VisitNode(in SyntaxNode node, in IScriptVisitor visitor)
        {
            if (_visited.Contains(node))
            {
                return;
            }

            _visited.Add(node);

            visitor.SayHello(in node);

            VisitChildren(in node, in visitor);

            visitor.SayGoodbye(in node);
        }
        private static void VisitChildren(in SyntaxNode parent, in IScriptVisitor visitor)
        {
            Type type = parent.GetType();

            foreach (PropertyInfo property in type.GetProperties())
            {
                Type propertyType = property.PropertyType;

                object value = property.GetValue(parent);

                if (value is null)
                {
                    continue;
                }

                //if (value is StatementBlock statements)
                //{
                //    visitor.SayHello(statements);

                //    for (int i = 0; i < statements.Count; i++)
                //    {
                //        VisitNode(statements[i], in visitor);
                //    }

                //    visitor.SayGoodbye(statements);
                //}
                //else
                
                if (propertyType.IsSyntaxNode())
                {
                    VisitNode((value as SyntaxNode), in visitor);
                }
                else if (propertyType.IsListOfSyntaxNodes())
                {
                    if (value is IList list)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            VisitNode((list[i] as SyntaxNode), in visitor);
                        }
                    }
                }
            }
        }

        public static List<TNode> Extract<TNode>(in SyntaxNode node) where TNode : SyntaxNode
        {
            List<TNode> found = new();

            Extract(in node, in found);

            return found;
        }
        private static void Extract<TNode>(in SyntaxNode node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is ColumnExpression expression) { Extract(in expression, in result); }
            else if (node is ColumnReference column) { Extract(in column, in result); }
            else if (node is ScalarExpression scalar) { Extract(in scalar, in result); }
            else if (node is VariableReference variable) { Extract(in variable, in result); }
            else if (node is MemberAccessExpression member) { Extract(in member, in result); }
            else if (node is FunctionExpression function) { Extract(in function, in result); }
            else if (node is CaseExpression _case) { Extract(in _case, in result); }
            else if (node is GroupOperator group) { Extract(in group, in result); }
            else if (node is UnaryOperator unary) { Extract(in unary, in result); }
            else if (node is BinaryOperator binary) { Extract(in binary, in result); }
            else if (node is ComparisonOperator comparison) { Extract(in comparison, in result); }
            else if (node is AdditionOperator addition) { Extract(in addition, in result); }
            else if (node is MultiplyOperator multiply) { Extract(in multiply, in result); }
        }
        private static void Extract<TNode>(in ColumnExpression node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }

            Extract(node.Expression, in result);
        }
        private static void Extract<TNode>(in ColumnReference node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }
        }
        private static void Extract<TNode>(in ScalarExpression node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }
        }
        private static void Extract<TNode>(in VariableReference node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }
        }
        private static void Extract<TNode>(in MemberAccessExpression node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }
        }
        private static void Extract<TNode>(in FunctionExpression node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }

            if (node.Parameters is not null)
            {
                foreach (SyntaxNode parameter in node.Parameters)
                {
                    Extract(in parameter, in result);
                }
            }

            if (node.Over is not null)
            {
                Extract(node.Over, in result);
            }
        }
        private static void Extract<TNode>(in OverClause node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }

            if (node.Partition is not null)
            {
                Extract(node.Partition, in result);
            }

            if (node.Order is not null)
            {
                Extract(node.Order, in result);
            }
        }
        private static void Extract<TNode>(in PartitionClause node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }

            if (node.Columns is not null)
            {
                foreach (SyntaxNode column in node.Columns)
                {
                    Extract(in column, in result);
                }
            }
        }
        private static void Extract<TNode>(in OrderClause node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }

            if (node.Expressions is not null)
            {
                foreach (OrderExpression order in node.Expressions)
                {
                    Extract(in order, in result);
                }
            }

            if (node.Offset is not null)
            {
                Extract(node.Offset, in result);

                if (node.Fetch is not null)
                {
                    Extract(node.Fetch, in result);
                }
            }
        }
        private static void Extract<TNode>(in OrderExpression node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }

            Extract(node.Expression, in result);
        }
        private static void Extract<TNode>(in CaseExpression node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }

            if (node.CASE is not null)
            {
                foreach (WhenClause when in node.CASE)
                {
                    Extract(when.WHEN, in result);
                    Extract(when.THEN, in result);
                }
            }

            if (node.ELSE is not null)
            {
                Extract(node.ELSE, in result);
            }
        }
        private static void Extract<TNode>(in GroupOperator node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }

            Extract(node.Expression, in result);
        }
        private static void Extract<TNode>(in UnaryOperator node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }

            Extract(node.Expression, in result);
        }
        private static void Extract<TNode>(in BinaryOperator node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }

            Extract(node.Expression1, in result);
            Extract(node.Expression2, in result);
        }
        private static void Extract<TNode>(in ComparisonOperator node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }

            Extract(node.Expression1, in result);
            Extract(node.Expression2, in result);
        }
        private static void Extract<TNode>(in AdditionOperator node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }

            Extract(node.Expression1, in result);
            Extract(node.Expression2, in result);
        }
        private static void Extract<TNode>(in MultiplyOperator node, in List<TNode> result) where TNode : SyntaxNode
        {
            if (node is TNode wanted)
            {
                result.Add(wanted);
            }

            Extract(node.Expression1, in result);
            Extract(node.Expression2, in result);
        }
    }
}