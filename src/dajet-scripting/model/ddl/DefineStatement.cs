namespace DaJet.Scripting.Model
{
    public sealed class DefineStatement : SyntaxNode
    {
        public DefineStatement() { Token = Token.TYPE; }
        public string Identifier { get; set; } = string.Empty;
        public List<DefineProperty> Properties { get; } = new();
        public override string ToString()
        {
            return $"[{Token}: {Identifier}]";
        }
        public DefineProperty GetPropertyByName(in string name)
        {
            if (Properties == null || Properties.Count == 0)
            {
                return null;
            }

            foreach (DefineProperty property in Properties)
            {
                if (property.Name.Equals(name, StringComparison.Ordinal))
                {
                    return property;
                }
            }

            return null;
        }
    }
}

// Каталог /code/ms-test/schema/Справочники/Номенклатура.djs

//DEFINE Справочник.Номенклатура [_Reference123]
//(
//    Ссылка       uuid             [_IDRRef binary(16,fixed)],
//    ВерсияДанных binary(8,fixed)  [_Version rowversion]       VERSION,
//    Родитель     binary(16,fixed) [_Parent binary(16,fixed)]  REFERENCES(Справочник.Номенклатура),
//    Наименование string(100)      [_Desription],
//    Владелец entity [
//        _Fld123_TYPE binary(1,fixed)  TAG,
//        _Fld123_TRef binary(4,fixed)  TYPECODE,
//        _Fld123_RRef binary(16,fixed) IDENTITY] REFERENCES(Справочник.Поставщики, Справочник.Клиенты)

//    DEFINE ДополнительнаяИнформация [_Reference123_VT45]
//    (
//        Ссылка         uuid             [_Reference123_IDRRef binary(16,fixed)] REFERENCES(Справочник.Номенклатура),
//        KeyField       binary(4,fixed)  [_KeyField],
//        НомерСтроки    decimal(7,0)     [_LineNo727],
//        РеквизитСтрока string(10) VALUE [_Fld728]
//    )
//)

//IMPORT '/ms-test/schema' AS MS_TEST

//DECLARE @object object OF MS_TEST.Справочник.Номенклатура