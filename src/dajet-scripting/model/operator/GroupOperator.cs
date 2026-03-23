namespace DaJet.Scripting.Model
{
    public sealed class GroupOperator : SyntaxNode
    {
        public GroupOperator() { Token = Token.OpenRoundBracket; }
        public SyntaxNode Expression { get; set; }
    }
}