namespace DaJet.Scripting.Model
{
    public sealed class ConsumeStatement : SqlStatement
    {
        public ConsumeStatement() { Token = Token.CONSUME; }
        public TopClause Top { get; set; }
        public FromClause From { get; set; }
        public WhereClause Where { get; set; }
        public OrderClause Order { get; set; }
        public bool StrictOrderRequired { get; set; } // do not use hints (ms) READPAST or (pg) SKIP LOCKED
        public IntoClause Into { get; set; }
        public List<ColumnExpression> Columns { get; set; } = new(); // output object data schema
        public bool IsStream { get; set; } // streaming consume: INTO variable is object
        public StatementBlock Statements { get; set; } // stream data processor

        // CONSUME <uri> WITH <options> INTO <variable> ... RabbitMQ and Apache Kafka
        public string Target { get; set; } // uri template string
        public bool IsDatabaseSource { get { return Target is null; } }
        public List<ColumnExpression> Options { get; set; } = new(); // WITH clause
    }
}