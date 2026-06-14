
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
   SELECT ПоПорядкуУбывания = Уровень INTO @Таблица
     FROM РекурсивныйЗапрос
    WHERE Уровень > 2
    ORDER BY ПоПорядкуУбывания DESC

END

RETURN @Таблица