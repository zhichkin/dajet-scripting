
DECLARE @record object
DECLARE @Код    string  = '000000007'

USE 'MS_TEST'

  SELECT TOP 1
         Ссылка, Владелец, Наименование, СоставнойТип, ОптимизированнаяСсылка
    INTO @record
    FROM Справочник.Справочник1
   WHERE Код = @Код

END -- MS_TEST