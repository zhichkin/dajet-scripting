namespace DaJet.Scripting.Model
{
    public sealed class MemberAccessExpression : SyntaxNode
    {
        public MemberAccessExpression() { Token = Token.Variable; }
        public string Identifier { get; set; } = string.Empty; // @variable.Member
        public DeclareStatement Binding { get; set; } // DECLARE @variable object
        public override string ToString()
        {
            return $"[{Token}: {Identifier}]";
        }
        public string GetVariableName()
        {
            return GetAccessMembers()[0]; // @variable
        }
        public List<string> GetAccessMembers()
        {
            string expression = Identifier;

            List<string> members = new();

            int position = 0;
            bool ignore_dot = false;

            for (int i = 0; i < expression.Length; i++)
            {
                if (expression[i] == '.' && !ignore_dot)
                {
                    if (position < i)
                    {
                        members.Add(expression[position..i]);
                    }

                    position = i + 1;
                }
                else if (expression[i] == '[')
                {
                    ignore_dot = true; //TODO: parse selector recursively

                    members.Add(expression[position..i]);

                    position = i;
                }
                else if (expression[i] == ']')
                {
                    members.Add(expression[position..(i + 1)]);

                    position = i + 1;

                    ignore_dot = false; //TODO: parse selector recursively
                }
            }

            members.Add(expression[position..]);

            return members;
        }
    }
}