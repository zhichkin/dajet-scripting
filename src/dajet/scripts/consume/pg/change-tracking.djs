
PRIVATE @Счётчик    integer
PRIVATE @Получатель object
PRIVATE @Сообщение  object

USE 'PG_UNF'

   SELECT Ссылка, Код, Наименование
     INTO @Получатель
     FROM ПланОбмена.ТоварыИУслуги
    WHERE Код = '002'

   CONSUME TOP 10 УзелОбмена, Ссылка
         , СтавкаНДС = Перечисление.СтавкиНДС.БезНДС
         , НомерСообщения = ISNULL(НомерСообщения, 0.0)
         , ЭтоУдаление = CASE WHEN EXISTS(SELECT 1 FROM Справочник.Номенклатура AS Данные WHERE Данные.Ссылка = Изменения.Ссылка)
                              THEN FALSE ELSE TRUE END
      INTO @Сообщение
      FROM Справочник.Номенклатура.Изменения AS Изменения
     WHERE УзелОбмена = @Получатель.Ссылка
     ORDER BY УзелОбмена, Ссылка DESC

   PRINT JSON(@Сообщение)

   SET @Счётчик = @Счётчик + 1
END

RETURN '[CONSUME] MS_UNF = ' + @Счётчик