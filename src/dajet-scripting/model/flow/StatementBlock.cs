using System.Collections;

namespace DaJet.Scripting.Model
{
    public sealed class StatementBlock : SyntaxNode, IList<SyntaxNode>
    {
        private readonly List<SyntaxNode> _statements = new();
        public StatementBlock() { Token = Token.BEGIN; }
        public SyntaxNode this[int index]
        {
            get { return _statements[index]; }
            set { _statements[index] = value; }
        }
        public bool IsReadOnly { get { return false; } }
        public int Count { get { return _statements.Count; } }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _statements.GetEnumerator();
        }
        public IEnumerator<SyntaxNode> GetEnumerator()
        {
            return _statements.GetEnumerator();
        }
        public void Clear() { _statements.Clear(); }
        public void Add(SyntaxNode item) { _statements.Add(item); }
        public int IndexOf(SyntaxNode item) { return _statements.IndexOf(item); }
        public void Insert(int index, SyntaxNode item) { _statements.Insert(index, item); }
        public void RemoveAt(int index) { _statements.RemoveAt(index); }
        public bool Remove(SyntaxNode item) { return _statements.Remove(item); }
        public bool Contains(SyntaxNode item) { return _statements.Contains(item); }
        public void CopyTo(SyntaxNode[] array, int arrayIndex) { _statements.CopyTo(array, arrayIndex); }
    }
}