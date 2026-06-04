using DaJet.Scripting.Model;
using System.Collections;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Xml.Linq;

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
        public static void Visit(in SyntaxNode node, in IScriptVisitor visitor)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(visitor);

            VisitNode(in node, in visitor);
        }
        private static void VisitNode(in SyntaxNode node, in IScriptVisitor visitor)
        {
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

                if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
                {
                    continue;
                }

                object value = property.GetValue(parent);

                if (value is null)
                {
                    continue;
                }

                if (value is StatementBlock statements)
                {
                    visitor.SayHello(statements);

                    for (int i = 0; i < statements.Count; i++)
                    {
                        VisitNode(statements[i], in visitor);
                    }

                    visitor.SayGoodbye(statements);
                }
                else if (propertyType.IsSyntaxNode())
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
            List<TNode> nodes = new();

            IScriptVisitor extractor = new Extractor<TNode>(in nodes);

            Visit(in node, in extractor);

            return nodes;
        }
    }

    internal sealed class Extractor<TNode> : IScriptVisitor where TNode : SyntaxNode
    {
        private readonly List<TNode> _nodes;
        internal Extractor(in List<TNode> nodes)
        {
            _nodes = nodes;
        }
        public void SayHello(in SyntaxNode node)
        {
            if (node is TNode target)
            {
                _nodes.Add(target);
            }
        }
        public void SayGoodbye(in SyntaxNode node)
        {
            // do nothing
        }
    }
}