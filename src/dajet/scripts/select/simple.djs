
DEFINE ОбъектДанных
(
  Ссылка       entity,
  Наименование string,
  ТестNULL     object OF ОбъектДанных
)

DECLARE @Код    string  = '000000007'
DECLARE @Запись object OF ОбъектДанных
DECLARE @Список array  OF ОбъектДанных

USE 'MS_TEST'
  SELECT Ссылка, Наименование
    INTO @Список -- @Запись
    FROM Справочник.Справочник1 AS T
   WHERE Код = @Код
END

RETURN @Список