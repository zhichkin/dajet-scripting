using DaJet.Metadata;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class MsInsertTranspiler : SqlTranspiler
    {
        private StringBuilder _script;
        private MetadataProvider _provider;
        public override bool TryTranspile(in SyntaxNode statement, in MetadataProvider provider, out string error)
        {
            ArgumentNullException.ThrowIfNull(provider, nameof(provider));

            error = null;
            _provider = provider;

            if (statement is not InsertStatement insert)
            {
                throw new InvalidOperationException();
            }
            
            _script = new StringBuilder();
            
            try
            {
                Transpile(in insert);
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
        private void Transpile(in InsertStatement statement)
        {
            //_script.Append("DROP SEQUENCE ").Append(statement.Identifier).AppendLine(";");

            //statement.Sql = _script.ToString();
        }
    }
}