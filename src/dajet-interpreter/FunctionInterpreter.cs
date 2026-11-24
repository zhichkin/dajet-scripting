using DaJet.Json;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace DaJet.Scripting
{
    internal static class FunctionInterpreter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        static FunctionInterpreter()
        {
            JsonOptions.Converters.Add(new DataTypeJsonConverter());
            JsonOptions.Converters.Add(new DataObjectJsonConverter());
            JsonOptions.Converters.Add(new JsonStringEnumConverter());
        }
        internal static object Evaluate(in ExpressionInterpreter context, in FunctionExpression node)
        {
            if (node.Name == nameof(JSON)) { return JSON(in context, in node); }
            else if (node.Name == nameof(TYPEOF)) { return TYPEOF(in context, in node); }
            else if (node.Name == nameof(UUIDOF)) { return UUIDOF(in context, in node); }
            else if (node.Name == nameof(UUID1C)) { return UUID1C(in context, in node); }
            else if (node.Name == nameof(UUIDDB)) { return UUIDDB(in context, in node); }
            else if (node.Name == nameof(ENTITY)) { return ENTITY(in context, in node); }
            else if (node.Name == nameof(NOW)) { return NOW(in context, in node); }
            else if (node.Name == nameof(UTC)) { return UTC(in context, in node); }
            else if (node.Name == nameof(ERROR_MESSAGE)) { return ERROR_MESSAGE(in context, in node); }
            else if (node.Name == nameof(DATESTART)) { return DATESTART(in context, in node); }
            else if (node.Name == nameof(DATEEND)) { return DATEEND(in context, in node); }
            else if (node.Name == nameof(DATEADD)) { return DATEADD(in context, in node); }
            else if (node.Name == nameof(DATEDIFF)) { return DATEDIFF(in context, in node); }
            else
            {
                return null;
            }
        }
        private static string JSON(in ExpressionInterpreter context, in FunctionExpression node)
        {
            SyntaxNode parameter = node.Parameters[0];

            object value = context.Evaluate(in parameter);

            if (value is not null)
            {
                return JsonSerializer.Serialize(value, value.GetType(), JsonOptions);
            }

            return string.Empty;
        }
        private static int TYPEOF(in ExpressionInterpreter context, in FunctionExpression node)
        {
            SyntaxNode parameter = node.Parameters[0];

            object value = context.Evaluate(in parameter);

            if (value is Entity entity)
            {
                return entity.TypeCode;
            }
            else if (value is Union union && union.Tag == UnionTag.Entity)
            {
                return union.GetEntity().TypeCode;
            }

            return 0;
        }
        private static Guid UUIDOF(in ExpressionInterpreter context, in FunctionExpression node)
        {
            SyntaxNode parameter = node.Parameters[0];

            object value = context.Evaluate(in parameter);

            if (value is Entity entity)
            {
                return entity.Identity;
            }
            else if (value is Union union && union.Tag == UnionTag.Entity)
            {
                return union.GetEntity().Identity;
            }

            return Guid.Empty;
        }
        private static DateTime NOW(in ExpressionInterpreter context, in FunctionExpression node)
        {
            return DateTime.Now;
        }
        private static DateTime UTC(in ExpressionInterpreter context, in FunctionExpression node)
        {
            return DateTime.UtcNow;
        }
        private static string ERROR_MESSAGE(in ExpressionInterpreter context, in FunctionExpression node)
        {
            return (string)context.Evaluate(new VariableReference() { Identifier = "@@ERROR_MESSAGE" });
        }
        private static DateTime DATESTART(in ExpressionInterpreter context, in FunctionExpression node)
        {
            SyntaxNode expression = node.Parameters[0];

            if (expression is not ScalarExpression scalar)
            {
                throw new InvalidOperationException($"[DATESTART] the first parameter must be string");
            }

            string datepart = scalar.Literal;

            expression = node.Parameters[1];

            DateTime parameter = (DateTime)context.Evaluate(in expression);

            if (datepart == "YEAR")
            {
                return new DateTime(parameter.Year, 1, 1);
            }
            else if (datepart == "QUARTER")
            {
                if (parameter.Month <= 3)
                {
                    return new DateTime(parameter.Year, 1, 1);
                }
                else if (parameter.Month <= 6)
                {
                    return new DateTime(parameter.Year, 4, 1);
                }
                else if (parameter.Month <= 9)
                {
                    return new DateTime(parameter.Year, 7, 1);
                }
                else // parameter.Month <= 12
                {
                    return new DateTime(parameter.Year, 10, 1);
                }
            }
            else if (datepart == "MONTH")
            {
                return new DateTime(parameter.Year, parameter.Month, 1);
            }
            else if (datepart == "DAY")
            {
                return new DateTime(parameter.Year, parameter.Month, parameter.Day);
            }
            else if (datepart == "HOUR")
            {
                return new DateTime(parameter.Year, parameter.Month, parameter.Day, parameter.Hour, 0, 0);
            }
            else if (datepart == "MINUTE")
            {
                return new DateTime(parameter.Year, parameter.Month, parameter.Day, parameter.Hour, parameter.Minute, 0);
            }
            else if (datepart == "SECOND")
            {
                return new DateTime(parameter.Year, parameter.Month, parameter.Day, parameter.Hour, parameter.Minute, parameter.Second);
            }

            throw new InvalidOperationException($"[DATESTART] the first parameter is invalid value");
        }
        private static DateTime DATEEND(in ExpressionInterpreter context, in FunctionExpression node)
        {
            SyntaxNode expression = node.Parameters[0];

            if (expression is not ScalarExpression scalar)
            {
                throw new InvalidOperationException($"[DATEEND] the first parameter must be string");
            }
            
            string datepart = scalar.Literal;

            expression = node.Parameters[1];

            DateTime parameter = (DateTime)context.Evaluate(in expression);

            if (datepart == "YEAR")
            {
                return new DateTime(parameter.Year, 12, 31, 23, 59, 59);
            }
            else if (datepart == "QUARTER")
            {
                if (parameter.Month <= 3)
                {
                    return new DateTime(parameter.Year, 3, 31, 23, 59, 59);
                }
                else if (parameter.Month <= 6)
                {
                    return new DateTime(parameter.Year, 6, 30, 23, 59, 59);
                }
                else if (parameter.Month <= 9)
                {
                    return new DateTime(parameter.Year, 9, 30, 23, 59, 59);
                }
                else // parameter.Month <= 12
                {
                    return new DateTime(parameter.Year, 12, 31, 23, 59, 59);
                }
            }
            else if (datepart == "MONTH")
            {
                return new DateTime(parameter.Year, parameter.Month, DateTime.DaysInMonth(parameter.Year, parameter.Month), 23, 59, 59);
            }
            else if (datepart == "DAY")
            {
                return new DateTime(parameter.Year, parameter.Month, parameter.Day, 23, 59, 59);
            }
            else if (datepart == "HOUR")
            {
                return new DateTime(parameter.Year, parameter.Month, parameter.Day, parameter.Hour, 59, 59);
            }
            else if (datepart == "MINUTE")
            {
                return new DateTime(parameter.Year, parameter.Month, parameter.Day, parameter.Hour, parameter.Minute, 59);
            }
            else if (datepart == "SECOND")
            {
                return new DateTime(parameter.Year, parameter.Month, parameter.Day, parameter.Hour, parameter.Minute, parameter.Second);
            }

            throw new InvalidOperationException($"[DATEEND] the first parameter is invalid value");
        }
        private static DateTime DATEADD(in ExpressionInterpreter context, in FunctionExpression node)
        {
            SyntaxNode expression = node.Parameters[0];

            if (expression is not ScalarExpression scalar)
            {
                throw new InvalidOperationException($"[DATEADD] the first parameter must be string");
            }

            string datepart = scalar.Literal;

            expression = node.Parameters[1];

            int addition = (int)context.Evaluate(in expression);
            
            expression = node.Parameters[2];

            DateTime parameter = (DateTime)context.Evaluate(in expression);

            if (datepart == "YEAR")
            {
                return parameter.AddYears(addition);
            }
            else if (datepart == "QUARTER")
            {
                return parameter.AddMonths(addition * 3);
            }
            else if (datepart == "MONTH")
            {
                return parameter.AddMonths(addition);
            }
            else if (datepart == "DAY")
            {
                return parameter.AddDays(addition);
            }
            else if (datepart == "HOUR")
            {
                return parameter.AddHours(addition);
            }
            else if (datepart == "MINUTE")
            {
                return parameter.AddMinutes(addition);
            }
            else if (datepart == "SECOND")
            {
                return parameter.AddSeconds(addition);
            }

            throw new InvalidOperationException($"[DATEADD] the first parameter is invalid value");
        }
        private static int DATEDIFF(in ExpressionInterpreter context, in FunctionExpression node)
        {
            SyntaxNode expression = node.Parameters[0];

            if (expression is not ScalarExpression scalar)
            {
                throw new InvalidOperationException($"[DATEDIFF] the first parameter must be string");
            }

            string datepart = scalar.Literal;

            expression = node.Parameters[1];

            DateTime start = (DateTime)context.Evaluate(in expression);

            expression = node.Parameters[2];

            DateTime end = (DateTime)context.Evaluate(in expression);

            TimeSpan diff = end - start;

            if (datepart == "YEAR")
            {
                return start.Year - end.Year;
            }
            else if (datepart == "MONTH")
            {
                return start.Month - end.Month;
            }
            else if (datepart == "DAY")
            {
                return (int)diff.TotalDays;
            }
            else if (datepart == "HOUR")
            {
                return (int)diff.TotalHours;
            }
            else if (datepart == "MINUTE")
            {
                return (int)diff.TotalMinutes;
            }
            else if (datepart == "SECOND")
            {
                return (int)diff.TotalSeconds;
            }

            throw new InvalidOperationException($"[DATEDIFF] the first parameter is invalid value");
        }

        private static Guid UUID1C(in ExpressionInterpreter context, in FunctionExpression node)
        {
            List<SyntaxNode> parameters = node.Parameters;

            if (parameters is null || parameters.Count == 0)
            {
                throw new InvalidOperationException("[UUID1C] Parameter is missing.");
            }

            if (parameters.Count > 1)
            {
                throw new InvalidOperationException("[UUID1C] Too many parameters.");
            }

            SyntaxNode parameter = node.Parameters[0];

            object value = context.Evaluate(in parameter);

            if (value is Guid uuid)
            {
                return UuidConverter.CONVERT_UUID_DB_TO_1C(uuid);
            }
            else if (value is Entity entity)
            {
                return UuidConverter.CONVERT_UUID_DB_TO_1C(entity);
            }
            else if (value is byte[] binary)
            {
                return UuidConverter.CONVERT_UUID_DB_TO_1C(binary);
            }
            else if (value is string literal)
            {
                if (Guid.TryParse(literal, out _))
                {
                    return UuidConverter.CONVERT_UUID_DB_TO_1C(literal);
                }
                else
                {
                    throw new InvalidOperationException("[UUID1C] Parameter of type 'string' has invalid 'uuid' format.");
                }
            }
            else
            {
                throw new InvalidOperationException("[UUID1C] Parameter must be of type 'uuid', 'entity', 'binary(16)' or string literal.");
            }
        }
        private static Guid UUIDDB(in ExpressionInterpreter context, in FunctionExpression node)
        {
            List<SyntaxNode> parameters = node.Parameters;

            if (parameters is null || parameters.Count == 0)
            {
                throw new InvalidOperationException("[UUIDDB] Parameter is missing.");
            }

            if (parameters.Count > 1)
            {
                throw new InvalidOperationException("[UUIDDB] Too many parameters.");
            }

            SyntaxNode parameter = node.Parameters[0];
            
            object value = context.Evaluate(in parameter);

            if (value is Guid uuid)
            {
                return UuidConverter.CONVERT_UUID_1C_TO_DB(uuid);
            }
            else if (value is Entity entity)
            {
                return UuidConverter.CONVERT_UUID_1C_TO_DB(entity);
            }
            else if (value is byte[] binary)
            {
                return UuidConverter.CONVERT_UUID_1C_TO_DB(binary);
            }
            else if (value is string literal)
            {
                if (Guid.TryParse(literal, out _))
                {
                    return UuidConverter.CONVERT_UUID_1C_TO_DB(literal);
                }
                else
                {
                    throw new InvalidOperationException("[UUIDDB] Parameter of type 'string' has invalid 'uuid' format.");
                }
            }
            else
            {
                throw new InvalidOperationException("[UUIDDB] Parameter value must be of type 'uuid', 'entity', 'binary(16)' or string literal.");
            }
        }
        private static Entity ENTITY(in ExpressionInterpreter context, in FunctionExpression node)
        {
            List<SyntaxNode> parameters = node.Parameters;

            if (parameters is null || parameters.Count != 2)
            {
                throw new InvalidOperationException("[ENTITY] Function must have two parameters.");
            }

            SyntaxNode parameter = parameters[0];

            object value = context.Evaluate(in parameter);

            if (value is not int code)
            {
                throw new InvalidOperationException("[ENTITY] The first parameter must be of type 'integer'.");
            }
            
            parameter = parameters[1];

            value = context.Evaluate(in parameter);

            if (value is not Guid uuid)
            {
                throw new InvalidOperationException("[ENTITY] The second parameter must be of type 'uuid'.");
            }

            return new Entity(code, uuid);
        }
    }
}