
DECLARE @Таблица array

USE 'MS_TEST'

  WITH РекурсивныйЗапрос AS
  (
    SELECT 1 AS Уровень
     UNION ALL
    SELECT parent.Уровень + 1
      FROM РекурсивныйЗапрос AS parent
     WHERE parent.Уровень + 1 <= 5
   )
   SELECT Уровень INTO @Таблица
     FROM РекурсивныйЗапрос
    ORDER BY Уровень DESC

END

RETURN @Таблица