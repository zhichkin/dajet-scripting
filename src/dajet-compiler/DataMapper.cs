using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using System.Reflection;
using System.Reflection.Emit;

namespace DaJet.Compiler
{
    internal static class MsDataMapper
    {
        private static readonly MethodInfo IsDBNull;
        static MsDataMapper()
        {
            IsDBNull = typeof(SqlDataReader).GetMethod("IsDBNull",
                BindingFlags.Instance | BindingFlags.Public, [typeof(int)]);
        }


        private static int _ordinal = 0;
        internal static void MapOutput(in Type output, in EntityDefinition metadata, in ILGenerator IL)
        {
            // protected virtual void Process(SqlDataReader reader)
            // IL.Emit(OpCodes.Ldarg_0); // this SelectProcessor
            // IL.Emit(OpCodes.Ldarg_1); // SqlDataReader
            // IL.Emit(OpCodes.Ldloc_0); // output variable reference

            for (int p = 0; p < metadata.Properties.Count; p++)
            {
                PropertyDefinition property = metadata.Properties[p];

                if (property.Columns.Count == 1) // single value
                {
                    ColumnDefinition column = property.Columns[0];

                    if (property.Type.IsEntity)
                    {
                        // ((byte[])reader.GetValue(ordinal))
                        //IL.Emit(OpCodes.Ldarg_1); // reader
                        //IL.Emit(OpCodes.Ldloc_0); // ordinal
                        //IL.Emit(OpCodes.Callvirt, GetBytes);
                    }
                }
                else // multiple value
                {
                    //for (int c = 0; c < property.Columns.Count; c++)
                    //{
                    //    ColumnDefinition column = property.Columns[c];
                    //}
                }
            }
        }
        internal static void MapEntity(in Type output, in PropertyDefinition metadata, in ILGenerator IL)
        {
            //Label IsDBNull_true = IL.DefineLabel();
            //Label IsDBNull_false = IL.DefineLabel();
            //Label IsDBNull_endif = IL.DefineLabel();

            //// if (reader.IsDBNull(ordinal))
            //IL.Emit(OpCodes.Ldarg_1); // reader
            //IL.Emit(OpCodes.Ldc_I4, 0); // field ordinal
            //IL.Emit(OpCodes.Ldloc_0); // ordinal
            //IL.Emit(OpCodes.Callvirt, IsDBNull);
            //IL.Emit(OpCodes.Stloc_1); // pop result ?
            //IL.Emit(OpCodes.Ldloc_1); // push result ?
            //IL.Emit(OpCodes.Brfalse_S, IsDBNull_false);
            //IL.EmitWriteLine("set default value");
            //IL.Emit(OpCodes.Br_S, IsDBNull_endif);
            //IL.MarkLabel(IsDBNull_false);
            //IL.EmitWriteLine("set database value");
            //IL.MarkLabel(IsDBNull_endif);
            //IL.EmitWriteLine("end if");

            //MethodInfo setAccessor = output.GetProperty("Наименование",
            //    BindingFlags.Instance | BindingFlags.Public).GetSetMethod();
            //IL.Emit(OpCodes.Ldloc_0); // output variable reference
            //IL.Emit(OpCodes.Ldstr, "test тест"); // database value
            //IL.Emit(OpCodes.Call, setAccessor);
        }
    }
}

// List<__Список> список = _context.Список;
//IL.Emit(OpCodes.Ldarg_0); // this SelectProcessor
//IL.Emit(OpCodes.Ldfld, context); // Script context
//IL.Emit(OpCodes.Call, outputProperty.GetGetMethod());
//IL.Emit(OpCodes.Stloc_2);

//IL.Emit(OpCodes.Ldloc_2);
//IL.Emit(OpCodes.Callvirt, outputProperty.PropertyType
//    .GetProperty("Count", BindingFlags.Instance | BindingFlags.Public).GetGetMethod());
//IL.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine",
//        BindingFlags.Static | BindingFlags.Public, [typeof(int)]));

//IL.EmitWriteLine("Add record to array");
//IL.Emit(OpCodes.Ldloc_2);
//IL.Emit(OpCodes.Ldloc_3);
//IL.Emit(OpCodes.Callvirt, outputProperty.PropertyType
//    .GetMethod("Add", BindingFlags.Instance | BindingFlags.Public, [recordType]));

//IL.Emit(OpCodes.Ldloc_2);
//IL.Emit(OpCodes.Callvirt, outputProperty.PropertyType
//    .GetProperty("Count", BindingFlags.Instance | BindingFlags.Public).GetGetMethod());
//IL.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine",
//        BindingFlags.Static | BindingFlags.Public, [typeof(int)]));