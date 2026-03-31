using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System;
using System.Collections.Generic;
using System.Text;

namespace DaJet.Scripting
{
    public abstract class SequenceTranspiler
    {
        protected ISchemaProvider Schema { get; private set; }
        public SequenceTranspiler(ISchemaProvider schema)
        {
            Schema = schema;
        }
        public abstract void Visit(in CreateSequenceStatement node, in StringBuilder script);
        protected virtual void Visit(in DropSequenceStatement node, in StringBuilder script)
        {
            script.Append("DROP SEQUENCE ").Append(node.Identifier).AppendLine(";");
        }
        public abstract void Visit(in ApplySequenceStatement node, in StringBuilder script);
        public abstract void Visit(in RevokeSequenceStatement node, in StringBuilder script);
    }
}