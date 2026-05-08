
DECLARE @catalog  object
DECLARE @register object
DECLARE @Код      string  = '000000007'

USE 'MS_TEST'

  SELECT TOP 1
         Ссылка, Владелец, Наименование, СоставнойТип, ОптимизированнаяСсылка
    INTO @catalog
    FROM Справочник.Справочник1
   WHERE Код = @Код

  SELECT TOP 1 Регистратор INTO @register FROM РегистрНакопления.РегистрНакопления1

END -- MS_TEST