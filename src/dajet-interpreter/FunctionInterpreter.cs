using DaJet.Json;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Reflection.Metadata;
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
    }
}