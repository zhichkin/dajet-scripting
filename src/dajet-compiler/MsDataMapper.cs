using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using System.Buffers.Binary;
using System.Data;
using System.Reflection;
using System.Reflection.Emit;

namespace DaJet.Compiler
{
    internal static class MsDataMapper
    {
        #region "STATIC METADATA FIELDS"

        // Array of bytes (buffer) processing
        private static readonly MethodInfo ToByteArray;
        private static readonly MethodInfo AsSpanOfBytes;
        private static readonly MethodInfo SpanOfBytesToReadOnly;
        private static readonly MethodInfo ReadInt32BigEndian;

        // SqlCommand methods
        private static readonly MethodInfo GetParameters;
        private static readonly MethodInfo ParametersClear;
        private static readonly MethodInfo AddWithValue;

        // SqlDataReader methods
        private static readonly MethodInfo IsDBNull;
        private static readonly MethodInfo GetValue;
        private static readonly MethodInfo GetBytes;
        private static readonly MethodInfo GetByte;
        private static readonly MethodInfo GetInt16;
        private static readonly MethodInfo GetInt32;
        private static readonly MethodInfo GetInt64;
        private static readonly MethodInfo GetDecimal;
        private static readonly MethodInfo GetDateTime;
        private static readonly MethodInfo DateTimeAddYears;
        private static readonly MethodInfo GetString;

        // SqlParameter properties
        private static readonly MethodInfo SetSqlDbType;

        // Default values
        private static readonly FieldInfo Zero;
        private static readonly MethodInfo ArrayEmpty;
        private static readonly FieldInfo GuidEmpty;
        private static readonly FieldInfo StringEmpty;
        private static readonly FieldInfo DateTimeMinValue;
        private static readonly FieldInfo TRUE; // byte[1] { 0x01 }
        private static readonly FieldInfo FALSE; // byte[1] { 0x00 }

        // Constructors
        private static readonly ConstructorInfo GuidCtor;
        private static readonly ConstructorInfo Int32ToDecimal;
        private static readonly ConstructorInfo Int64ToDecimal;
        private static readonly ConstructorInfo UInt32ToDecimal;
        private static readonly ConstructorInfo UInt64ToDecimal;

        // Entity type
        private static readonly FieldInfo EntityUndefined;
        private static readonly ConstructorInfo EntityCtor;
        private static readonly PropertyInfo EntityIdentity;

        // Union type implicit conversion
        private static readonly FieldInfo UnionUndefined;
        private static readonly MethodInfo BooleanToUnion;
        private static readonly MethodInfo DecimalToUnion;
        private static readonly MethodInfo DateTimeToUnion;
        private static readonly MethodInfo StringToUnion;
        private static readonly MethodInfo EntityToUnion;
        
        #endregion
        
        static MsDataMapper()
        {
            TRUE = typeof(SelectProcessor).GetField(nameof(TRUE),
                BindingFlags.Static | BindingFlags.NonPublic);
            
            FALSE = typeof(SelectProcessor).GetField(nameof(FALSE),
                BindingFlags.Static | BindingFlags.NonPublic);

            GetParameters = typeof(SqlCommand)
                .GetProperty(nameof(SqlCommand.Parameters),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .GetGetMethod();

            ParametersClear = typeof(SqlParameterCollection)
                .GetMethod(nameof(SqlParameterCollection.Clear),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                Type.EmptyTypes);

            SetSqlDbType = typeof(SqlParameter).GetProperty(nameof(SqlParameter.SqlDbType),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .GetSetMethod();

            AddWithValue = typeof(SqlParameterCollection)
                .GetMethod(nameof(SqlParameterCollection.AddWithValue),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                [typeof(string), typeof(object)]);

            AsSpanOfBytes = typeof(MemoryExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == nameof(MemoryExtensions.AsSpan)
                && m.IsGenericMethod
                && m.GetParameters().Length == 3
                && m.GetParameters()[0].ParameterType.BaseType == typeof(Array)
                && m.GetParameters()[1].ParameterType == typeof(int)
                && m.GetParameters()[2].ParameterType == typeof(int))
                .FirstOrDefault()
                .MakeGenericMethod([typeof(byte)]);

            SpanOfBytesToReadOnly = typeof(Span<byte>).GetMethod("op_Implicit",
                BindingFlags.Static | BindingFlags.Public, [typeof(Span<byte>)]);

            ReadInt32BigEndian = typeof(BinaryPrimitives)
                .GetMethod(nameof(BinaryPrimitives.ReadInt32BigEndian),
                BindingFlags.Public | BindingFlags.Static, [typeof(ReadOnlySpan<byte>)]);

            IsDBNull = typeof(SqlDataReader).GetMethod(nameof(SqlDataReader.IsDBNull),
                BindingFlags.Instance | BindingFlags.Public, [typeof(int)]);

            GetValue = typeof(SqlDataReader).GetMethod(nameof(SqlDataReader.GetValue),
                BindingFlags.Instance | BindingFlags.Public, [typeof(int)]);

            GetBytes = typeof(SqlDataReader).GetMethod(nameof(SqlDataReader.GetBytes),
                BindingFlags.Instance | BindingFlags.Public,
                [typeof(int), typeof(long), typeof(byte[]), typeof(int), typeof(int)]);

            GetByte = typeof(SqlDataReader).GetMethod(nameof(SqlDataReader.GetByte),
                BindingFlags.Instance | BindingFlags.Public, [typeof(int)]);

            GetInt16 = typeof(SqlDataReader).GetMethod(nameof(SqlDataReader.GetInt16),
                BindingFlags.Instance | BindingFlags.Public, [typeof(int)]);

            GetInt32 = typeof(SqlDataReader).GetMethod(nameof(SqlDataReader.GetInt32),
                BindingFlags.Instance | BindingFlags.Public, [typeof(int)]);

            GetInt64 = typeof(SqlDataReader).GetMethod(nameof(SqlDataReader.GetInt64),
                BindingFlags.Instance | BindingFlags.Public, [typeof(int)]);

            GetDecimal = typeof(SqlDataReader).GetMethod(nameof(SqlDataReader.GetDecimal),
                BindingFlags.Instance | BindingFlags.Public, [typeof(int)]);

            GetDateTime = typeof(SqlDataReader).GetMethod(nameof(SqlDataReader.GetDateTime),
                BindingFlags.Instance | BindingFlags.Public, [typeof(int)]);

            DateTimeAddYears = typeof(DateTime).GetMethod(nameof(DateTime.AddYears),
                BindingFlags.Instance | BindingFlags.Public, [typeof(int)]);

            GetString = typeof(SqlDataReader).GetMethod(nameof(SqlDataReader.GetString),
                BindingFlags.Instance | BindingFlags.Public, [typeof(int)]);

            Zero = typeof(decimal).GetField(nameof(decimal.Zero),
                BindingFlags.Static | BindingFlags.Public);

            ArrayEmpty = typeof(Array).GetMethod(nameof(Array.Empty),
                BindingFlags.Static | BindingFlags.Public, Type.EmptyTypes)
                .MakeGenericMethod([typeof(byte)]);

            GuidEmpty = typeof(Guid).GetField(nameof(Guid.Empty),
                BindingFlags.Static | BindingFlags.Public);

            ToByteArray = typeof(Guid).GetMethod(nameof(Guid.ToByteArray),
                BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes);

            StringEmpty = typeof(string).GetField(nameof(string.Empty),
                BindingFlags.Static | BindingFlags.Public);

            DateTimeMinValue = typeof(DateTime).GetField(nameof(DateTime.MinValue),
                BindingFlags.Static | BindingFlags.Public);

            UnionUndefined = typeof(Union).GetField(nameof(Union.Undefined),
                BindingFlags.Static | BindingFlags.Public);

            EntityUndefined = typeof(Entity).GetField(nameof(Entity.Undefined),
                BindingFlags.Static | BindingFlags.Public);

            GuidCtor = typeof(Guid).GetConstructor(BindingFlags.Instance | BindingFlags.Public,
                [typeof(byte[])]);

            EntityCtor = typeof(Entity).GetConstructor(BindingFlags.Instance | BindingFlags.Public,
                [typeof(int), typeof(Guid)]);

            EntityIdentity = typeof(Entity).GetProperty(nameof(Entity.Identity),
                BindingFlags.Instance | BindingFlags.Public);

            Int32ToDecimal = typeof(decimal).GetConstructor(BindingFlags.Instance | BindingFlags.Public,
                [typeof(int)]);

            UInt32ToDecimal = typeof(decimal).GetConstructor(BindingFlags.Instance | BindingFlags.Public,
                [typeof(uint)]);

            Int64ToDecimal = typeof(decimal).GetConstructor(BindingFlags.Instance | BindingFlags.Public,
                [typeof(long)]);

            UInt64ToDecimal = typeof(decimal).GetConstructor(BindingFlags.Instance | BindingFlags.Public,
                [typeof(ulong)]);

            BooleanToUnion = typeof(Union).GetMethod("op_Implicit",
                BindingFlags.Static | BindingFlags.Public, [typeof(bool)]);

            DecimalToUnion = typeof(Union).GetMethod("op_Implicit",
                BindingFlags.Static | BindingFlags.Public, [typeof(decimal)]);

            DateTimeToUnion = typeof(Union).GetMethod("op_Implicit",
                BindingFlags.Static | BindingFlags.Public, [typeof(DateTime)]);

            StringToUnion = typeof(Union).GetMethod("op_Implicit",
                BindingFlags.Static | BindingFlags.Public, [typeof(string)]);

            EntityToUnion = typeof(Union).GetMethod("op_Implicit",
                BindingFlags.Static | BindingFlags.Public, [typeof(Entity)]);
        }

        internal static int YearOffset { get; set; }

        internal static void MapInput(in List<SyntaxNode> input, in FieldInfo data, in Dictionary<string, PropertyInfo> properties, in ILGenerator IL)
        {
            // public abstract class SelectProcessor
            // protected virtual void Configure(SqlCommand command)
            // ARG 0 : this
            // ARG 1 : command
            // LOC 0 : Guid local value to call Guid.ToByteArray()
            // LOC 1 : Entity local value to call Entity.Identity
            // LOC 2 : DateTime to manipulate year offset
            // this._data : Ссылка на данные ScriptProcessor
            // input : Список входящих данных - переменных скрипта

            _ = IL.DeclareLocal(typeof(Guid));     // LOC 0
            _ = IL.DeclareLocal(typeof(Entity));   // LOC 1
            _ = IL.DeclareLocal(typeof(DateTime)); // LOC 2

            // command.Parameters.Clear();
            IL.Emit(OpCodes.Ldarg_1);
            IL.Emit(OpCodes.Callvirt, GetParameters);
            IL.Emit(OpCodes.Callvirt, ParametersClear);

            ExpressionCompiler expression = new(in data, in properties);

            for (int i = 0; i < input.Count; i++)
            {
                SyntaxNode node = input[i];

                // command.Parameters.AddWithValue("p0", _data.Свойство);

                IL.Emit(OpCodes.Ldarg_1);
                IL.Emit(OpCodes.Callvirt, GetParameters);
                IL.Emit(OpCodes.Ldstr, $"p{i}");

                Type value = expression.Evaluate(in node, in IL);

                //TODO: null -> DBNull.Value

                if (value == typeof(bool)) // convert to byte[1]
                {
                    Label _ELSE = IL.DefineLabel();
                    Label _ENDIF = IL.DefineLabel();
                    IL.Emit(OpCodes.Brfalse_S, _ELSE);
                    IL.Emit(OpCodes.Ldsfld, TRUE); // 0x01
                    IL.Emit(OpCodes.Br_S, _ENDIF);
                    IL.MarkLabel(_ELSE);
                    IL.Emit(OpCodes.Ldsfld, FALSE); // 0x00
                    IL.MarkLabel(_ENDIF);

                    value = typeof(byte[]);
                }
                else if (value == typeof(int))
                {
                    IL.Emit(OpCodes.Newobj, Int32ToDecimal); value = typeof(decimal);
                }
                else if (value == typeof(long))
                {
                    IL.Emit(OpCodes.Newobj, Int64ToDecimal); value = typeof(decimal);
                }
                else if (value == typeof(uint))
                {
                    IL.Emit(OpCodes.Newobj, UInt32ToDecimal); value = typeof(decimal);
                }
                else if (value == typeof(ulong))
                {
                    IL.Emit(OpCodes.Newobj, UInt64ToDecimal); value = typeof(decimal);
                }
                else if (value == typeof(DateTime))
                {
                    if (YearOffset > 0)
                    {
                        IL.Emit(OpCodes.Stloc_2);
                        IL.Emit(OpCodes.Ldloca_S, 2);
                        IL.Emit(OpCodes.Ldc_I4, YearOffset);
                        IL.Emit(OpCodes.Call, DateTimeAddYears);
                    }
                }
                else if (value == typeof(Guid)) // convert to byte[16]
                {
                    IL.Emit(OpCodes.Stloc_0);
                    IL.Emit(OpCodes.Ldloca_S, 0);
                    IL.Emit(OpCodes.Call, ToByteArray);

                    value = typeof(byte[]);
                }
                else if (value == typeof(Entity))
                {
                    // _data.Свойство.Identity.ToByteArray()

                    IL.Emit(OpCodes.Stloc_1);
                    IL.Emit(OpCodes.Ldloca_S, 1);
                    IL.Emit(OpCodes.Call, EntityIdentity.GetGetMethod());

                    IL.Emit(OpCodes.Stloc_0);
                    IL.Emit(OpCodes.Ldloca_S, 0);
                    IL.Emit(OpCodes.Call, ToByteArray);

                    value = typeof(byte[]);
                }

                if (value.IsValueType)
                {
                    IL.Emit(OpCodes.Box, value);
                }

                IL.Emit(OpCodes.Callvirt, AddWithValue); // Возвращает на стек ссылку на SqlParameter

                if (value == typeof(DateTime))
                {
                    // command.Parameters.AddWithValue("p2", _data.ДатаВремя).SqlDbType = SqlDbType.DateTime2;

                    IL.Emit(OpCodes.Ldc_I4, (int)SqlDbType.DateTime2);
                    IL.Emit(OpCodes.Callvirt, SetSqlDbType);
                }
                else
                {
                    IL.Emit(OpCodes.Pop); // Убираем со стека SqlParameter, который возвращает AddWithValue
                }
            }
        }

        private static List<ColumnDefinition> Columns; // column ordinals in SqlDataReader
        private static void FlattenColumns(in EntityDefinition metadata)
        {
            Columns = new List<ColumnDefinition>();

            PropertyDefinition property;

            for (int p = 0; p < metadata.Properties.Count; p++)
            {
                property = metadata.Properties[p];

                for (int c = 0; c < property.Columns.Count; c++)
                {
                    Columns.Add(property.Columns[c]);
                }
            }
        }
        
        internal static void MapOutput(in Type output, in EntityDefinition metadata, in ILGenerator IL)
        {
            // public abstract class SelectProcessor
            // protected virtual void Process(SqlDataReader reader)
            // ARG 0 : this
            // ARG 1 : reader
            // LOC 0 : output variable reference
            // LOC 1 : byte[16] buffer reference
            // LOC 2 : DateTime to manipulate year offset

            FlattenColumns(in metadata);

            PropertyDefinition property;

            List<PropertyDefinition> properties = metadata.Properties;

            for (int i = 0; i < properties.Count; i++)
            {
                property = properties[i];

                DataType target = property.Type;

                if (target.IsUnion) { MapUnion(in output, in property, in IL); }
                else if (target.IsBoolean) { MapBoolean(in output, in property, in IL); }
                else if (target.IsDecimal) { MapDecimal(in output, in property, in IL); }
                else if (target.IsInteger) { MapInteger(in output, in property, in IL); }
                else if (target.IsDateTime) { MapDateTime(in output, in property, in IL); }
                else if (target.IsString) { MapString(in output, in property, in IL); }
                else if (target.IsBinary) { MapBinary(in output, in property, in IL); }
                else if (target.IsUuid) { MapUuid(in output, in property, in IL); }
                else if (target.IsEntity) { MapEntity(in output, in property, in IL); }
            }

            // cleanup

            Columns = null;
        }
        private static void MapBoolean(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            PropertyInfo target = output.GetProperty(property.Name,
                BindingFlags.Instance | BindingFlags.Public);

            MethodInfo setAccessor = target.GetSetMethod();

            ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Value); // single value column

            column ??= property.GetColumnByPurpose(ColumnPurpose.Boolean); // union type column

            if (column is not null)
            {
                int ordinal = Columns.IndexOf(column);

                Label _ELSE = IL.DefineLabel();
                Label _ENDIF = IL.DefineLabel();

                IL.Emit(OpCodes.Ldloc_0); // output variable reference

                // if (reader.IsDBNull(ordinal))
                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal);
                IL.Emit(OpCodes.Callvirt, IsDBNull);
                IL.Emit(OpCodes.Brfalse_S, _ELSE);

                // TRUE
                IL.Emit(OpCodes.Ldc_I4_0); // assign default value

                IL.Emit(OpCodes.Br_S, _ENDIF);

                IL.MarkLabel(_ELSE);

                // reader.GetBytes(ordinal, 0L, buffer, 0, 1);
                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal); // ordinal
                IL.Emit(OpCodes.Ldc_I4_0); // 0
                IL.Emit(OpCodes.Conv_I8); // 0 -> 0L
                IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference
                IL.Emit(OpCodes.Ldc_I4_0); // buffer start
                IL.Emit(OpCodes.Ldc_I4_1); // bytes to read
                IL.Emit(OpCodes.Callvirt, GetBytes);
                IL.Emit(OpCodes.Pop); // remove return value from stack

                // _output.Свойство = buffer[0] != 0;
                IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference
                IL.Emit(OpCodes.Ldc_I4_0); // index of the value
                IL.Emit(OpCodes.Ldelem_U1); // push buffer[0] value onto the stack

                IL.MarkLabel(_ENDIF);

                if (target.PropertyType == typeof(Union))
                {
                    IL.Emit(OpCodes.Call, BooleanToUnion);
                }

                IL.Emit(OpCodes.Call, setAccessor);
            }
        }
        private static void MapDecimal(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            // _output.Свойство = reader.IsDBNull(0) ? 0M : reader.GetDecimal(0);

            PropertyInfo target = output.GetProperty(property.Name,
                BindingFlags.Instance | BindingFlags.Public);

            MethodInfo setAccessor = target.GetSetMethod();

            ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Value);

            column ??= property.GetColumnByPurpose(ColumnPurpose.Numeric); // union type column

            if (column is not null)
            {
                int ordinal = Columns.IndexOf(column);

                Label _ELSE = IL.DefineLabel();
                Label _ENDIF = IL.DefineLabel();

                IL.Emit(OpCodes.Ldloc_0); // output variable reference

                // if (reader.IsDBNull(ordinal))
                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal);
                IL.Emit(OpCodes.Callvirt, IsDBNull);
                IL.Emit(OpCodes.Brfalse_S, _ELSE);

                // TRUE
                IL.Emit(OpCodes.Ldsfld, Zero); // assign default value

                IL.Emit(OpCodes.Br_S, _ENDIF);

                IL.MarkLabel(_ELSE);

                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal);
                IL.Emit(OpCodes.Callvirt, GetDecimal); // reader.GetDecimal(ordinal)

                IL.MarkLabel(_ENDIF);

                if (target.PropertyType == typeof(Union))
                {
                    IL.Emit(OpCodes.Call, DecimalToUnion);
                }

                IL.Emit(OpCodes.Call, setAccessor); // _output.Свойство = value;
            }
        }
        private static void MapInteger(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            // _output.Свойство = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);

            MethodInfo setAccessor = output.GetProperty(property.Name,
                BindingFlags.Instance | BindingFlags.Public).GetSetMethod();

            ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Value);

            if (column is not null)
            {
                int ordinal = Columns.IndexOf(column);

                Label _ELSE = IL.DefineLabel();
                Label _ENDIF = IL.DefineLabel();

                IL.Emit(OpCodes.Ldloc_0); // output variable reference

                // if (reader.IsDBNull(ordinal))
                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal);
                IL.Emit(OpCodes.Callvirt, IsDBNull);
                IL.Emit(OpCodes.Brfalse_S, _ELSE);
                
                // TRUE
                IL.Emit(OpCodes.Ldc_I4_0); // assign default value
                
                IL.Emit(OpCodes.Br_S, _ENDIF);
                
                IL.MarkLabel(_ELSE);

                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal);

                if (property.Type.Size == 1)
                {
                    IL.Emit(OpCodes.Callvirt, GetByte); // reader.GetByte(ordinal)
                }
                else if (property.Type.Size == 2)
                {
                    IL.Emit(OpCodes.Callvirt, GetInt16); // reader.GetInt64(ordinal)
                }
                else if (property.Type.Size == 4)
                {
                    IL.Emit(OpCodes.Callvirt, GetInt32); // reader.GetInt32(ordinal)
                }
                else if (property.Type.Size == 8)
                {
                    IL.Emit(OpCodes.Callvirt, GetInt64); // reader.GetInt64(ordinal)
                }
                
                IL.MarkLabel(_ENDIF);

                IL.Emit(OpCodes.Call, setAccessor); // _output.Свойство = value;
            }
        }
        private static void MapDateTime(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            // _output.Свойство = reader.IsDBNull(0) ? DateTime.MinValue : reader.GetDateTime(0).AddYears(-YearOffset);

            PropertyInfo target = output.GetProperty(property.Name,
                BindingFlags.Instance | BindingFlags.Public);

            MethodInfo setAccessor = target.GetSetMethod();

            ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Value);

            column ??= property.GetColumnByPurpose(ColumnPurpose.DateTime); // union type column

            if (column is not null)
            {
                int ordinal = Columns.IndexOf(column);

                Label _ELSE = IL.DefineLabel();
                Label _ENDIF = IL.DefineLabel();

                IL.Emit(OpCodes.Ldloc_0); // output variable reference

                // if (reader.IsDBNull(ordinal))
                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal);
                IL.Emit(OpCodes.Callvirt, IsDBNull);
                IL.Emit(OpCodes.Brfalse_S, _ELSE);

                // TRUE
                IL.Emit(OpCodes.Ldsfld, DateTimeMinValue); // assign default value

                IL.Emit(OpCodes.Br_S, _ENDIF);

                IL.MarkLabel(_ELSE);

                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal);
                IL.Emit(OpCodes.Callvirt, GetDateTime);

                if (YearOffset > 0)
                {
                    IL.Emit(OpCodes.Stloc_2);
                    IL.Emit(OpCodes.Ldloca, 2);
                    IL.Emit(OpCodes.Ldc_I4, -YearOffset);
                    IL.Emit(OpCodes.Call, DateTimeAddYears);
                }

                IL.MarkLabel(_ENDIF);

                if (target.PropertyType == typeof(Union))
                {
                    IL.Emit(OpCodes.Call, DateTimeToUnion);
                }

                IL.Emit(OpCodes.Call, setAccessor);
            }
        }
        private static void MapString(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            // _output.Свойство = reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

            PropertyInfo target = output.GetProperty(property.Name,
                BindingFlags.Instance | BindingFlags.Public);

            MethodInfo setAccessor = target.GetSetMethod();

            ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Value); // single value column

            column ??= property.GetColumnByPurpose(ColumnPurpose.String); // union type column

            if (column is not null)
            {
                int ordinal = Columns.IndexOf(column);

                Label _ELSE = IL.DefineLabel();
                Label _ENDIF = IL.DefineLabel();

                IL.Emit(OpCodes.Ldloc_0); // output variable reference

                // if (reader.IsDBNull(ordinal))
                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal);
                IL.Emit(OpCodes.Callvirt, IsDBNull);
                IL.Emit(OpCodes.Brfalse_S, _ELSE);

                // TRUE
                IL.Emit(OpCodes.Ldsfld, StringEmpty); // assign default value

                IL.Emit(OpCodes.Br_S, _ENDIF);

                IL.MarkLabel(_ELSE);

                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal);
                IL.Emit(OpCodes.Callvirt, GetString);

                IL.MarkLabel(_ENDIF);

                if (target.PropertyType == typeof(Union))
                {
                    IL.Emit(OpCodes.Call, StringToUnion);
                }

                IL.Emit(OpCodes.Call, setAccessor);
            }
        }
        private static void MapBinary(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            // _output.Свойство = reader.IsDBNull(ordinal) ? Array.Empty<byte>() : (byte[])reader.GetValue(ordinal);

            MethodInfo setAccessor = output.GetProperty(property.Name,
                BindingFlags.Instance | BindingFlags.Public).GetSetMethod();

            ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Value);

            if (column is not null)
            {
                int ordinal = Columns.IndexOf(column);

                Label _ELSE = IL.DefineLabel();
                Label _ENDIF = IL.DefineLabel();

                IL.Emit(OpCodes.Ldloc_0); // output variable reference

                // if (reader.IsDBNull(ordinal))
                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal);
                IL.Emit(OpCodes.Callvirt, IsDBNull);
                IL.Emit(OpCodes.Brfalse_S, _ELSE);

                // TRUE
                IL.Emit(OpCodes.Call, ArrayEmpty); // assign default value

                IL.Emit(OpCodes.Br_S, _ENDIF);

                IL.MarkLabel(_ELSE);

                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal);
                IL.Emit(OpCodes.Callvirt, GetValue);
                IL.Emit(OpCodes.Castclass, typeof(byte[]));

                IL.MarkLabel(_ENDIF);

                IL.Emit(OpCodes.Call, setAccessor);
            }
        }
        private static void MapUuid(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            MethodInfo setAccessor = output.GetProperty(property.Name,
                BindingFlags.Instance | BindingFlags.Public).GetSetMethod();

            ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Value);

            if (column is not null)
            {
                int ordinal = Columns.IndexOf(column);

                Label _ELSE = IL.DefineLabel();
                Label _ENDIF = IL.DefineLabel();

                IL.Emit(OpCodes.Ldloc_0); // output variable reference

                // if (reader.IsDBNull(ordinal))
                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal);
                IL.Emit(OpCodes.Callvirt, IsDBNull);
                IL.Emit(OpCodes.Brfalse_S, _ELSE);

                // TRUE
                IL.Emit(OpCodes.Ldsfld, GuidEmpty); // assign default value

                IL.Emit(OpCodes.Br_S, _ENDIF);

                IL.MarkLabel(_ELSE);

                // reader.GetBytes(ordinal, 0L, buffer, 0, 16);
                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal); // ordinal
                IL.Emit(OpCodes.Ldc_I4_0); // 0
                IL.Emit(OpCodes.Conv_I8); // 0 -> 0L
                IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference
                IL.Emit(OpCodes.Ldc_I4_0); // buffer start
                IL.Emit(OpCodes.Ldc_I4, 16); // bytes to read
                IL.Emit(OpCodes.Callvirt, GetBytes);
                IL.Emit(OpCodes.Pop); // remove return value from stack

                // _output.Свойство = buffer[0] != 0;
                IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference
                IL.Emit(OpCodes.Newobj, GuidCtor);

                IL.MarkLabel(_ENDIF);

                IL.Emit(OpCodes.Call, setAccessor);
            }
        }
        private static void MapEntity(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            PropertyInfo target = output.GetProperty(property.Name,
                BindingFlags.Instance | BindingFlags.Public);
            
            MethodInfo setAccessor = target.GetSetMethod();

            int ordinal;

            ColumnDefinition column;

            // single value column

            column = property.GetColumnByPurpose(ColumnPurpose.Value); // binary(16)

            // single column value

            if (column is not null) // binary(16)
            {
                ordinal = Columns.IndexOf(column);

                Label IsDBNull_true = IL.DefineLabel();
                Label IsDBNull_false = IL.DefineLabel();
                Label IsDBNull_endif = IL.DefineLabel();

                // if (reader.IsDBNull(ordinal))
                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal); // field ordinal
                IL.Emit(OpCodes.Callvirt, IsDBNull);
                IL.Emit(OpCodes.Brfalse_S, IsDBNull_false);
                // IsDBNull == true
                // _output.Свойство = Entity.Undefined;
                IL.Emit(OpCodes.Ldloc_0); // output variable reference
                IL.Emit(OpCodes.Ldsfld, EntityUndefined); // property value to assign
                IL.Emit(OpCodes.Call, setAccessor);
                IL.Emit(OpCodes.Br_S, IsDBNull_endif);
                // IsDBNull == false
                IL.MarkLabel(IsDBNull_false);
                IL.Emit(OpCodes.Ldloc_0); // output variable reference
                IL.Emit(OpCodes.Ldc_I4, property.Type.TypeCode);
                // reader.GetBytes(ordinal, 0L, buffer, 0, 16);
                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal); // ordinal
                IL.Emit(OpCodes.Ldc_I4_0);
                IL.Emit(OpCodes.Conv_I8); // 0L
                IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference
                IL.Emit(OpCodes.Ldc_I4_0); // buffer start
                IL.Emit(OpCodes.Ldc_I4, 16); // bytes to read
                IL.Emit(OpCodes.Callvirt, GetBytes);
                IL.Emit(OpCodes.Pop); // remove return value from stack
                // _output.Свойство = new Entity(column.Type.TypeCode, new Guid(buffer));
                IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference
                IL.Emit(OpCodes.Newobj, GuidCtor);
                IL.Emit(OpCodes.Newobj, EntityCtor);
                IL.Emit(OpCodes.Call, setAccessor);
                IL.MarkLabel(IsDBNull_endif);

                return;
            }

            // union type column

            IL.Emit(OpCodes.Ldloc_0); // output variable reference

            column = property.GetColumnByPurpose(ColumnPurpose.TypeCode); // binary(4)

            if (column is not null) // binary(4)
            {
                ordinal = Columns.IndexOf(column);

                Label _ELSE = IL.DefineLabel();
                Label _ENDIF = IL.DefineLabel();
                Label _ELSE_IF = IL.DefineLabel();
                Label _ELSE_IF_END = IL.DefineLabel();

                // if (reader.IsDBNull(ordinal))
                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal);
                IL.Emit(OpCodes.Callvirt, IsDBNull);
                IL.Emit(OpCodes.Brfalse, _ELSE_IF);
                // TRUE
                IL.Emit(OpCodes.Ldsfld, EntityUndefined); // Pushes Entity structure onto stack
                IL.Emit(OpCodes.Br, _ENDIF);
                
                IL.MarkLabel(_ELSE_IF);

                MapTypeCode(ordinal, in IL); // Pushes type code onto stack

                column = property.GetColumnByPurpose(ColumnPurpose.Identity); // binary(16)

                if (column is not null)
                {
                    ordinal = Columns.IndexOf(column);

                    // else if (reader.IsDBNull(ordinal))
                    IL.Emit(OpCodes.Ldarg_1); // reader
                    IL.Emit(OpCodes.Ldc_I4, ordinal);
                    IL.Emit(OpCodes.Callvirt, IsDBNull);
                    IL.Emit(OpCodes.Brfalse_S, _ELSE);

                    IL.Emit(OpCodes.Ldsfld, GuidEmpty); // Pushes empty UUID onto stack

                    IL.Emit(OpCodes.Br_S, _ELSE_IF_END);

                    IL.MarkLabel(_ELSE);

                    MapIdentity(ordinal, in IL); // Reads and pushes UUID onto stack
                }
                else
                {
                    IL.Emit(OpCodes.Ldsfld, GuidEmpty); // Pushes empty UUID onto stack
                }

                IL.MarkLabel(_ELSE_IF_END);

                IL.Emit(OpCodes.Newobj, EntityCtor);

                IL.MarkLabel(_ENDIF);

                if (target.PropertyType == typeof(Union))
                {
                    IL.Emit(OpCodes.Call, EntityToUnion);
                }
            }
            else // IsReferenceOnlyUnion == true (оптимизация хранения на стороне базы данных)
            {
                IL.Emit(OpCodes.Ldsfld, EntityUndefined); // test stub !!!

                if (target.PropertyType == typeof(Union)) // test stub !!!
                {
                    IL.Emit(OpCodes.Call, EntityToUnion); // test stub !!!
                }

                //IL.Emit(OpCodes.Ldc_I4, property.Type.TypeCode); // push type code to stack

                //column = property.GetColumnByPurpose(ColumnPurpose.Identity); // binary(16)

                //if (column is not null)
                //{
                //    ordinal = Columns.IndexOf(column);

                //    Label _ELSE = IL.DefineLabel();
                //    Label _ENDIF = IL.DefineLabel();

                //    // else if (reader.IsDBNull(ordinal))
                //    IL.Emit(OpCodes.Ldarg_1); // reader
                //    IL.Emit(OpCodes.Ldc_I4, ordinal);
                //    IL.Emit(OpCodes.Callvirt, IsDBNull);
                //    IL.Emit(OpCodes.Brfalse_S, _ELSE);

                //    IL.Emit(OpCodes.Ldsfld, GuidEmpty); // Pushes empty UUID onto stack

                //    IL.Emit(OpCodes.Br_S, _ENDIF);

                //    IL.MarkLabel(_ELSE);

                //    MapIdentity(ordinal, in IL); // Reads and pushes UUID onto stack

                //    IL.MarkLabel(_ENDIF);
                //}
                //else
                //{
                //    IL.Emit(OpCodes.Ldsfld, GuidEmpty); // Pushes empty UUID onto stack
                //}
            }

            IL.Emit(OpCodes.Call, setAccessor);
        }
        private static void MapTypeCode(int ordinal, in ILGenerator IL)
        {
            // reader.GetBytes(ordinal, 0L, buffer, 0, 4);
            IL.Emit(OpCodes.Ldarg_1); // reader
            IL.Emit(OpCodes.Ldc_I4, ordinal); // ordinal
            IL.Emit(OpCodes.Ldc_I4_0); // 0
            IL.Emit(OpCodes.Conv_I8); // 0 -> 0L
            IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference
            IL.Emit(OpCodes.Ldc_I4_0); // buffer start
            IL.Emit(OpCodes.Ldc_I4_4); // bytes to read
            IL.Emit(OpCodes.Callvirt, GetBytes);
            
            IL.Emit(OpCodes.Pop); // remove return value from stack

            // BinaryPrimitives.ReadInt32BigEndian(array.AsSpan(0, 4));
            IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference
            IL.Emit(OpCodes.Ldc_I4_0); // buffer start
            IL.Emit(OpCodes.Ldc_I4_4); // bytes to process
            IL.Emit(OpCodes.Call, AsSpanOfBytes);
            IL.Emit(OpCodes.Call, SpanOfBytesToReadOnly); // implicit conversion
            IL.Emit(OpCodes.Call, ReadInt32BigEndian); // byte[4] to int32
        }
        private static void MapIdentity(int ordinal, in ILGenerator IL)
        {
            // reader.GetBytes(ordinal, 0L, buffer, 0, 16);
            IL.Emit(OpCodes.Ldarg_1); // reader
            IL.Emit(OpCodes.Ldc_I4, ordinal); // ordinal
            IL.Emit(OpCodes.Ldc_I4_0);
            IL.Emit(OpCodes.Conv_I8); // 0L
            IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference
            IL.Emit(OpCodes.Ldc_I4_0); // buffer start
            IL.Emit(OpCodes.Ldc_I4, 16); // bytes to read
            IL.Emit(OpCodes.Callvirt, GetBytes);
            
            IL.Emit(OpCodes.Pop); // remove return value from stack

            IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference
            IL.Emit(OpCodes.Newobj, GuidCtor);
        }
        private static void MapUnion(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            DataType target = property.Type;

            ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Tag); // binary(1)

            if (column is not null) // _TYPE
            {
                int ordinal = Columns.IndexOf(column);

                Label defaultCase = IL.DefineLabel();
                Label endOfSwitch = IL.DefineLabel();

                // reader.GetBytes(ordinal, 0L, buffer, 0, 1);
                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal); // ordinal
                IL.Emit(OpCodes.Ldc_I4_0); // 0
                IL.Emit(OpCodes.Conv_I8); // 0 -> 0L
                IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference
                IL.Emit(OpCodes.Ldc_I4_0); // buffer start
                IL.Emit(OpCodes.Ldc_I4_1); // bytes to read
                IL.Emit(OpCodes.Callvirt, GetBytes);
                IL.Emit(OpCodes.Pop); // remove return value from stack

                // switch (buffer[0])
                IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference
                IL.Emit(OpCodes.Ldc_I4_0); // index of the value
                IL.Emit(OpCodes.Ldelem_U1); // push buffer[0] value onto the stack
                IL.Emit(OpCodes.Ldc_I4_1); // Нужно вычесть единицу из значения _TYPE
                IL.Emit(OpCodes.Sub); // Приводим значение _TYPE к индексам switch
                Label[] cases = [     // Jump table:
                    IL.DefineLabel(), // 1 = Неопределено
                    IL.DefineLabel(), // 2 = Булево
                    IL.DefineLabel(), // 3 = Число
                    IL.DefineLabel(), // 4 = Дата
                    IL.DefineLabel()  // 5 = Строка
                    ];
                IL.Emit(OpCodes.Switch, cases); // default - Ссылка
                IL.Emit(OpCodes.Br, defaultCase);
                
                IL.MarkLabel(cases[0]); // Неопределено
                MapUndefined(in output, in property, in IL);
                IL.Emit(OpCodes.Br, endOfSwitch);

                IL.MarkLabel(cases[1]); // Булево
                MapBoolean(in output, in property, in IL);
                IL.Emit(OpCodes.Br, endOfSwitch);

                IL.MarkLabel(cases[2]); // Число
                MapDecimal(in output, in property, in IL);
                IL.Emit(OpCodes.Br, endOfSwitch);

                IL.MarkLabel(cases[3]); // Дата
                MapDateTime(in output, in property, in IL);
                IL.Emit(OpCodes.Br, endOfSwitch);

                IL.MarkLabel(cases[4]); // Строка
                MapString(in output, in property, in IL);
                IL.Emit(OpCodes.Br, endOfSwitch);

                IL.MarkLabel(defaultCase); // 0x08 Ссылка
                MapEntity(in output, in property, in IL);
                //IL.Emit(OpCodes.Br, endOfSwitch);

                IL.MarkLabel(endOfSwitch);
            }
            else // _TRef + _RRef
            {
                MapEntity(in output, in property, in IL);
            }
        }
        private static void MapUndefined(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            MethodInfo setAccessor = output.GetProperty(property.Name,
                BindingFlags.Instance | BindingFlags.Public).GetSetMethod();

            if (property.Type.IsReferenceOnlyUnion)
            {
                IL.Emit(OpCodes.Ldloc_0); // output variable reference
                IL.Emit(OpCodes.Ldsfld, EntityUndefined); // property value to assign
            }
            else
            {
                IL.Emit(OpCodes.Ldloc_0); // output variable reference
                IL.Emit(OpCodes.Ldsfld, UnionUndefined); // property value to assign
            }

            IL.Emit(OpCodes.Call, setAccessor);
        }
    }
}