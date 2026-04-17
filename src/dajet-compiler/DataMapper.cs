using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;

namespace DaJet.Compiler
{
    internal static class MsDataMapper
    {
        private static readonly MethodInfo AsSpanOfBytes;
        private static readonly MethodInfo SpanOfBytesToReadOnly;
        private static readonly MethodInfo ReadInt32BigEndian;

        private static readonly MethodInfo IsDBNull;
        private static readonly MethodInfo GetBytes;
        private static readonly MethodInfo GetString;
        private static readonly FieldInfo EntityUndefined;
        private static readonly FieldInfo UnionUndefined;
        private static readonly ConstructorInfo GuidCtor;
        private static readonly ConstructorInfo EntityCtor;

        static MsDataMapper()
        {
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

            GetBytes = typeof(SqlDataReader).GetMethod(nameof(SqlDataReader.GetBytes),
                BindingFlags.Instance | BindingFlags.Public,
                [typeof(int), typeof(long), typeof(byte[]), typeof(int), typeof(int)]);

            GetString = typeof(SqlDataReader).GetMethod(nameof(SqlDataReader.GetString),
                BindingFlags.Instance | BindingFlags.Public, [typeof(int)]);

            UnionUndefined = typeof(Union).GetField(nameof(Union.Undefined),
                BindingFlags.Static | BindingFlags.Public);

            EntityUndefined = typeof(Entity).GetField(nameof(Entity.Undefined),
                BindingFlags.Static | BindingFlags.Public);

            GuidCtor = typeof(Guid).GetConstructor(BindingFlags.Instance | BindingFlags.Public,
                [typeof(byte[])]);

            EntityCtor = typeof(Entity).GetConstructor(BindingFlags.Instance | BindingFlags.Public,
                [typeof(int), typeof(Guid)]);
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
        private static ColumnDefinition GetColumnByPurpose(this PropertyDefinition property, ColumnPurpose purpose)
        {
            ColumnDefinition column;

            for (int i = 0; i < property.Columns.Count; i++)
            {
                column = property.Columns[i];

                if (column.Purpose == purpose)
                {
                    return column;
                }
            }

            return null;
        }
        internal static void MapOutput(in Type output, in EntityDefinition metadata, in ILGenerator IL)
        {
            // protected virtual void Process(SqlDataReader reader)
            // IL.Emit(OpCodes.Ldarg_0); // this SelectProcessor
            // IL.Emit(OpCodes.Ldarg_1); // SqlDataReader
            // IL.Emit(OpCodes.Ldloc_0); // output variable reference
            // IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference

            FlattenColumns(in metadata);

            for (int p = 0; p < metadata.Properties.Count; p++)
            {
                PropertyDefinition property = metadata.Properties[p];

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
            MethodInfo setAccessor = output.GetProperty(property.Name,
                BindingFlags.Instance | BindingFlags.Public).GetSetMethod();

            ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Value); // single value column

            column ??= property.GetColumnByPurpose(ColumnPurpose.Boolean); // union type column

            if (column is not null)
            {
                int ordinal = Columns.IndexOf(column);

                IL.Emit(OpCodes.Ldloc_0); // output variable reference

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

                IL.Emit(OpCodes.Call, setAccessor);
            }
        }
        private static void MapDecimal(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            //return reader.GetDecimal(ordinal);

            Label IsDBNull_true = IL.DefineLabel();
            Label IsDBNull_false = IL.DefineLabel();
            Label IsDBNull_endif = IL.DefineLabel();

            // if (reader.IsDBNull(ordinal))
            //IL.Emit(OpCodes.Ldarg_1); // reader
            //IL.Emit(OpCodes.Ldc_I4, ordinal); // field ordinal
            //IL.Emit(OpCodes.Callvirt, IsDBNull);
            //IL.Emit(OpCodes.Brfalse_S, IsDBNull_false);
            // IsDBNull == true
            // _output.Свойство = Entity.Undefined;
            //IL.Emit(OpCodes.Ldloc_0); // output variable reference
            //IL.Emit(OpCodes.Ldsfld, EntityUndefined); // property value to assign
            //IL.Emit(OpCodes.Call, setAccessor);
            //IL.Emit(OpCodes.Br_S, IsDBNull_endif);
            // IsDBNull == false
            //IL.MarkLabel(IsDBNull_false);
            //IL.Emit(OpCodes.Ldloc_0); // output variable reference
            //IL.Emit(OpCodes.Ldc_I4, property.Type.TypeCode);
            //// reader.GetBytes(ordinal, 0L, buffer, 0, 16);
            //IL.Emit(OpCodes.Ldarg_1); // reader
            //IL.Emit(OpCodes.Ldc_I4, ordinal); // ordinal
            //IL.Emit(OpCodes.Ldc_I4_0);
            //IL.Emit(OpCodes.Conv_I8); // 0L
            //IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference
            //IL.Emit(OpCodes.Ldc_I4_0); // buffer start
            //IL.Emit(OpCodes.Ldc_I4, 16); // bytes to read
            //IL.Emit(OpCodes.Callvirt, GetBytes);
            //IL.Emit(OpCodes.Pop); // remove return value from stack
            //                      // _output.Свойство = new Entity(column.Type.TypeCode, new Guid(buffer));
            //IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference
            //IL.Emit(OpCodes.Newobj, GuidCtor);
            //IL.Emit(OpCodes.Newobj, EntityCtor);
            //IL.Emit(OpCodes.Call, setAccessor);
            //IL.MarkLabel(IsDBNull_endif);
        }
        private static void MapInteger(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            //reader.GetInt32(ordinal)
            //reader.GetInt64(ordinal)
        }
        private static void MapDateTime(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            //int ordinal = GetOrdinal(in reader, UnionTag.DateTime, out _);

            //if (reader.IsDBNull(ordinal))
            //{
            //    return null;
            //}

            //return reader.GetDateTime(ordinal).AddYears(-YearOffset);
        }
        private static void MapString(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            MethodInfo setAccessor = output.GetProperty(property.Name,
                BindingFlags.Instance | BindingFlags.Public).GetSetMethod();

            ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Value); // single value column

            column ??= property.GetColumnByPurpose(ColumnPurpose.String); // union type column

            if (column is not null)
            {
                int ordinal = Columns.IndexOf(column);

                // _output.Свойство = reader.GetString(ordinal);

                IL.Emit(OpCodes.Ldloc_0); // output variable reference
                IL.Emit(OpCodes.Ldarg_1); // reader
                IL.Emit(OpCodes.Ldc_I4, ordinal);
                IL.Emit(OpCodes.Callvirt, GetString);
                IL.Emit(OpCodes.Call, setAccessor);
            }
        }
        private static void MapBinary(in Type output, in PropertyDefinition property, in ILGenerator IL) { }
        private static void MapUuid(in Type output, in PropertyDefinition property, in ILGenerator IL) { }
        private static void MapEntity(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            MethodInfo setAccessor = output.GetProperty(property.Name,
                BindingFlags.Instance | BindingFlags.Public).GetSetMethod();

            int ordinal;

            // single value column

            ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Value); // binary(16)

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

            column = property.GetColumnByPurpose(ColumnPurpose.TypeCode); // binary(4)

            if (column is not null) // binary(4)
            {
                ordinal = Columns.IndexOf(column);

                IL.Emit(OpCodes.Ldloc_0); // output variable reference

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
            else // IsReferenceOnlyUnion == true
            {
                IL.Emit(OpCodes.Ldloc_0); // output variable reference
                IL.Emit(OpCodes.Ldc_I4, property.Type.TypeCode); // push type code to stack
            }

            column = property.GetColumnByPurpose(ColumnPurpose.Identity); // binary(16)

            if (column is not null)
            {
                ordinal = Columns.IndexOf(column);

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
            }

            // _output.Свойство = new Entity(_TRef, _RRef);
            IL.Emit(OpCodes.Ldloc_1); // byte[16] buffer reference
            IL.Emit(OpCodes.Newobj, GuidCtor);
            IL.Emit(OpCodes.Newobj, EntityCtor);
            IL.Emit(OpCodes.Call, setAccessor);
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
                IL.Emit(OpCodes.Br_S, defaultCase);
                
                IL.MarkLabel(cases[0]); // Неопределено
                MapUndefined(in output, in property, in IL);
                IL.Emit(OpCodes.Br_S, endOfSwitch);

                IL.MarkLabel(cases[1]); // Булево
                MapBoolean(in output, in property, in IL);
                IL.Emit(OpCodes.Br_S, endOfSwitch);

                IL.MarkLabel(cases[2]); // Число
                MapDecimal(in output, in property, in IL);
                IL.Emit(OpCodes.Br_S, endOfSwitch);

                IL.MarkLabel(cases[3]); // Дата
                MapDateTime(in output, in property, in IL);
                IL.Emit(OpCodes.Br_S, endOfSwitch);

                IL.MarkLabel(cases[4]); // Строка
                MapString(in output, in property, in IL);
                IL.Emit(OpCodes.Br_S, endOfSwitch);

                IL.MarkLabel(defaultCase); // 0x08 Ссылка
                MapEntity(in output, in property, in IL);
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