
PRIVATE @Счётчик    integer
PRIVATE @Получатель object
PRIVATE @Сообщение  object

USE 'MS_UNF'

   SELECT Ссылка, Код, Наименование
     INTO @Получатель
     FROM ПланОбмена.ТоварыИУслуги
    WHERE Код = '002'

   CONSUME TOP 10 УзелОбмена, Ссылка
         , НомерСообщения = ISNULL(НомерСообщения, 0.0)
        -- , ЭтоУдаление = CASE WHEN Данные.Ссылка IS NULL THEN TRUE ELSE FALSE END
      INTO @Сообщение
      FROM Справочник.Номенклатура.Изменения
     -- FROM Справочник.Номенклатура.Изменения AS Изменения
     -- LEFT JOIN Справочник.Номенклатура      AS Данные
     --   ON Изменения.Ссылка = Данные.Ссылка
     WHERE УзелОбмена = @Получатель.Ссылка
     ORDER BY УзелОбмена, Ссылка ASC

   

   SET @Счётчик = @Счётчик + 1
END

RETURN '[CONSUME] MS_UNF = ' + @Счётчик