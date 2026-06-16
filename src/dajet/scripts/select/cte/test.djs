
PRIVATE @Таблица array

USE 'MS_TEST'

  WITH Выборка AS
  (
    SELECT TOP 5
           НомерПоПорядку = ROW_NUMBER() OVER (ORDER BY Код)
         , Ссылка
         , Наименование
      FROM Справочник.Номенклатура
     ORDER BY НомерПоПорядку DESC
  )
  SELECT НомерПоПорядку, Ссылка, Наименование
    INTO @Таблица
    FROM Выборка
   WHERE НомерПоПорядку > 1

END

RETURN @Таблица