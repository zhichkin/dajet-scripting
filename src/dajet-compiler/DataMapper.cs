using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using System.Reflection;
using System.Reflection.Emit;

namespace DaJet.Compiler
{
    internal static class MsDataMapper
    {
        private static readonly MethodInfo IsDBNull;
        private static readonly MethodInfo GetBytes;
        private static readonly FieldInfo EntityUndefined;
        private static readonly ConstructorInfo GuidCtor;
        private static readonly ConstructorInfo EntityCtor;
        static MsDataMapper()
        {
            IsDBNull = typeof(SqlDataReader).GetMethod("IsDBNull",
                BindingFlags.Instance | BindingFlags.Public, [typeof(int)]);

            GetBytes = typeof(SqlDataReader).GetMethod(nameof(SqlDataReader.GetBytes),
                BindingFlags.Instance | BindingFlags.Public,
                [typeof(int), typeof(long), typeof(byte[]), typeof(int), typeof(int)]);

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
            ColumnDefinition column = null;

            for (int i = 0; i < property.Columns.Count; i++)
            {
                column = property.Columns[i];

                if (column.Purpose == purpose)
                {
                    return column;
                }
            }

            return column;
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
            ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Value); // single value column

            column ??= property.GetColumnByPurpose(ColumnPurpose.Boolean); // union type column

            int ordinal = Columns.IndexOf(column);

            DataType source = column.Type;

            if (source.IsBoolean) // bit
            {

            }
            else if (source.IsBinary) // binary(1)
            {

            }
            else if (source.IsInteger) // int
            {
                if (source.Size == 1) // tinyint
                {

                }
                else if (source.Size == 2) // smallint
                {

                }
                else if (source.Size == 4) // int
                {

                }
                else if (source.Size == 8) // bigint
                {

                }
            }

            // set property value
        }
        private static void MapDecimal(in Type output, in PropertyDefinition property, in ILGenerator IL) { }
        private static void MapInteger(in Type output, in PropertyDefinition property, in ILGenerator IL) { }
        private static void MapDateTime(in Type output, in PropertyDefinition property, in ILGenerator IL) { }
        private static void MapString(in Type output, in PropertyDefinition property, in ILGenerator IL) { }
        private static void MapBinary(in Type output, in PropertyDefinition property, in ILGenerator IL) { }
        private static void MapUuid(in Type output, in PropertyDefinition property, in ILGenerator IL) { }
        private static void MapEntity(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            MethodInfo setAccessor = output.GetProperty(property.Name,
                BindingFlags.Instance | BindingFlags.Public).GetSetMethod();

            int ordinal;

            ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Value); // single value column

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

            column = property.GetColumnByPurpose(ColumnPurpose.TypeCode);

            if (column is not null) // binary(4)
            {
                ordinal = Columns.IndexOf(column);

                // reader.GetBytes
                // Положить на стек значение кода типа
            }
            else
            {
                IL.Emit(OpCodes.Ldc_I4, column.Type.TypeCode);
            }

            column = property.GetColumnByPurpose(ColumnPurpose.Identity);

            if (column is not null) // binary(16)
            {
                // Положить на стек Guid
            }

                DataType source = column.Type;

            if (column.Purpose == ColumnPurpose.TypeCode) // binary(4)
            {
                
            }
        }
        private static void MapUnion(in Type output, in PropertyDefinition property, in ILGenerator IL)
        {
            DataType target = property.Type;

            if (target.IsReferenceOnlyUnion)
            {
                //MapEntity(in output, in property, in IL);
            }

            ColumnDefinition column = property.GetColumnByPurpose(ColumnPurpose.Tag);

            if (column is null)
            {
                throw new FormatException();
            }

            if (column.Purpose == ColumnPurpose.Tag) // binary(1)
            {

            }
            else if (column.Purpose == ColumnPurpose.TypeCode) // binary(4)
            {

            }
            else if (column.Purpose == ColumnPurpose.Identity) // binary(16)
            {

            }
        }
    }
}