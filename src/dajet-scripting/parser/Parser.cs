using DaJet.Scripting.Model;
using DaJet.TypeSystem;

namespace DaJet.Scripting
{
    public sealed class Parser
    {
        private Script _script;
        private int _current = 0;
        private Lexeme _token = null;
        private List<Lexeme> _tokens = null;
        private List<Lexeme> _ignore = null; // test and debug purposes
        public bool TryParse(in string script, out Script model, out string error)
        {
            model = null;
            error = string.Empty;
            _script = new Script();

            Lexer lexer = new();

            if (!lexer.TryScan(in script, out _tokens, out error))
            {
                return false;
            }
            
            try
            {
                SyntaxNode node;

                while (!EndOfStream())
                {
                    node = statement();

                    if (node is not null)
                    {
                        _script.Statements.Add(node);
                    }
                }

                model = _script;
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            _script = null;

            return model is not null;
        }

        #region "UTILITY FUNCTIONS"
        private bool EndOfStream()
        {
            return (_tokens == null || _current >= _tokens.Count);
        }
        private Lexeme Current()
        {
            return _tokens[_current];
        }
        private Lexeme Previous()
        {
            return _tokens[_current - 1];
        }
        private bool Consume()
        {
            if (EndOfStream()) return false;

            _token = _tokens[_current++];

            return (_token != null);
        }
        private bool Check(Token token)
        {
            if (EndOfStream()) return false;

            return Current().Token == token;
        }
        private bool Match(params Token[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (Check(tokens[i])) return Consume();
            }
            return false;
        }
        private void Ignore()
        {
            if (Consume())
            {
                _ignore ??= new List<Lexeme>();

                _ignore.Add(Previous());
            }
        }
        private void Skip(params Token[] tokens)
        {
            while (!EndOfStream())
            {
                if (Match(tokens))
                {
                    continue;
                }
                else
                {
                    break;
                }
            }
        }
        #endregion

        #region "DATA SCHEMA DEFINITION AND VARIABLE DECLARATION STATEMENTS"

        // DECLARE @variable <type> [(<qualifiers>|<type>)]
        // CAST(column AS <type>)
        // CREATE SEQUENCE <name> AS <type>
        // WHERE Регистратор IS Документ.Приход
        // TypeReference type() function

        private DeclareStatement declare_statement()
		{
			DeclareStatement declare = new();

            declare.IsPrivate = (Previous().Token == Token.PRIVATE);
            
            if (!Match(Token.Variable))
			{
				throw new FormatException("Variable identifier expected.");
			}
			
            declare.Identifier = Previous().Value;

			if (Match(Token.AS))
			{
				// do nothing - optional
			}

			if (!Match(Token.Identifier))
			{
				throw new FormatException("Variable data type identifier expected.");
			}
			
            declare.Type = datatype(out string schema);

            declare.Schema = schema; // user-defined object type

			if (Match(Token.Equals))
			{
                if (Match(Token.Boolean, Token.Number, Token.String, Token.Binary, Token.Entity))
                {
                    declare.Initializer = scalar();
                }
                else if (Match(Token.OpenSquareBracket))
                {
                    declare.Initializer = new ValuesExpression() { Values = array_of_values() };

                    if (!Match(Token.CloseSquareBracket))
                    {
                        throw new FormatException("Close square bracket expected.");
                    }
                }
                else if (Check(Token.SELECT))
                {
                    declare.Initializer = union();
                }
                else
                {
                    throw new FormatException("Variable initializer expression is invalid.");
                }
			}

			if (Match(Token.EndOfStatement)) { /* IGNORE */ }

			Skip(Token.Comment);

			return declare;
		}
        private DataType datatype(out string schema)
        {
            schema = string.Empty;

            string identifier = Previous().Value;

            DataType type = DataType.FromName(identifier);

            if (type == DataType.Undefined)
            {
                // WHERE Регистратор IS Документ.Приход
                // DECLARE @variable array(<UDT>)
                // DECLARE @variable object(<UDT>)

                schema = identifier; // database or user-defined object type

                return DataType.Object;
            }

            if (type.IsDecimal) { return ParseDecimal(); }
            else if (type.IsInteger) { return ParseInteger(); }
            else if (type.IsString) { return ParseString(); }
            else if (type.IsBinary) { return ParseBinary(); }
            else if (type.IsEntity) { return DataType.Entity(); }
            else if (type.IsUnion) { return ParseUnion(); }
            else if (type.IsArray) { return ParseArray(out schema); }
            else if (type.IsObject) { return ParseObject(out schema); }

            return type; // boolean, datetime, uuid
        }
        private DataType array_type(in string identifier)
        {
            DataType type = DataType.FromName(identifier);

            if (type.IsArray || type.IsObject)
            {
                throw new FormatException($"[DECLARE][ARRAY] Item type '{identifier}' is invalid.");
            }

            if (identifier == "decimal") { return ParseDecimal(); }
            else if (identifier == "integer") { return ParseInteger(); }
            else if (identifier == "string") { return ParseString(); }
            else if (identifier == "binary") { return ParseBinary(); }
            else if (identifier == "entity") { return DataType.Entity(); }
            else if (identifier == "union") { return ParseUnion(); }
            
            return type; // boolean, datetime, uuid, undefined

        }
        private DataType ParseDecimal()
        {
            if (!Match(Token.OpenRoundBracket))
            {
                return DataType.Decimal();
            }

            byte precision = 8, scale = 0;

            if (!Match(Token.Number))
            {
                throw new FormatException("Number literal expected.");
            }

            if (!byte.TryParse(Previous().Value, out precision))
            {
                throw new FormatException("Number literal expected.");
            }

            if (Match(Token.Comma))
            {
                if (!Match(Token.Number))
                {
                    throw new FormatException("Number literal expected.");
                }

                if (!byte.TryParse(Previous().Value, out scale))
                {
                    throw new FormatException("Number literal expected.");
                }
            }

            if (scale > precision)
            {
                throw new FormatException($"Scale [{scale}] must be less or equal precision [{precision}].");
            }

            if (!Match(Token.CloseRoundBracket)) { throw new FormatException("Close round bracket expected."); }

            return DataType.Decimal(precision, scale);
        }
        private DataType ParseInteger()
        {
            if (!Match(Token.OpenRoundBracket))
            {
                return DataType.Integer();
            }

            if (!Match(Token.Number))
            {
                throw new FormatException("Number literal expected.");
            }

            if (!ushort.TryParse(Previous().Value, out ushort size))
            {
                throw new FormatException("Number literal expected.");
            }

            if (!(size == 1 || size == 2 || size == 4 || size == 8))
            {
                throw new FormatException("Number literal of 1, 2, 4, or 8 expected.");
            }

            if (!Match(Token.CloseRoundBracket)) { throw new FormatException("Close round bracket expected."); }

            return DataType.Integer(size);
        }
        private DataType ParseString()
        {
            if (!Match(Token.OpenRoundBracket))
            {
                return DataType.String();
            }

            if (!Match(Token.Number))
            {
                throw new FormatException("Number literal expected.");
            }

            if (!ushort.TryParse(Previous().Value, out ushort size))
            {
                throw new FormatException("Number literal expected.");
            }

            if (size > 1024)
            {
                throw new FormatException("Number literal of 1024 or less expected.");
            }

            bool variable = true;

            if (Match(Token.Comma))
            {
                if (!Match(Token.Identifier))
                {
                    throw new FormatException("Qualifier literal 'fixed' expected.");
                }

                if (Previous().Value != "fixed")
                {
                    throw new FormatException("Qualifier literal 'fixed' expected.");
                }

                variable = false;
            }

            if (!Match(Token.CloseRoundBracket)) { throw new FormatException("Close round bracket expected."); }

            return DataType.String(size, variable);
        }
        private DataType ParseBinary()
        {
            if (!Match(Token.OpenRoundBracket))
            {
                return DataType.Binary();
            }

            if (!Match(Token.Number))
            {
                throw new FormatException("Number literal expected.");
            }

            if (!ushort.TryParse(Previous().Value, out ushort size))
            {
                throw new FormatException("Number literal expected.");
            }

            if (size > 1024)
            {
                throw new FormatException("Number literal of 1024 or less expected.");
            }

            bool variable = true;

            if (Match(Token.Comma))
            {
                if (!Match(Token.Identifier))
                {
                    throw new FormatException("Qualifier literal 'fixed' expected.");
                }

                if (Previous().Value != "fixed")
                {
                    throw new FormatException("Qualifier literal 'fixed' expected.");
                }

                variable = false;
            }

            if (!Match(Token.CloseRoundBracket)) { throw new FormatException("Close round bracket expected."); }

            return DataType.Binary(size, variable);
        }
        private DataType ParseUnion()
        {
            if (!Match(Token.OpenRoundBracket))
            {
                return new DataType(DataTypeFlags.UnionTypes, QualifierFlags.DateTime, 10, 15, 2);
            }

            DataTypeFlags types = DataTypeFlags.Undefined;
            QualifierFlags qualifiers = QualifierFlags.None;
            ushort size = 0; // string
            byte precision = 0; // decimal
            byte scale = 0; // decimal

            int count = ParseUnionDataType(ref types, ref qualifiers, ref size, ref precision, ref scale);

            while (Match(Token.Comma))
            {
                count += ParseUnionDataType(ref types, ref qualifiers, ref size, ref precision, ref scale);
            }

            if (count == 1)
            {
                throw new FormatException("Union type must have more then 1 data type qualified.");
            }

            if (!Match(Token.CloseRoundBracket)) { throw new FormatException("Close round bracket expected."); }

            return new DataType(types, qualifiers, size, precision, scale);
        }
        private DataType ParseArray(out string schema)
        {
            schema = string.Empty;

            if (!Match(Token.OpenRoundBracket))
            {
                return DataType.Array(); // Array of objects without data schema defined
            }

            if (!Match(Token.Identifier))
            {
                throw new FormatException("[DECLARE][ARRAY] Item type identifier expected.");
            }

            string identifier = Previous().Value;

            if (identifier == "array")
            {
                throw new FormatException("[DECLARE][ARRAY] Item type 'array' is invalid.");
            }

            if (identifier == "object")
            {
                throw new FormatException($"[DECLARE][ARRAY] Item type incorrect syntax 'array(object)'. Use 'array(<UDT>)' instead.");
            }

            if (!Match(Token.CloseRoundBracket)) { throw new FormatException("[DECLARE][ARRAY] Close round bracket expected."); }

            DataType item = array_type(in identifier);

            if (!item.IsUndefined)
            {
                return DataType.Array(item); // Array of simple types
            }

            schema = identifier; // User-defined data schema

            return DataType.Array(); // Array of user-defined object type
        }
        private DataType ParseObject(out string schema)
        {
            schema = string.Empty;

            if (!Match(Token.OpenRoundBracket))
            {
                return DataType.Object; // Object without data schema defined
            }

            if (!Match(Token.Identifier))
            {
                throw new FormatException("[DECLARE][OBJECT] Type identifier expected.");
            }

            string identifier = Previous().Value;

            if (!Match(Token.CloseRoundBracket)) { throw new FormatException("[DECLARE][OBJECT] Close round bracket expected."); }

            schema = identifier; // User-defined data schema

            return DataType.Object;
        }
        private int ParseUnionDataType(ref DataTypeFlags types, ref QualifierFlags qualifiers, ref ushort size, ref byte precision, ref byte scale)
        {
            if (!Match(Token.Identifier))
            {
                throw new FormatException("Data type identifier expected.");
            }

            string identifier = Previous().Value;

            if (identifier == "boolean")
            {
                types |= DataTypeFlags.Boolean;
            }
            else if (identifier == "decimal")
            {
                types |= DataTypeFlags.Decimal;
                
                ParseDecimal(ref precision, ref scale);
            }
            else if (identifier == "datetime" || identifier == "date" || identifier == "time")
            {
                types |= DataTypeFlags.DateTime;

                if (identifier == "date")
                {
                    qualifiers |= QualifierFlags.Date;
                }
                else if (identifier == "time")
                {
                    qualifiers |= QualifierFlags.Time;
                }
                else
                {
                    qualifiers |= QualifierFlags.DateTime;
                }
            }
            else if (identifier == "string")
            {
                types |= DataTypeFlags.String;
                
                ParseString(ref size, ref qualifiers);
            }
            else if (identifier == "entity")
            {
                types |= DataTypeFlags.Entity;
            }
            else
            {
                throw new FormatException($"Union data type invalid identifier: [{identifier}]");
            }

            return 1;
        }
        private void ParseDecimal(ref byte precision, ref byte scale)
        {
            if (!Match(Token.OpenRoundBracket))
            {
                precision = 10; scale = 0; return;
            }
            
            if (!Match(Token.Number))
            {
                throw new FormatException("Number literal expected.");
            }
            
            if (!byte.TryParse(Previous().Value, out precision))
            {
                throw new FormatException("Number literal expected.");
            }

            if (Match(Token.Comma))
            {
                if (!Match(Token.Number))
                {
                    throw new FormatException("Number literal expected.");
                }

                if (!byte.TryParse(Previous().Value, out scale))
                {
                    throw new FormatException("Number literal expected.");
                }
            }

            if (!Match(Token.CloseRoundBracket)) { throw new FormatException("Close round bracket expected."); }
        }
        private void ParseString(ref ushort size, ref QualifierFlags qualifiers)
        {
            if (!Match(Token.OpenRoundBracket))
            {
                size = 10; return;
            }

            if (!Match(Token.Number))
            {
                throw new FormatException("Number literal expected.");
            }

            if (!ushort.TryParse(Previous().Value, out size))
            {
                throw new FormatException("Number literal expected.");
            }

            if (size > 1024)
            {
                throw new FormatException("Number literal of 1024 or less expected.");
            }

            if (Match(Token.Comma))
            {
                if (!Match(Token.Identifier))
                {
                    throw new FormatException("Qualifier literal expected.");
                }

                if (Previous().Value != "fixed")
                {
                    throw new FormatException("Qualifier literal expected.");
                }

                qualifiers |= QualifierFlags.Fixed;
            }

            if (!Match(Token.CloseRoundBracket)) { throw new FormatException("Close round bracket expected."); }
        }

        private TypeReference type_reference()
        {
            TypeReference type = new()
            {
                Type = datatype(out string schema),
                Schema = schema
            };

            return type;
        }
        private SyntaxNode import_statement()
		{
			if (!Match(Token.IMPORT))
			{
				throw new FormatException("IMPORT keyword expected.");
			}

			ImportStatement statement = new();

			Skip(Token.Comment);

			if (!Match(Token.String))
			{
				throw new FormatException("IMPORT: data source URL expected.");
			}
			else
			{
				statement.Source = Previous().Value;
			}

			Skip(Token.Comment);

			if (Match(Token.INTO))
			{
				statement.Target = table_variables();

				Skip(Token.Comment);

				if (Match(Token.EndOfStatement)) { /* IGNORE */ }

				Skip(Token.Comment);
			}

			return statement;
		}
        private SyntaxNode define_statement()
		{
			DefineStatement statement = new();

			if (!Match(Token.Identifier))
			{
				throw new FormatException("[DEFINE] Type identifier expected");
			}

			statement.Identifier = Previous().Value;

			if (!Match(Token.OpenRoundBracket))
			{
				throw new FormatException("[DEFINE] Open round bracket expected.");
			}

			statement.Properties.Add(property_definition());

			while (Match(Token.Comma))
			{
				statement.Properties.Add(property_definition());
			}

			if (!Match(Token.CloseRoundBracket))
			{
				throw new FormatException("[DEFINE] Close round bracket expected.");
			}

			return statement;
		}
		private DefineProperty property_definition()
		{
			if (!Match(Token.Identifier)) { throw new FormatException("Property identifier expected"); }

			DefineProperty property = new() { Name = Previous().Value };

			if (!Match(Token.Identifier, Token.UNION)) //NOTE: exceptional keyword
			{
				throw new FormatException("Data type identifier expected");
			}

            property.Type = datatype(out string schema);

            property.Schema = schema; // user-defined object type

            return property;
		}
		private ColumnDefinition column_definition()
		{
			if (!Match(Token.Identifier)) { throw new FormatException("Column identifier expected."); }

			ColumnDefinition column = new() { Name = Previous().Value };

			if (!Match(Token.Identifier)) { throw new FormatException("Data type identifier expected."); }

            column.Type = datatype(out string schema);

            if (column.Type.IsArray || column.Type.IsObject)
            {
                throw new FormatException($"[COLUMN] Database column data type {column.Type} is not allowed.");
            }

            return column;
		}
		#endregion

		#region "ALGORITHMIC STATEMENTS"
        private SyntaxNode comment()
        {
            return new CommentStatement()
            {
                Text = Previous().Value
            };
        }
        private DirectiveStatement directive()
        {
            DirectiveStatement pragma = new();

            if (Match(Token.NAME))
            {
                pragma.Token = Token.NAME;

                if (!Match(Token.OpenRoundBracket))
                {
                    throw new FormatException("[NAME] open round bracket expected");
                }

                if (!Match(Token.String))
                {
                    throw new FormatException("[NAME] name value expected");
                }

                string name = Previous().Value;

                if (!Match(Token.CloseRoundBracket))
                {
                    throw new FormatException("[NAME] close round bracket expected");
                }

                _script.Name = name;
            }
            else if (Match(Token.STARTUP))
            {
                pragma.Token = Token.STARTUP;
                
                _script.RunAtStartup = true;
            }
            else if (Match(Token.LONG_TASK))
            {
                pragma.Token = Token.LONG_TASK;

                _script.IsLongRunning = true;
            }
            else if (Match(Token.SINGLETON))
            {
                pragma.Token = Token.SINGLETON;

                if (!Match(Token.OpenRoundBracket))
                {
                    throw new FormatException("[SINGLETON] open round bracket expected");
                }

                if (!Match(Token.String))
                {
                    throw new FormatException("[SINGLETON] key value expected");
                }

                string key = Previous().Value;

                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new FormatException("[SINGLETON] key value is empty");
                }
                
                if (!Match(Token.CloseRoundBracket))
                {
                    throw new FormatException("[SINGLETON] close round bracket expected");
                }

                _script.IsSingleton = true;
                _script.SingletonKey = key;
            }
            else
            {
                throw new FormatException("[#] NAME, STARTUP, LONG_TASK or SINGLETON expected");
            }

            return pragma;
        }
        private SyntaxNode assignment()
        {
            AssignmentOperator statement = new();

            if (!Match(Token.Variable))
            {
                throw new FormatException("[SET] Target variable expected");
            }

            SyntaxNode target = variable();

            if (target is not VariableReference _variable &&
                target is not MemberAccessExpression memberAccess)
            {
                throw new FormatException("[SET] Variable identifier or member access expression expected");
            }

            statement.Target = target;

            if (!Match(Token.Equals))
            {
                throw new FormatException("[SET] Assignment operator expected");
            }

            if (Check(Token.SELECT))
            {
                statement.Initializer = union();
            }
            else if (Match(Token.OpenSquareBracket))
            {
                statement.Initializer = new ValuesExpression() { Values = array_of_values() };

                if (!Match(Token.CloseSquareBracket))
                {
                    throw new FormatException("[SET] Close square bracket expected.");
                }
            }
            else
            {
                statement.Initializer = expression();
            }

            if (statement.Initializer is null)
            {
                throw new FormatException("[SET] Target initializer expected");
            }

            return statement;
        }
        private SyntaxNode use_statement()
        {
            UseStatement statement = new();

            if (!Match(Token.String))
            {
                throw new FormatException("[USE] data source identifier expected");
            }

            statement.Source = Previous().Value;

            statement.Statements = statement_block(Token.END);

            if (statement.Statements.Count == 0)
            {
                throw new FormatException("[USE] statement block is empty");
            }

            if (!Match(Token.END))
            {
                throw new FormatException("[USE] END keyword expected");
            }

            return statement;
        }
        private SyntaxNode for_statement()
        {
            ForStatement statement = new();

            if (Match(Token.EACH))
            {
                // do nothing - optional keyword
            }

            if (!Match(Token.Variable) || variable() is not VariableReference _variable)
            {
                throw new FormatException("[FOR] variable identifier expected");
            }

            statement.Variable = _variable;

            if (!Match(Token.IN))
            {
                throw new FormatException("[FOR] IN keyword expected");
            }

            if (!Match(Token.Variable) || variable() is not VariableReference _iterator)
            {
                throw new FormatException("[FOR] iterator identifier expected");
            }

            statement.Iterator = _iterator;

            Skip(Token.Comment);

            if (Match(Token.MAXDOP)) // optional
            {
                if (Match(Token.UNBOUNDED))
                {
                    statement.DegreeOfParallelism = int.MaxValue;
                }
                else
                {
                    int minus = Match(Token.Minus) ? -1 : 1;

                    if (!Match(Token.Number) || scalar() is not ScalarExpression _scalar)
                    {
                        throw new FormatException("[FOR] MAXDOP parameter expected");
                    }

                    statement.DegreeOfParallelism = minus * int.Parse(_scalar.Literal);
                }

                Skip(Token.Comment);
            }

            statement.Statements = statement_block(Token.END);

            if (statement.Statements.Count == 0)
            {
                throw new FormatException("[FOR] statement block is empty");
            }

            if (!Match(Token.END))
            {
                throw new FormatException("[FOR] END keyword expected");
            }

            return statement;
        }
        private SyntaxNode try_statement()
        {
            TryStatement statement = new();

            statement.TRY = statement_block(Token.CATCH, Token.FINALLY);

            if (statement.TRY.Count == 0)
            {
                throw new FormatException("[TRY]: statement block is empty");
            }

            if (Match(Token.CATCH)) // optional if FINALLY is present
            {
                statement.CATCH = statement_block(Token.FINALLY, Token.END);

                if (statement.CATCH.Count == 0)
                {
                    throw new FormatException("[TRY] CATCH block is empty");
                }
            }

            if (Match(Token.FINALLY)) // optional if CATCH is present
            {
                statement.FINALLY = statement_block(Token.END);

                if (statement.FINALLY.Count == 0)
                {
                    throw new FormatException("[TRY] FINALLY block is empty");
                }
            }

            bool catch_is_missing = (statement.CATCH.Count == 0);
            bool finally_is_missing = (statement.FINALLY.Count == 0);

            if (catch_is_missing && finally_is_missing) // either CATCH or FINALLY block must be present
            {
                throw new FormatException("[TRY] CATCH or FINALLY block expected");
            }

            if (!Match(Token.END))
            {
                throw new FormatException("[TRY] END keyword expected");
            }

            return statement;
        }
        private SyntaxNode throw_statement()
        {
            ThrowStatement statement = new()
            {
                Expression = expression()
            };

            Skip(Token.Comment);

            return statement;
        }
        private SyntaxNode sleep_statement()
        {
            SleepStatement statement = new();

            if (!Match(Token.Number) || scalar() is not ScalarExpression timeout)
            {
                throw new FormatException("[SLEEP] timeout value expected");
            }

            statement.Timeout = int.Parse(timeout.Literal);

            return statement;
        }
        private SyntaxNode statement()
        {
            if (Match(Token.Comment)) { return comment(); }
            else if (Match(Token.Sharp)) { return directive(); }
            else if (Match(Token.DECLARE, Token.PRIVATE)) { return declare_statement(); }
            else if (Match(Token.SET)) { return assignment(); }
            else if (Match(Token.USE)) { return use_statement(); }
            else if (Match(Token.FOR)) { return for_statement(); }
            else if (Match(Token.TRY)) { return try_statement(); }
            else if (Match(Token.THROW)) { return throw_statement(); }
            else if (Match(Token.SLEEP)) { return sleep_statement(); }
            else if (Match(Token.IF)) { return if_statement(); }
            else if (Match(Token.CASE)) { return case_statement(); }
            else if (Match(Token.WHILE)) { return while_statement(); }
            else if (Match(Token.WITH)) { return statement_with_cte(); }
            else if (Match(Token.CREATE)) { return create_statement(); }
            else if (Check(Token.SELECT)) { return select_statement(); }
            else if (Check(Token.INSERT)) { return insert_statement(); }
            else if (Check(Token.UPDATE)) { return update_statement(); }
            else if (Check(Token.DELETE)) { return delete_statement(); }
            else if (Check(Token.UPSERT)) { return upsert_statement(); }
            else if (Check(Token.STREAM)) { return stream_statement(); }
            else if (Check(Token.CONSUME)) { return consume_statement(); }
            else if (Check(Token.PRODUCE)) { return produce_statement(); }
            else if (Check(Token.REQUEST)) { return request_statement(); }
            else if (Check(Token.IMPORT)) { return import_statement(); }
            else if (Match(Token.DROP)) { return drop_statement(); }
            else if (Match(Token.APPLY)) { return apply_statement(); }
            else if (Match(Token.REVOKE)) { return revoke_statement(); }
            else if (Match(Token.PRINT)) { return print_statement(); }
            else if (Match(Token.BREAK)) { return new BreakStatement(); }
            else if (Match(Token.CONTINUE)) { return new ContinueStatement(); }
            else if (Match(Token.RETURN)) { return return_statement(); }
            else if (Match(Token.EXECUTE)) { return execute_statement(); }
            else if (Match(Token.PROCESS)) { return process_statement(); }
            else if (Match(Token.WAIT)) { return wait_statement(); }
            else if (Match(Token.MODIFY)) { return modify_statement(); }
            else if (Match(Token.DEFINE)) { return define_statement(); }
            else if (Match(Token.EndOfStatement)) { return null; }

            Ignore();

            throw new FormatException($"Unknown statement: {Previous()}");
        }
        private SyntaxNode print_statement() { return new PrintStatement() { Expression = expression() }; }
        private StatementBlock statement_block(params Token[] terminals)
        {
            SyntaxNode node;

            bool expect_statement = true;

            StatementBlock block = new();

            while (!EndOfStream() && expect_statement)
            {
                node = statement();

                if (node is not null)
                {
                    block.Add(node);
                }

                for (int i = 0; i < terminals.Length; i++)
                {
                    if (Check(terminals[i]))
                    {
                        expect_statement = false; break;
                    }
                }
            }

            return block;
        }
        private SyntaxNode if_statement()
        {
            IfStatement statement = new();

            bool expect_close = Match(Token.OpenRoundBracket);

            statement.IF = predicate();

            if (expect_close && !Match(Token.CloseRoundBracket))
            {
                throw new FormatException("IF: close round bracket expected");
            }

            Skip(Token.Comment);

            if (!Match(Token.THEN))
            {
                throw new FormatException("IF: THEN keyword expected");
            }

            statement.THEN = statement_block(Token.ELSE, Token.END);

            if (statement.THEN.Count == 0)
            {
                throw new FormatException("IF: THEN statement block is empty");
            }

            if (Match(Token.ELSE)) // optional
            {
                statement.ELSE = statement_block(Token.END);

                if (statement.ELSE.Count == 0)
                {
                    throw new FormatException("IF: ELSE statement block is empty");
                }
            }

            if (!Match(Token.END))
            {
                throw new FormatException("[IF] END keyword expected");
            }

            return statement;
        }
        private SyntaxNode case_statement()
        {
            CaseStatement statement = new();

            Skip(Token.Comment);

            while (Match(Token.WHEN))
            {
                bool expect_close = Match(Token.OpenRoundBracket);

                WhenClause when = new() { WHEN = predicate() };

                if (expect_close && !Match(Token.CloseRoundBracket))
                {
                    throw new FormatException("[CASE] WHEN close round bracket expected");
                }

                if (!Match(Token.THEN))
                {
                    throw new FormatException($"[CASE] THEN keyword expected");
                }

                StatementBlock block = statement_block(Token.WHEN, Token.ELSE, Token.END);

                if (block.Count == 0)
                {
                    throw new FormatException("[CASE] THEN statement block is empty");
                }

                when.THEN = block;
                
                statement.CASE.Add(when);
            }

            if (statement.CASE.Count == 0)
            {
                throw new FormatException("[CASE] WHEN ... THEN expected");
            }

            if (Match(Token.ELSE))
            {
                statement.ELSE = statement_block(Token.END);

                if (statement.ELSE.Count == 0)
                {
                    throw new FormatException("[CASE] ELSE statement block is empty");
                }
            }

            if (!Match(Token.END))
            {
                throw new FormatException("[CASE] END keyword expected");
            }

            return statement;
        }
        private SyntaxNode while_statement()
        {
            WhileStatement statement = new();

            bool expect_close = Match(Token.OpenRoundBracket);

            statement.Condition = predicate();

            if (expect_close && !Match(Token.CloseRoundBracket))
            {
                throw new FormatException("[WHILE] close round bracket expected");
            }

            statement.Statements = statement_block(Token.END);

            if (statement.Statements.Count == 0)
            {
                throw new FormatException("[WHILE] statement block is empty");
            }

            if (!Match(Token.END))
            {
                throw new FormatException("[WHILE] END keyword expected");
            }

            return statement;
        }
        private SyntaxNode return_statement()
        {
            ReturnStatement statement = new()
            {
                Expression = expression()
            };

            Skip(Token.Comment);

            return statement;
        }
        private SyntaxNode execute_statement()
        {
            ExecuteStatement statement = new();

            if (Match(Token.TASK))
            {
                statement.Kind = ExecuteKind.Task;
            }
            else if (Match(Token.WORK))
            {
                statement.Kind = ExecuteKind.Work;
            }
            else if (Match(Token.SYNC))
            {
                statement.Kind = ExecuteKind.Sync;
            }

            if (!Match(Token.String))
            {
                throw new FormatException("[EXECUTE] uri expected");
            }

            statement.Uri = Previous().Value;

            Skip(Token.Comment);

            if (Match(Token.DEFAULT)) // optional
            {
                if (Match(Token.String))
                {
                    statement.Default = Previous().Value;
                }
                else
                {
                    throw new FormatException("[EXECUTE] default uri expected");
                }
            }

            Skip(Token.Comment);

            if (Match(Token.AS)) // optional
            {
                statement.Name = expression();
            }

            Skip(Token.Comment);

            if (Match(Token.WITH)) // optional
            {
                parse_column_expressions(statement.Parameters);
            }

            Skip(Token.Comment);

            if (Match(Token.INTO)) // optional
            {
                if (!Match(Token.Variable))
                {
                    throw new FormatException($"[EXECUTE] variable identifier expected");
                }

                statement.Return = new VariableReference()
                {
                    Identifier = Previous().Value
                };
            }

            Skip(Token.Comment);

            return statement;
        }
        private SyntaxNode create_statement()
        {
            if (Match(Token.TYPE))
            {
                return create_type();
            }

            if (Match(Token.SEQUENCE))
            {
                return create_sequence();
            }

            Token token = Token.TABLE;

            if (Match(Token.TEMPORARY))
            {
                token = Previous().Token;
            }

            if (!Match(Token.TABLE))
            {
                throw new FormatException("TABLE keyword expected.");
            }

            if (Match(Token.VARIABLE))
            {
                token = Previous().Token;
            }

            if (token == Token.TABLE)
            {
                return create_table();
            }

            if (!Check(Token.Identifier))
            {
                throw new FormatException("Table identifier expected.");
            }

            if (token == Token.VARIABLE)
            {
                return table_variable();
            }
            else if (token == Token.TEMPORARY)
            {
                return temporary_table();
            }

            throw new FormatException("Invalid CREATE TABLE statement.");
        }
        private SyntaxNode drop_statement()
        {
            if (Match(Token.SEQUENCE))
            {
                return drop_sequence();
            }

            throw new FormatException("Unknown DROP statement.");
        }
        private SyntaxNode apply_statement()
        {
            if (Match(Token.SEQUENCE))
            {
                return apply_sequence();
            }

            throw new FormatException("Unknown APPLY statement");
        }
        private SyntaxNode revoke_statement()
        {
            if (Match(Token.SEQUENCE))
            {
                return revoke_sequence();
            }

            throw new FormatException("Unknown REVOKE statement");
        }
        private SyntaxNode wait_statement()
        {
            WaitStatement statement = new();

            if (Match(Token.ALL))
            {
                statement.Kind = WaitKind.All;
            }
            else if (Match(Token.ANY))
            {
                statement.Kind = WaitKind.Any;
            }
            else
            {
                throw new FormatException("[WAIT] {ALL|ANY} keyword expected");
            }

            if (!Match(Token.Variable) || variable() is not VariableReference array)
            {
                throw new FormatException("[WAIT] task array variable expected");
            }

            statement.Tasks = array;

            if (statement.Kind == WaitKind.Any)
            {
                if (!Match(Token.INTO))
                {
                    throw new FormatException("[WAIT] {INTO} keyword expected");
                }

                if (!Match(Token.Variable) || variable() is not VariableReference task)
                {
                    throw new FormatException("[WAIT] task object variable expected");
                }

                statement.Result = task; //NOTE: DataObject
            }
            else if (statement.Kind == WaitKind.All)
            {
                if (Match(Token.INTO)) // optional
                {
                    if (!Match(Token.Variable) || variable() is not VariableReference completed)
                    {
                        throw new FormatException("[WAIT] completed variable expected");
                    }

                    statement.Result = completed; //NOTE: boolean
                }
            }

            if (Match(Token.TIMEOUT)) // optional
            {
                if (!Match(Token.Number) || scalar() is not ScalarExpression _scalar)
                {
                    throw new FormatException("[WAIT] timeout value expected");
                }

                int timeout = int.Parse(_scalar.Literal);

                if (timeout > 0)
                {
                    statement.Timeout = timeout;
                }
                else
                {
                    throw new FormatException("[WAIT] timeout value must be greater then zero");
                }
            }
            
            return statement;
        }

        #endregion

        #region "CREATE TABLE STATEMENT"
        private SyntaxNode create_table()
        {
            if (!Match(Token.Identifier)) { throw new FormatException("Table identifier expected."); }

            string identifier = Previous().Value;

            //if (!Match(Token.OF))
            //{
            //    throw new FormatException("OF keyword expected.");
            //}

            //if (!Match(Token.Identifier)) { throw new FormatException("Type identifier expected."); }

            return new CreateTableStatement()
            {
                Name = identifier,
                Type = Previous().Value
            };
        }
        private SyntaxNode table_variable()
        {
            if (!Match(Token.Identifier))
            {
                throw new FormatException("Table identifier expected.");
            }

            string identifier = Previous().Value;

            if (!Match(Token.AS))
            {
                throw new FormatException("AS keyword expected.");
            }

            bool expect_close = Match(Token.OpenRoundBracket);

            TableVariableExpression table = new()
            {
                Name = identifier,
                Expression = union()
            };

            if (expect_close && !Match(Token.CloseRoundBracket))
            {
                throw new FormatException("CREATE TABLE: close round bracket expected.");
            }

            return table;
        }
        private SyntaxNode temporary_table()
        {
            if (!Match(Token.Identifier))
            {
                throw new FormatException("Table identifier expected.");
            }

            string identifier = Previous().Value;

            if (!Match(Token.AS))
            {
                throw new FormatException("AS keyword expected.");
            }

            bool expect_close = Match(Token.OpenRoundBracket);

            TemporaryTableExpression table = new()
            {
                Name = identifier,
                Expression = union()
            };

            if (expect_close && !Match(Token.CloseRoundBracket))
            {
                throw new FormatException("CREATE TABLE: close round bracket expected.");
            }

            return table;
        }
        private SyntaxNode statement_with_cte()
        {
            CommonTableExpression root = cte();

            while (Match(Token.Comma))
            {
                CommonTableExpression node = cte();
                node.Next = root;
                root = node;
            }

            if (Check(Token.SELECT))
            {
                return new SelectStatement()
                {
                    Expression = union(),
                    CommonTables = root
                };
            }
            else if (Check(Token.STREAM))
            {
                SelectStatement select = stream_statement();
                select.CommonTables = root;
                return select;
            }
            else if (Check(Token.INSERT))
            {
                InsertStatement insert = insert_statement();
                insert.CommonTables = root;
                return insert;
            }
            else if (Check(Token.UPDATE))
            {
                UpdateStatement update = update_statement();
                update.CommonTables = root;
                return update;
            }
            else if (Check(Token.DELETE))
            {
                DeleteStatement delete = delete_statement();
                delete.CommonTables = root;
                return delete;
            }
            else if (Check(Token.UPSERT))
            {
                UpsertStatement upsert = upsert_statement();
                upsert.CommonTables = root;
                return upsert;
            }

            throw new FormatException("Statement expected.");
        }
        private CommonTableExpression cte()
        {
            if (!Match(Token.Identifier))
            {
                throw new FormatException("Table identifier expected.");
            }

            CommonTableExpression cte = new()
            {
                Name = Previous().Value
            };

            Skip(Token.Comment);

            if (!Match(Token.AS))
            {
                throw new FormatException("AS keyword expected.");
            }

            Skip(Token.Comment);

            if (!Match(Token.OpenRoundBracket))
            {
                throw new FormatException("Open round bracket expected.");
            }

            if (Check(Token.INSERT)) { cte.Expression = insert_statement(); }
            else if (Check(Token.UPDATE)) { cte.Expression = update_statement(); }
            else if (Check(Token.DELETE)) { cte.Expression = delete_statement(); }
            else
            {
                cte.Expression = union(); // SELECT
            }

            Skip(Token.Comment);

            if (!Match(Token.CloseRoundBracket))
            {
                throw new FormatException("Close round bracket expected.");
            }

            Skip(Token.Comment);

            return cte;
        }
        #endregion

        #region "SELECT STATEMENT"
        private SyntaxNode select_statement() { return new SelectStatement() { Expression = union() }; }
        private SelectStatement stream_statement()
        {
            if (Current().Token == Token.STREAM)
            {
                Current().Override(Token.SELECT);
            }

            SelectStatement select = new()
            {
                IsStream = true,
                Expression = union()
            };

            ValidateStreamStatement(in select);

            return select;
        }
        private void ValidateStreamStatement(in SelectStatement node)
        {
            if (node.Expression is SelectExpression select)
            {
                ValidateStreamStatement(in select);
            }
            else if (node.Expression is TableUnionOperator union)
            {
                ValidateStreamStatement(in union);
            }
        }
        private void ValidateStreamStatement(in SelectExpression node)
        {
            if (node.Into is null)
            {
                throw new FormatException("[STREAM] INTO clause expected");
            }
            else if (node.Into.Value is null)
            {
                throw new FormatException("[STREAM] INTO variable expected");
            }
        }
        private void ValidateStreamStatement(in TableUnionOperator node)
        {
            if (node.Expression1 is SelectExpression select)
            {
                ValidateStreamStatement(in select);
            }
        }
        ///<returns>SelectExpression or TableUnionOperator</returns>
        private SyntaxNode union()
        {
            SyntaxNode node = union_operator();

            if (node is not TableUnionOperator _union)
            {
                return node; // SelectExpression
            }

            SyntaxNode bottom = _union.Expression2;

            if (bottom is SelectExpression subordinate)
            {
                subordinate.IsUnionSubordinate = true; // final subordinate
            }

            while (bottom is TableUnionOperator next)
            {
                if (next.Expression1 is SelectExpression select1)
                {
                    select1.IsUnionSubordinate = true;
                }

                if (next.Expression2 is SelectExpression select2)
                {
                    select2.IsUnionSubordinate = true; // final subordinate
                }

                bottom = next.Expression2;
            }

            if (bottom is not SelectExpression select)
            {
                throw new FormatException("UNION: SELECT expression expected.");
            }

            if (select.Order is not null)
            {
                _union.Order = select.Order;

                select.Order = null;
            }
            else if (Previous().Token == Token.CloseRoundBracket)
            {
                if (Match(Token.ORDER)) { _union.Order = order_clause(); }
            }
            
            return _union;
        }
        ///<returns>SelectExpression or TableUnionOperator</returns>
        private SyntaxNode union_operator()
        {
            SyntaxNode node;

            if (Match(Token.OpenRoundBracket))
            {
                node = select();

                if (!Match(Token.CloseRoundBracket))
                {
                    throw new FormatException("Close round bracket expected.");
                }
            }
            else
            {
                node = select();
            }

            if (Match(Token.UNION))
            {
                if (node is SelectExpression select && select.Order is not null)
                {
                    throw new FormatException("UNION: unexpected ORDER BY clause.");
                }

                Skip(Token.Comment);

                Token _operator = Match(Token.ALL) ? Token.UNION_ALL : Token.UNION;

                Skip(Token.Comment);

                node = new TableUnionOperator()
                {
                    Token = _operator,
                    Expression1 = node,
                    Expression2 = union_operator()
                };
            }
            
            return node;
        }
        private SelectExpression select()
        {
            if (!Match(Token.SELECT))
            {
                throw new FormatException("SELECT keyword expected.");
            }

            SelectExpression select = new();

            Skip(Token.Comment);
            select_clause(in select);
            Skip(Token.Comment);
            if (Match(Token.INTO)) { select.Into = into_clause(); }
            Skip(Token.Comment);
            if (Match(Token.FROM)) { select.From = from_clause(); }
            Skip(Token.Comment);
            if (Match(Token.WHERE)) { select.Where = where_clause(); }
            Skip(Token.Comment);
            if (Match(Token.GROUP)) { select.Group = group_clause(); }
            Skip(Token.Comment);
            if (Match(Token.HAVING)) { select.Having = having_clause(); }
            Skip(Token.Comment);
            if (Match(Token.ORDER)) { select.Order = order_clause(); }
            Skip(Token.Comment);

            return select;
        }
        ///<returns>TableReference or TableExpression</returns>
        private SyntaxNode table()
        {
            if (Match(Token.Identifier, Token.Variable))
            {
                string identifier = Previous().Value;

                return new TableReference()
                {
                    Alias = alias(),
                    Identifier = identifier
                };
            }

            if (!Match(Token.OpenRoundBracket))
            {
                throw new FormatException("Open round bracket expected.");
            }

            TableExpression table = new() { Expression = union() };

            if (!Match(Token.CloseRoundBracket))
            {
                throw new FormatException("Close round bracket expected.");
            }

            table.Alias = alias(); //NOTE: the function can return an empty string alias

            return table;
        }
        ///<returns>TableReference or TableExpression or TableJoinOperator</returns>
        private SyntaxNode join()
        {
            SyntaxNode left = table();

            if (left is TableExpression expression)
            {
                disable_correlation_flag(in expression);
            }

            Skip(Token.Comment);

            while (Match(Token.LEFT, Token.RIGHT, Token.INNER,
                Token.FULL, Token.CROSS, Token.OUTER, Token.APPEND))
            {
                Token _operator = Previous().Token;

                Token modifier = Token.Array;

                if (_operator == Token.APPEND)  //THINK: make "ARRAY" and "OBJECT" keywords !?
                {
                    if (Match(Token.Identifier) && DataType.TryParse(Previous().Value.ToLower(), out DataType type, out string schema))
                    {
                        if (type.IsArray)
                        {
                            modifier = Token.Array;
                        }
                        else if (type.IsObject)
                        {
                            modifier = Token.Object;
                        }
                    }
                }
                else if (Match(Token.APPLY))
                {
                    if (_operator == Token.CROSS) { _operator = Token.CROSS_APPLY; }
                    else if (_operator == Token.OUTER) { _operator = Token.OUTER_APPLY; }
                    else { throw new FormatException("[APPLY] CROSS or OUTER keyword expected"); }
                }
                else if (!Match(Token.JOIN))
                {
                    throw new FormatException("JOIN keyword expected.");
                }

                bool parse_on_clause = false;

                SyntaxNode right = table();

                TableExpression subquery = right as TableExpression;

                if (subquery is not null && string.IsNullOrEmpty(subquery.Alias))
                {
                    throw new FormatException($"[{_operator}] Table expression alias expected.");
                }

                if (_operator == Token.CROSS_APPLY ||
                    _operator == Token.OUTER_APPLY ||
                    _operator == Token.APPEND)
                {
                    if (subquery is null)
                    {
                        throw new FormatException($"[{_operator}] Table expression expected.");
                    }
                }
                else if (_operator == Token.CROSS) //NOTE: CROSS JOIN operator does not use ON clause
                {
                    if (subquery is not null)
                    {
                        disable_correlation_flag(in subquery);
                    }
                }
                else if (Match(Token.ON)) // { LEFT | RIGHT | INNER | FULL } JOIN
                {
                    parse_on_clause = true;

                    if (subquery is not null)
                    {
                        disable_correlation_flag(in subquery);
                    }
                }
                else
                {
                    throw new FormatException("ON keyword expected.");
                }

                if (parse_on_clause)
                {
                    Skip(Token.Comment);
                }

                if (_operator == Token.APPEND)
                {
                    disable_correlation_flag(in subquery);
                }

                left = new TableJoinOperator()
                {
                    Token = _operator,
                    Expression1 = left,
                    Expression2 = right,
                    On = parse_on_clause ? on_clause() : null,
                    Modifier = _operator == Token.APPEND ? modifier : _operator
                };
            }

            return left;
        }
        private void disable_correlation_flag(in SyntaxNode node)
        {
            if (node is SelectExpression select)
            {
                select.IsCorrelated = false;
            }
            else if (node is TableExpression table)
            {
                disable_correlation_flag(in table);
            }
            else if (node is TableUnionOperator union)
            {
                disable_correlation_flag(in union);
            }
        }
        private void disable_correlation_flag(in TableExpression table)
        {
            disable_correlation_flag(table.Expression);
        }
        private void disable_correlation_flag(in TableUnionOperator union)
        {
            disable_correlation_flag(union.Expression1);
            disable_correlation_flag(union.Expression2);
        }
        private FromClause from_clause() { return new FromClause() { Expression = join() }; }
        private IntoClause into_clause()
        {
            IntoClause clause = new();

            if (Match(Token.Identifier))
            {
                clause.Table = new TableReference()
                {
                    Identifier = Previous().Value
                };
                return clause;
            }
            else if (Match(Token.Variable))
            {
                clause.Value = new VariableReference()
                {
                    Identifier = Previous().Value
                };
                return clause;
            }

            throw new FormatException("INTO: table or variable identifier expected.");
        }
        private string alias()
        {
            if (Match(Token.AS))
            {
                if (Match(Token.Identifier, Token.TYPE))
                {
                    return Previous().Value;
                }
                else
                {
                    throw new FormatException("Alias expected.");
                }
            }
            else if (Match(Token.Identifier))
            {
                return Previous().Value;
            }

            return string.Empty;
        }
        private ColumnExpression column()
        {
            if (Match(Token.Star))
            {
                return new ColumnExpression()
                {
                    Expression = new StarExpression(),
                    Alias = alias()
                };
            }

            ColumnExpression definition;

            SyntaxNode node = expression();

            if (node is ComparisonOperator assignment) //NOTE: Summa = SUM(t1.Value)
            {
                if (assignment.Token != Token.Equals)
                {
                    throw new FormatException("Column definition error: assignment expected");
                }

                if (assignment.Expression1 is not ColumnReference alias) // left operand
                {
                    throw new FormatException("Column definition error: identifier expected");
                }

                definition = new ColumnExpression() //THINK: multi-part identifier assignment
                {
                    Alias = alias.Identifier,           // left  operand (column name)
                    Expression = assignment.Expression2 // right operand (initializer)
                };
            }
            else
            {
                definition = new ColumnExpression() //NOTE: SUM(t1.Value) AS Summa
                {
                    Expression = node, Alias = alias()
                };
            }

            if (definition.Expression is ColumnReference column)
            {
                column.Parent = definition; // Нужно для доступа к Alias при генерации SQL
            }

            return definition;
        }
        private ColumnReference column_identifier()
        {
            if (!Match(Token.Identifier))
            {
                throw new FormatException("Column identifier expected.");
            }

            return new ColumnReference() { Identifier = Previous().Value };
        }
        private void parse_column_references(in List<ColumnReference> columns)
        {
            columns.Add(column_identifier());

            Skip(Token.Comment);

            while (Match(Token.Comma))
            {
                Skip(Token.Comment);

                columns.Add(column_identifier());

                Skip(Token.Comment);
            }
        }
        private void parse_column_expressions(in List<ColumnExpression> columns)
        {
            columns.Add(column());

            Skip(Token.Comment);

            while (Match(Token.Comma))
            {
                Skip(Token.Comment);

                columns.Add(column());

                Skip(Token.Comment);
            }
        }
        private void select_clause(in SelectExpression select)
        {
            Skip(Token.Comment);
            select.Distinct = Match(Token.DISTINCT);
            Skip(Token.Comment);

            Skip(Token.Comment);
            select.Top = top_clause();
            Skip(Token.Comment);

            select.Columns.Add(column());

            Skip(Token.Comment);

            while (Match(Token.Comma))
            {
                Skip(Token.Comment);

                select.Columns.Add(column());

                Skip(Token.Comment);
            }
        }
        private TopClause top_clause()
        {
            if (!Match(Token.TOP))
            {
                return null;
            }

            bool expect_close = Match(Token.OpenRoundBracket);

            TopClause clause = new() { Expression = expression() };

            if (expect_close && !Match(Token.CloseRoundBracket))
            {
                throw new FormatException("TOP clause: close round bracket expected.");
            }

            return clause;
        }
        private OnClause on_clause() { return new OnClause() { Expression = predicate() }; }
        private WhereClause where_clause() { return new WhereClause() { Expression = predicate() }; }
        private HavingClause having_clause() { return new HavingClause() { Expression = predicate() }; }
        private GroupClause group_clause()
        {
            if (!Match(Token.BY))
            {
                throw new FormatException("BY keyword expected.");
            }

            GroupClause group = new();

            Skip(Token.Comment);

            group.Expressions.Add(expression());

            Skip(Token.Comment);

            while (Match(Token.Comma))
            {
                Skip(Token.Comment);

                group.Expressions.Add(expression());

                Skip(Token.Comment);
            }

            return group;
        }
        private OrderClause order_clause()
        {
            if (!Match(Token.BY))
            {
                throw new FormatException("BY keyword expected.");
            }

            OrderClause order = new();

            Skip(Token.Comment);

            order.Expressions.Add(order_expression());

            Skip(Token.Comment);

            while (Match(Token.Comma))
            {
                Skip(Token.Comment);
                
                order.Expressions.Add(order_expression());

                Skip(Token.Comment);
            }

            if (Match(Token.OFFSET))
            {
                order.Offset = expression();

                if (!Match(Token.ROW, Token.ROWS))
                {
                    throw new FormatException("ROW or ROWS keyword expected.");
                }
            }

            if (Match(Token.FETCH))
            {
                if (!Match(Token.FIRST, Token.NEXT))
                {
                    throw new FormatException("FIRST or NEXT keyword expected.");
                }

                order.Fetch = expression();

                if (!Match(Token.ROW, Token.ROWS))
                {
                    throw new FormatException("ROW or ROWS keyword expected.");
                }

                if (!Match(Token.ONLY))
                {
                    throw new FormatException("ROW or ROWS keyword expected.");
                }
            }

            return order;
        }
        private OrderExpression order_expression()
        {
            SyntaxNode column = expression();

            Token sort_order = Token.ASC;

            if (Match(Token.ASC, Token.DESC))
            {
                sort_order = Previous().Token;
            }

            return new OrderExpression()
            {
                Token = sort_order,
                Expression = column
            };
        }
        #endregion

        #region "PREDICATE"
        private SyntaxNode predicate()
        {
            return or();
        }
        private SyntaxNode or()
        {
            SyntaxNode left = and();

            while (Match(Token.OR))
            {
                SyntaxNode right = and();

                left = new BinaryOperator()
                {
                    Token = Token.OR,
                    Expression1 = left,
                    Expression2 = right
                };
            }

            return left;
        }
        private SyntaxNode and()
        {
            SyntaxNode left = not();

            while (Match(Token.AND))
            {
                SyntaxNode right = not();

                left = new BinaryOperator()
                {
                    Token = Token.AND,
                    Expression1 = left,
                    Expression2 = right
                };
            }

            return left;
        }
        private SyntaxNode not()
        {
            if (Match(Token.NOT))
            {
                SyntaxNode unary = not();

                return new UnaryOperator()
                {
                    Token = Token.NOT,
                    Expression = unary
                };
            }

            return expression();
        }
        #endregion

        #region "EXPRESSION"
        private SyntaxNode expression()
        {
            return comparison();
        }
        private SyntaxNode comparison()
        {
            SyntaxNode left = addition();

            Skip(Token.Comment);

            while (Match(Token.IS,
                Token.Equals, Token.NotEquals,
                Token.Greater, Token.GreaterOrEquals,
                Token.Less, Token.LessOrEquals,
                Token.NOT, Token.IN, Token.LIKE, Token.BETWEEN))
            {
                Token _operator = Previous().Token;

                bool negate = (_operator == Token.NOT);

                if (negate)
                {
                    if (Match(Token.IN, Token.LIKE, Token.BETWEEN))
                    {
                        _operator = Previous().Token;
                    }
                    else
                    {
                        throw new FormatException("IN, LIKE or BETWEEN keyword expected");
                    }
                }

                if (_operator == Token.IS)
                {
                    return new ComparisonOperator()
                    {
                        Token = _operator,
                        Expression1 = left,
                        Expression2 = is_right_operand()
                    };
                }
                else if (_operator == Token.IN)
                {
                    return new ComparisonOperator()
                    {
                        Token = _operator,
                        Modifier = negate ? Token.NOT : Token.Ignore,
                        Expression1 = left,
                        Expression2 = in_right_operand()
                    };
                }
                else if (_operator == Token.LIKE)
                {
                    ComparisonOperator expression = new()
                    {
                        Token = _operator,
                        Expression1 = left,
                        Expression2 = terminal()
                    };

                    if (!(expression.Expression2 is ScalarExpression
                        || expression.Expression2 is VariableReference))
                    {
                        throw new FormatException("[LIKE] string pattern or variable reference expected");
                    }

                    if (negate) { expression.Modifier = Token.NOT; }

                    return expression;
                }
                else if (_operator == Token.BETWEEN)
                {
                    ComparisonOperator expression = new()
                    {
                        Token = _operator,
                        Expression1 = left,
                        Expression2 = and()
                    };

                    if (expression.Expression2 is not BinaryOperator)
                    {
                        throw new FormatException("[BETWEEN] AND operator expected");
                    }

                    if (negate) { expression.Modifier = Token.NOT; }

                    return expression;
                }
                else if (Match(Token.ALL, Token.ANY))
                {
                    Token modifier = Previous().Token;

                    ComparisonOperator expression = new()
                    {
                        Token = _operator,
                        Modifier = modifier,
                        Expression1 = left,
                        Expression2 = table()
                    };

                    if (expression.Expression2 is not TableExpression)
                    {
                        throw new FormatException($"[{modifier}] table expression expected");
                    }

                    return expression;
                }
                else
                {
                    left = new ComparisonOperator()
                    {
                        Token = _operator,
                        Expression1 = left,
                        Expression2 = addition()
                    };
                }
            }

            return left;
        }
        ///<returns>TableExpression or ValuesExpression</returns>
        private SyntaxNode in_right_operand()
        {
            if (!Match(Token.OpenRoundBracket))
            {
                throw new FormatException("[IN] Open round bracket expected.");
            }

            Token token = Current().Token;

            SyntaxNode expression = (token == Token.SELECT)
                ? new TableExpression() { Expression = union() }
                : new ValuesExpression() { Values = array_of_values() };

            if (!Match(Token.CloseRoundBracket))
            {
                throw new FormatException("[IN] Close round bracket expected.");
            }

            return expression;
        }
        private SyntaxNode is_right_operand()
        {
            if (Match(Token.NOT))
            {
                UnaryOperator unary = new()
                {
                    Token = Token.NOT
                };

                if (Match(Token.NULL))
                {
                    unary.Expression = scalar();
                }
                else if (Match(Token.Identifier))
                {
                    unary.Expression = type_reference();
                }
                else
                {
                    throw new FormatException($"[IS] NULL or type identifier expected.");
                }

                return unary;
            }
            else if (Match(Token.NULL))
            {
                return scalar();
            }
            else if (Match(Token.Identifier))
            {
                return type_reference();
            }

            throw new FormatException($"[IS] NOT token, NULL or type identifier expected.");
        }
        private SyntaxNode addition()
        {
            SyntaxNode left = multiply();

            while (Match(Token.Comment, Token.Plus, Token.Minus))
            {
                if (Previous().Token == Token.Comment)
                {
                    continue; // ignore
                }

                Token _operator = Previous().Token;

                SyntaxNode right = multiply();

                left = new AdditionOperator()
                {
                    Token = _operator,
                    Expression1 = left,
                    Expression2 = right
                };
            }

            return left;
        }
        private SyntaxNode multiply()
        {
            SyntaxNode left = unary();

            while (Match(Token.Comment, Token.Star, Token.Divide, Token.Modulo))
            {
                if (Previous().Token == Token.Comment)
                {
                    continue; // ignore
                }

                Token _operator = Previous().Token;

                SyntaxNode right = unary();

                left = new MultiplyOperator()
                {
                    Token = _operator,
                    Expression1 = left,
                    Expression2 = right
                };
            }

            return left;
        }
        private SyntaxNode unary()
        {
            if (Match(Token.Minus, Token.NOT))
            {
                Token _operator = Previous().Token;

                SyntaxNode expression = unary();

                return new UnaryOperator()
                {
                    Token = _operator,
                    Expression = expression
                };
            }

            return grouping();
        }
        ///<returns>GroupOperator (round brackets) or TableExpression without alias</returns>
        private SyntaxNode grouping()
        {
            if (Match(Token.OpenRoundBracket))
            {
                Token token = Current().Token;

                SyntaxNode expression = (token == Token.SELECT)
                    ? union() //NOTE: SelectExpression | TableUnionOperator
                    : predicate(); //NOTE: recursion

                if (!Match(Token.CloseRoundBracket))
                {
                    throw new FormatException("Close round bracket token expected.");
                }

                return (token == Token.SELECT)
                    ? new TableExpression() { Expression = expression }
                    : new GroupOperator() { Expression = expression };
            }

            return terminal();
        }
        private SyntaxNode terminal()
        {
            Skip(Token.Comment);

            if (Match(Token.Identifier))
            {
                return identifier();
            }
            else if (Match(Token.Variable))
            {
                return variable();
            }
            else if (Match(Token.Boolean, Token.Number, Token.DateTime,
                Token.String, Token.Binary, Token.NULL, Token.Entity))
            {
                return scalar();
            }
            else if (Match(Token.Star))
            {
                return star();
            }
            else if (Match(Token.CASE))
            {
                return case_expression();
            }
            else if (Match(Token.EXISTS))
            {
                return exists_function();
            }
            else if (Match(Token.TYPE)) //NOTE: exceptional keyword - see also alias() function
            {
                return new ColumnReference() { Identifier = Previous().Value };
            }

            Ignore();

            throw new FormatException($"Unknown expression: {Previous()}");
        }
        private SyntaxNode star()
        {
            return new StarExpression();
        }
        private SyntaxNode scalar()
        {
            ScalarExpression scalar = new()
            {
                Token = Previous().Token,
                Literal = Previous().Value
            };

            if (scalar.Token == Token.String && scalar.Literal.Length >= 10)
            {
                if (Guid.TryParse(scalar.Literal, out Guid _))
                {
                    scalar.Token = Token.Uuid;
                }
                else if (DateTime.TryParse(scalar.Literal, out DateTime _))
                {
                    scalar.Token = Token.DateTime;
                }
            }
            else if (scalar.Token == Token.Number)
            {
                scalar.Token = scalar.Literal.Contains('.') ? Token.Decimal : Token.Integer;
            }

            return scalar;
        }
        private SyntaxNode variable()
        {
            string identifier = Previous().Value;

            if (identifier.Contains('.') || identifier.Contains('['))
            {
                //NOTE: multi-part identifier, index or selector member access

                return new MemberAccessExpression()
                {
                    Identifier = identifier
                };
            }
            else
            {
                return new VariableReference()
                {
                    Identifier = identifier
                };
            }
        }
        ///<returns>ColumnReference or FunctionExpression</returns>
        private SyntaxNode identifier()
        {
            string identifier = Previous().Value;

            if (LexerHelper.IsFunction(identifier, out Token token))
            {
                return function(token, identifier); // language built-in function
            }
            
            if (Check(Token.OpenRoundBracket)) //TODO: check UDF.TryGet !?
            {
                return function(Token.UDF, identifier); // user-defined function
            }

            return new ColumnReference() { Identifier = identifier };
        }
        private SyntaxNode case_expression()
        {
            CaseExpression node = new();

            Skip(Token.Comment);

            while (Match(Token.WHEN))
            {
                node.CASE.Add(when_expression());
            }

            if (Match(Token.ELSE))
            {
                node.ELSE = expression();
            }

            if (!Match(Token.END))
            {
                throw new FormatException("[CASE] END keyword expected");
            }

            return node;
        }
        private WhenClause when_expression()
        {
            WhenClause node = new()
            {
                WHEN = predicate()
            };

            if (!Match(Token.THEN))
            {
                throw new FormatException($"THEN keyword expected.");
            }

            node.THEN = expression();

            return node;
        }
        private FunctionExpression exists_function()
        {
            SyntaxNode parameter = table();

            if (parameter is not TableExpression)
            {
                throw new FormatException("[EXISTS] table expression expected");
            }

            FunctionExpression function = new()
            {
                Name = "EXISTS",
                Token = Token.EXISTS
            };

            function.Parameters.Add(parameter);

            return function;
        }
        private List<SyntaxNode> array_of_values()
        {
            List<SyntaxNode> values = new()
            {
                terminal()
            };

            while (Match(Token.Comma))
            {
                values.Add(terminal());
            }

            return values;
        }
        private List<VariableReference> array_of_variables()
        {
            if (!Match(Token.Variable) || variable() is not VariableReference var_0)
            {
                throw new FormatException("Variable identifier expected");
            }

            List<VariableReference> variables = new() { var_0 };

            Skip(Token.Comment);

            while (Match(Token.Comma))
            {
                Skip(Token.Comment);

                if (!Match(Token.Variable) || variable() is not VariableReference var_N)
                {
                    throw new FormatException("Variable identifier expected");
                }

                variables.Add(var_N);

                Skip(Token.Comment);
            }

            return variables;
        }
        #endregion

        #region "FUNCTION"
        private SyntaxNode function(Token token, string identifier)
        {
            if (!Match(Token.OpenRoundBracket))
            {
                throw new FormatException($"[{identifier}] Open round bracket expected.");
            }

            FunctionExpression function = new()
            {
                Token = token,
                Name = identifier
            };

            if (identifier == "CAST")
            {
                return parse_function_cast(in function);
            }

            if (token == Token.COUNT && Match(Token.DISTINCT))
            {
                function.Modifier = Token.DISTINCT;
            }

            if (Match(Token.CloseRoundBracket))
            {
                // the function does not have any parameters
            }
            else
            {
                function.Parameters.Add(expression());

                while (Match(Token.Comma))
                {
                    function.Parameters.Add(expression());
                }

                if (!Match(Token.CloseRoundBracket))
                {
                    throw new FormatException($"[{identifier}] Close round bracket expected.");
                }
            }

            if (Match(Token.OVER))
            {
                function.Over = over_clause();
            }

            //THINK: implement function parameters validation
            if (function.Name.ToUpperInvariant() == "VECTOR")
            {
                if (function.Parameters is null)
                {
                    throw new FormatException("VECTOR function: missing parameter.");
                }
                else if (function.Parameters.Count == 0)
                {
                    throw new FormatException("VECTOR function: missing parameter.");
                }
                else if (function.Parameters.Count > 1)
                {
                    throw new FormatException("VECTOR function: too many parameters.");
                }

                if (function.Parameters[0] is not ScalarExpression scalar || scalar.Token != Token.String)
                {
                    throw new FormatException("VECTOR function: string parameter type expected.");
                }

                if (string.IsNullOrWhiteSpace(scalar.Literal))
                {
                    throw new FormatException("VECTOR function: parameter value must be non-empty string.");
                }
            }

            return function;
        }
        private FunctionExpression parse_function_cast(in FunctionExpression function)
        {
            function.Parameters.Add(expression());

            if (!Match(Token.AS))
            {
                throw new FormatException("[CAST] AS keyword expected.");
            }

            if (!Match(Token.Identifier))
            {
                throw new FormatException("[CAST] Type identifier expected.");
            }
            else
            {
                function.Parameters.Add(type_reference());
            }

            if (!Match(Token.CloseRoundBracket))
            {
                throw new FormatException("[CAST] Close round bracket expected.");
            }

            return function;
        }
        private OverClause over_clause()
        {
            OverClause over = new();

            if (!Match(Token.OpenRoundBracket))
            {
                throw new FormatException("Open round bracket expected.");
            }

            if (Match(Token.PARTITION)) // optional
            {
                over.Partition = partition();
            }

            if (Match(Token.ORDER)) // optional
            {
                over.Order = order_clause();
            }

            if (Match(Token.ROWS, Token.RANGE))
            {
                over.FrameType = Previous().Token;

                if (Match(Token.BETWEEN)) // optional
                {
                    over.Preceding = window_frame(Token.PRECEDING);

                    if (!Match(Token.AND))
                    {
                        throw new FormatException("AND keyword expected.");
                    }

                    over.Following = window_frame(Token.FOLLOWING);
                }
                else
                {
                    over.Preceding = window_frame(Token.PRECEDING);
                }
            }
            
            if (!Match(Token.CloseRoundBracket))
            {
                throw new FormatException("Close round bracket expected.");
            }

            return over;
        }
        private PartitionClause partition()
        {
            if (!Match(Token.BY))
            {
                throw new FormatException("BY keyword expected.");
            }

            PartitionClause clause = new();

            clause.Columns.Add(expression());

            while (Match(Token.Comma))
            {
                clause.Columns.Add(expression());
            }

            return clause;
        }
        private WindowFrame window_frame(Token token)
        {
            WindowFrame frame = new() { Token = token };

            if (Match(Token.UNBOUNDED))
            {
                frame.Extent = -1;

                if (!Match(token))
                {
                    throw new FormatException($"{token} keyword expected.");
                }
            }
            else if (Match(Token.CURRENT))
            {
                frame.Extent = 0;

                if (!Match(Token.ROW))
                {
                    throw new FormatException("ROW keyword expected.");
                }
            }
            else if (Match(Token.Number))
            {
                frame.Extent = int.Parse(Previous().Value);

                if (!Match(token))
                {
                    throw new FormatException($"{token} keyword expected.");
                }
            }
            else
            {
                return null!;
            }

            return frame;
        }
        #endregion

        #region "INSERT STATEMENT"
        private InsertStatement insert_statement()
        {
            if (!Match(Token.INSERT))
            {
                throw new FormatException("INSERT keyword expected.");
            }

            if (Match(Token.INTO)) { /* do nothing - optional */ }

            InsertStatement insert = new()
            {
                Target = table_identifier()
            };

            if (Match(Token.FROM))
            {
                insert.Source = table();
            }
            else if (Check(Token.SELECT))
            {
                insert.Source = union();
            }
            else
            {
                throw new FormatException("INSERT: table source expression expected.");
            }
            
            return insert;
        }
        private SyntaxNode values()
        {
            if (!Match(Token.VALUES))
            {
                throw new FormatException("VALUES keyword expected.");
            }
            
            if (!Match(Token.OpenRoundBracket))
            {
                throw new FormatException("Open round bracket expected.");
            }

            ValuesExpression values = new();

            values.Values.Add(expression());

            while (Match(Token.Comma))
            {
                values.Values.Add(expression());
            }

            if (!Match(Token.CloseRoundBracket))
            {
                throw new FormatException("Close round bracket expected.");
            }

            return values;
        }
        private TableReference table_identifier()
        {
            if (!Match(Token.Identifier))
            {
                throw new FormatException("INSERT: target identifier expected.");
            }
            
            return new TableReference() { Identifier = Previous().Value };
        }
        #endregion

        #region "UPDATE STATEMENT"
        private UpdateStatement update_statement()
        {
            if (!Match(Token.UPDATE)) { throw new FormatException("UPDATE keyword expected."); }

            UpdateStatement update = new()
            {
                Target = table_identifier()
            };

            if (Match(Token.FROM)) // FROM-WHERE-SET | FROM-SET-WHERE
            {
                update.Source = table();
                if (Match(Token.WHERE))
                {
                    update.Where = where_clause();
                    if (Match(Token.SET)) { update.Set = set_clause(update); }
                }
                else if (Match(Token.SET))
                {
                    update.Set = set_clause(update);
                    if (Match(Token.WHERE)) { update.Where = where_clause(); }
                }
            }
            else if (Match(Token.WHERE)) // WHERE-FROM-SET | WHERE-SET-FROM
            {
                update.Where = where_clause();
                if (Match(Token.FROM))
                {
                    update.Source = table();
                    if (Match(Token.SET)) { update.Set = set_clause(update); }
                }
                else if (Match(Token.SET))
                {
                    update.Set = set_clause(update);
                    if (Match(Token.FROM)) { update.Source = table(); }
                }
            }
            else if (Match(Token.SET)) // SET-FROM-WHERE | SET-WHERE-FROM
            {
                update.Set = set_clause(update);
                if (Match(Token.FROM))
                {
                    update.Source = table();
                    if (Match(Token.WHERE)) { update.Where = where_clause(); }
                }
                else if (Match(Token.WHERE))
                {
                    update.Where = where_clause();
                    if (Match(Token.FROM)) { update.Source = table(); }
                }
            }

            if (Match(Token.OUTPUT))
            {
                update.Output = output_clause();
            }

            // The WHERE and FROM clauses are optional
            if (update.Set is null) { throw new FormatException("UPDATE: SET keyword expected."); }

            return update;
        }
        private SetExpression set_expression()
        {
            SetExpression set = new()
            {
                Column = column_identifier()
            };

            if (!Match(Token.Equals))
            {
                throw new FormatException("Assignment operator expected.");
            }

            set.Initializer = expression();

            return set;
        }
        private SetClause set_clause(SyntaxNode parent)
        {
            SetClause clause = new() { Parent = parent };

            clause.Expressions.Add(set_expression());

            while (Match(Token.Comma))
            {
                clause.Expressions.Add(set_expression());
            }

            return clause;
        }
        #endregion

        #region "DELETE STATEMENT"
        private DeleteStatement delete_statement()
        {
            if (!Match(Token.DELETE))
            {
                throw new FormatException("DELETE keyword expected.");
            }

            DeleteStatement delete = new();

            if (Match(Token.FROM)) { /* do nothing - optional */ }

            delete.Target = table_identifier();

            if (Match(Token.OUTPUT))
            {
                delete.Output = output_clause();
            }

            if (Match(Token.WHERE))
            {
                delete.Where = where_clause();
            }

            return delete;
        }
        private OutputClause output_clause()
        {
            OutputClause output = new();

            Skip(Token.Comment);
            output.Columns.Add(column());
            Skip(Token.Comment);

            while (Match(Token.Comma))
            {
                Skip(Token.Comment);
                output.Columns.Add(column());
                Skip(Token.Comment);
            }

            Skip(Token.Comment);
            if (Match(Token.INTO)) { output.Into = into_clause(); }
            Skip(Token.Comment);

            return output;
        }
        #endregion

        #region "UPSERT STATEMENT"
        private UpsertStatement upsert_statement()
        {
            if (!Match(Token.UPSERT))
            {
                throw new FormatException("UPSERT keyword expected.");
            }

            UpsertStatement upsert = new()
            {
                Target = table_identifier()
            };

            bool ignore = Match(Token.IGNORE);
            bool update = Match(Token.UPDATE);
            if (ignore && !update)
            {
                throw new FormatException("UPSERT: UPDATE keyword is missing.");
            }
            else if (update && !ignore)
            {
                throw new FormatException("UPSERT: IGNORE keyword is missing.");
            }
            upsert.IgnoreUpdate = ignore && update;

            bool from = Match(Token.FROM);
            if (from) { upsert.Source = table(); }

            if (Match(Token.WHERE)) { upsert.Where = where_clause(); }
            else { throw new FormatException("UPSERT: WHERE keyword expected."); }

            if (Match(Token.SET)) { upsert.Set = set_clause(upsert); }
            else { throw new FormatException("UPSERT: SET keyword expected."); }

            if (from)
            {
                if (Check(Token.FROM))
                {
                    throw new FormatException("UPSERT: FROM clause is used twice.");
                }
            }
            else if (Match(Token.FROM)) { upsert.Source = table(); }
            else { throw new FormatException("UPSERT: FROM keyword expected."); }

            return upsert;
        }
        #endregion

        #region "CREATE AND DEFINE TYPE STATEMENT"
        private SyntaxNode create_type()
        {
            CreateTypeStatement statement = new();

            if (!Match(Token.Identifier))
            {
                throw new FormatException("Type identifier expected.");
            }

            statement.Identifier = Previous().Value;

            if (!Match(Token.OpenRoundBracket))
            {
                throw new FormatException("Open round bracket expected.");
            }

            statement.Columns.Add(column_definition());

            while (Match(Token.Comma))
            {
                statement.Columns.Add(column_definition());
            }

            if (!Match(Token.CloseRoundBracket))
            {
                throw new FormatException("Close round bracket expected.");
            }

            return statement;
        }
        #endregion

        #region "CREATE AND APPLY SEQUENCE (VECTOR)"
        private SyntaxNode drop_sequence()
        {
            DropSequenceStatement statement = new();

            if (!Match(Token.Identifier)) { throw new FormatException("Sequence identifier expected."); }

            statement.Identifier = Previous().Value;

            return statement;
        }
        private SyntaxNode create_sequence()
        {
            CreateSequenceStatement statement = new();

            if (!Match(Token.Identifier)) { throw new FormatException("Sequence identifier expected."); }

            statement.Identifier = Previous().Value;

            if (Match(Token.AS))
            {
                if (!Match(Token.Identifier)) { throw new FormatException("Data type identifier expected."); }

                statement.DataType = type_reference();
            }
            
            if (Match(Token.START))
            {
                if (!Match(Token.WITH)) { throw new FormatException("START WITH keyword expected."); }

                if (!Match(Token.Number)) { throw new FormatException("Integer literal expected."); }

                if (!int.TryParse(Previous().Value, out int start))
                {
                    throw new FormatException("Integer literal expected.");
                }

                statement.StartWith = start;
            }
            
            if (Match(Token.INCREMENT))
            {
                if (!Match(Token.BY)) { throw new FormatException("INCREMENT BY keyword expected."); }

                if (!Match(Token.Number)) { throw new FormatException("Integer literal expected."); }

                if (!int.TryParse(Previous().Value, out int increment))
                {
                    throw new FormatException("Integer literal expected.");
                }

                statement.Increment = increment;
            }

            if (Match(Token.CACHE))
            {
                if (!Match(Token.Number)) { throw new FormatException("Integer literal expected."); }

                if (!int.TryParse(Previous().Value, out int cache))
                {
                    throw new FormatException("Integer literal expected.");
                }

                statement.CacheSize = cache;
            }

            return statement;
        }
        private SyntaxNode apply_sequence()
        {
            // APPLY SEQUENCE <sequence> ON <table>(<column>) [RECALCULATE]

            ApplySequenceStatement statement = new();

            if (!Match(Token.Identifier)) { throw new FormatException("[APPLY] Sequence identifier expected"); }

            statement.Identifier = Previous().Value;

            Skip(Token.Comment);

            if (!Match(Token.ON)) { throw new FormatException("[APPLY] ON keyword expected"); }

            if (!Match(Token.Identifier)) { throw new FormatException("[APPLY] Table identifier expected"); }

            statement.Table = new TableReference() { Identifier = Previous().Value };

            if (!Match(Token.OpenRoundBracket)) { throw new FormatException("[APPLY] Open round bracket expected"); }
            
            if (!Match(Token.Identifier)) { throw new FormatException("[APPLY] Column identifier expected"); }

            statement.Column = new ColumnReference() { Identifier = Previous().Value };

            if (!Match(Token.CloseRoundBracket)) { throw new FormatException("[APPLY] Close round bracket expected"); }

            Skip(Token.Comment);

            statement.ReCalculate = Match(Token.RECALCULATE); // optional

            return statement;
        }
        private SyntaxNode revoke_sequence()
        {
            // REVOKE SEQUENCE <sequence> ON <table>

            RevokeSequenceStatement statement = new();

            if (!Match(Token.Identifier)) { throw new FormatException("[REVOKE] Sequence identifier expected"); }

            statement.Identifier = Previous().Value;

            Skip(Token.Comment);

            if (!Match(Token.ON)) { throw new FormatException("[REVOKE] ON keyword expected"); }

            if (!Match(Token.Identifier)) { throw new FormatException("[REVOKE] Table identifier expected"); }

            statement.Table = new TableReference() { Identifier = Previous().Value };

            return statement;
        }
        #endregion

        #region "CONSUME STATEMENT"
        private SyntaxNode consume_statement()
        {
            if (!Match(Token.CONSUME))
            {
                throw new FormatException("CONSUME keyword expected.");
            }

            ConsumeStatement consume = new();

            Skip(Token.Comment);

            if (Match(Token.String)) //NOTE: stream processor URI
            {
                return consume_stream_statement(in consume);
            }
            else
            {
                consume.Top = top_clause();
            }
            
            Skip(Token.Comment);

            if (consume.Top is null) { throw new FormatException("CONSUME: TOP keyword expected."); }

            if (Match(Token.WITH))
            {
                if (Match(Token.STRICT))
                {
                    consume.StrictOrderRequired = true;
                }
                else if (Match(Token.RANDOM))
                {
                    consume.StrictOrderRequired = false;
                }
                else
                {
                    throw new FormatException($"CONSUME: STRICT or RANDOM keyword expected.");
                }

                if (!Match(Token.ORDER)) { throw new FormatException($"CONSUME: (STRICT or RANDOM) ORDER keyword expected."); }
            }

            Skip(Token.Comment);
            select_columns(in consume);
            Skip(Token.Comment);
            if (Match(Token.INTO)) { consume.Into = into_clause(); }
            Skip(Token.Comment);
            if (Match(Token.FROM)) { consume.From = from_clause(); }
            Skip(Token.Comment);
            if (Match(Token.WHERE)) { consume.Where = where_clause(); }
            Skip(Token.Comment);
            if (Match(Token.ORDER)) { consume.Order = order_clause(); }
            Skip(Token.Comment);

            if (consume.From.Expression is TableReference table && table.Identifier.StartsWith('@'))
            {
                throw new FormatException($"CONSUME {table.Identifier}: table variable targeting is not allowed.");
            }

            return consume;
        }
        private SyntaxNode consume_stream_statement(in ConsumeStatement consume)
        {
            consume.Target = Previous().Value;

            Skip(Token.Comment);

            if (Match(Token.WITH))
            {
                consume_options(in consume);
            }

            Skip(Token.Comment);
            
            if (!Match(Token.INTO))
            {
                throw new FormatException($"CONSUME: INTO keyword expected");
            }

            Skip(Token.Comment);

            consume.Into = into_clause();

            if (consume.Into is null || consume.Into.Value is null)
            {
                throw new FormatException("CONSUME: INTO variable identifier expected");
            }

            Skip(Token.Comment);

            return consume;
        }
        private void select_columns(in ConsumeStatement consume)
        {
            consume.Columns.Add(column());

            Skip(Token.Comment);

            while (Match(Token.Comma))
            {
                Skip(Token.Comment);

                consume.Columns.Add(column());

                Skip(Token.Comment);
            }
        }
        private void consume_options(in ConsumeStatement consume)
        {
            consume.Options.Add(column());

            Skip(Token.Comment);

            while (Match(Token.Comma))
            {
                Skip(Token.Comment);

                consume.Options.Add(column());

                Skip(Token.Comment);
            }
        }
        #endregion

        #region "IMPORT STATEMENT"
        private List<VariableReference> table_variables()
        {
            if (!Match(Token.Variable)) { throw new FormatException("IMPORT: table variable expected."); }

            List<VariableReference> tables = new()
            {
                new VariableReference() { Identifier = Previous().Value }
            };

            while (Match(Token.Comma))
            {
                if (!Match(Token.Variable)) { throw new FormatException("IMPORT: table variable expected."); }

                tables.Add(new VariableReference() { Identifier = Previous().Value });
            }

            return tables;
        }
        #endregion

        #region "PRODUCE STATEMENT"
        private SyntaxNode produce_statement()
        {
            if (!Match(Token.PRODUCE))
            {
                throw new FormatException("PRODUCE keyword expected");
            }

            ProduceStatement produce = new();

            Skip(Token.Comment);

            if (!Match(Token.String))
            {
                throw new FormatException("PRODUCE: uri expected");
            }

            produce.Target = Previous().Value;

            Skip(Token.Comment);

            if (Match(Token.WITH))
            {
                parse_column_expressions(produce.Options);
            }

            if (!Match(Token.SELECT))
            {
                throw new FormatException($"PRODUCE: SELECT keyword expected");
            }

            parse_column_expressions(produce.Columns);

            return produce;
        }
        #endregion

        #region "REQUEST STATEMENT"
        private SyntaxNode request_statement()
        {
            if (!Match(Token.REQUEST))
            {
                throw new FormatException("REQUEST keyword expected");
            }

            RequestStatement request = new();

            Skip(Token.Comment);

            if (!Match(Token.String))
            {
                throw new FormatException("REQUEST: URI template expected");
            }

            request.Target = Previous().Value;

            Skip(Token.Comment);

            if (Match(Token.WHEN)) // optional
            {
                request.When = predicate();
            }

            if (Match(Token.WITH)) // optional
            {
                parse_column_expressions(request.Headers);
            }

            if (Match(Token.SELECT)) // optional
            {
                parse_column_expressions(request.Options);
            }

            if (Match(Token.INTO)) //NOTE: optional for databases
            {
                //throw new FormatException($"REQUEST: INTO keyword expected");

                if (!Match(Token.Variable))
                {
                    throw new FormatException($"REQUEST: variable identifier expected");
                }

                request.Response = new VariableReference()
                {
                    Identifier = Previous().Value
                };
            }

            Skip(Token.Comment);

            return request;
        }
        #endregion

        #region "PROCESS STATEMENT"
        private SyntaxNode process_statement()
        {
            ProcessStatement statement = new()
            {
                Variables = array_of_variables()
            };

            if (!Match(Token.WITH))
            {
                throw new FormatException($"[PROCESS] WITH keyword expected");
            }

            if (!Match(Token.Identifier))
            {
                throw new FormatException("[PROCESS] processor identifier expected");
            }

            statement.Processor = Previous().Value;

            Skip(Token.Comment);

            if (Match(Token.INTO)) // optional
            {
                if (!Match(Token.Variable) || variable() is not VariableReference into)
                {
                    throw new FormatException($"[PROCESS] INTO variable identifier expected");
                }

                statement.Return = into;

                Skip(Token.Comment);
            }

            if (Match(Token.SELECT)) // optional
            {
                parse_column_expressions(statement.Options);
            }

            return statement;
        }
        #endregion

        #region "MODIFY STATEMENT"
        private SyntaxNode modify_statement()
        {
            ModifyStatement statement = new();

            if (!Match(Token.Variable) || variable() is not VariableReference target)
            {
                throw new FormatException("[MODIFY] target object variable expected");
            }

            statement.Target = target;

            Skip(Token.Comment);

            if (Match(Token.FROM)) // optional
            {
                if (!Match(Token.Variable) || variable() is not VariableReference source)
                {
                    throw new FormatException("[MODIFY] source object variable expected");
                }

                statement.Source = source;
            }

            Skip(Token.Comment);

            if (Match(Token.DELETE)) // optional
            {
                parse_column_references(statement.Delete);

                if (statement.Delete.Count == 0)
                {
                    throw new FormatException("[MODIFY][DELETE] object property identifier expected");
                }
            }

            Skip(Token.Comment);

            if (Match(Token.SELECT)) // optional
            {
                parse_column_expressions(statement.Select);

                if (statement.Select.Count == 0)
                {
                    throw new FormatException("[MODIFY][SELECT] object property expression expected");
                }
            }

            Skip(Token.Comment);

            return statement;
        }
        #endregion
    }
}