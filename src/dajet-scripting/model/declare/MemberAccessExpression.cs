using System.Text;

namespace DaJet.Scripting.Model
{
    public sealed class MemberAccessExpression : SyntaxNode
    {
        public MemberAccessExpression() { Token = Token.Variable; }
        public string Identifier { get; set; } = string.Empty; // @variable.member
        public object Binding { get; set; } // DECLARE @variable !?
        public override string ToString()
        {
            return $"[{Token}: {Identifier}]";
        }
        public string GetVariableName()
        {
            List<string> members = GetAccessMembers(Identifier);
            
            return members[0]; // @variable
        }
        public string GetDbParameterName()
        {
            // @variable.member -> @variable_member
            // @variable.member[0].member -> @variable_member_0_member
            // @variable.member[id=123].member -> @variable_member_id_member

            List<string> members = GetAccessMembers(Identifier);

            StringBuilder name = new();

            for (int i = 0; i < members.Count; i++)
            {
                if (i > 0) { name.Append('_'); }
                
                if (members[i].StartsWith('['))
                {
                    name.Append(members[i].TrimStart('[').TrimEnd(']').Split('=')[0]);
                }
                else
                {
                    name.Append(members[i]);
                }
            }

            return name.ToString();
        }
        public List<string> GetAccessMembers(in string expression)
        {
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