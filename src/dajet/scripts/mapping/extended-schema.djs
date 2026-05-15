
DEFINE ОбъектДанных
(
  Ссылка        entity,
  Наименование  string,
  НовоеСвойство string,
  НовыйОбъект   object OF ОбъектДанных,
  НовыйМассив   array  OF ОбъектДанных
)

DECLARE @Код     string = '000000001'
DECLARE @Данные  object OF ОбъектДанных
DECLARE @Таблица array  OF ОбъектДанных

USE 'MS_TEST'
  
  SELECT Ссылка, Наименование
    INTO @Данные
    FROM Справочник.Справочник1
   WHERE Код = @Код

  SELECT Ссылка, Наименование
    INTO @Таблица
    FROM Справочник.Справочник1
   WHERE Код = @Код

END