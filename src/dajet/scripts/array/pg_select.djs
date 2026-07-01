
DECLARE @Массив array(string) = ['PG-001','PG-002','PG-003']

DECLARE @Код01 string = 'MS-01'
DECLARE @Код02 string = 'MS-02'
DECLARE @Код09 string = 'PG-009'

PRIVATE @Таблица array
PRIVATE @Объект object
PRIVATE @Список array(entity)

SET @Массив = ['PG-001','PG-002','PG-003']
--SET @Массив = [@Код01, @Код02, @Код03]

USE 'PG_TEST'
  
  SELECT Ссылка
    INTO @Список
    FROM Справочник.Номенклатура
   WHERE Код IN (@Массив) OR Код = @Код09

  SELECT Ссылка, Код, Наименование
    INTO @Таблица
    FROM Справочник.Номенклатура
   WHERE Ссылка IN (@Список)
   ORDER BY Код DESC

END

RETURN @Таблица --@Список