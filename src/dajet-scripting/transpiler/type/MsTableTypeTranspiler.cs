using DaJet.Metadata;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class MsTableTypeTranspiler : SqlTranspiler
    {
        private StringBuilder _script;
        private MetadataProvider _provider;
        public override bool TryTranspile(in SyntaxNode statement, in MetadataProvider provider, out string error)
        {
            ArgumentNullException.ThrowIfNull(provider, nameof(provider));
            ArgumentNullException.ThrowIfNull(statement, nameof(statement));

            error = null;
            _provider = provider;
            _script = new StringBuilder();
            
            try
            {
                Transpile(in statement);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            _script = null;
            _provider = null;

            return error is null;
        }
        public override void Visit(in SyntaxNode statement, in StringBuilder script)
        {
            throw new NotImplementedException();
        }
        private void Transpile(in SyntaxNode statement)
        {
            if (statement is CreateTypeStatement create)
            {
                Transpile(in create);
            }
            else if (statement is DropTypeStatement drop)
            {
                Transpile(in drop);
            }
            else
            {
                throw new InvalidOperationException($"Invalid statement: {statement.GetType()}");
            }
        }
        private void Transpile(in CreateTypeStatement statement)
        {
            if (statement.Table is not TableReference table)
            {
                throw new InvalidOperationException();
            }

            if (table.Binding is not EntityDefinition binding)
            {
                throw new InvalidOperationException();
            }

            statement.Sql = MsBulkInsertTranspiler.CreateTypeStatement(binding);
        }
        private void Transpile(in DropTypeStatement statement)
        {
            if (statement.Table is not TableReference table)
            {
                throw new InvalidOperationException();
            }

            if (table.Binding is not EntityDefinition binding)
            {
                throw new InvalidOperationException();
            }

            statement.Sql = MsBulkInsertTranspiler.DropTypeStatement(binding);
        }
    }
}