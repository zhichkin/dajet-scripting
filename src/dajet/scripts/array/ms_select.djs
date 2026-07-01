
DECLARE @Массив array(string) = ['MS-03','MS-06','MS-09']

DECLARE @Код01 string = 'MS-01'
DECLARE @Код02 string = 'MS-02'
DECLARE @Код03 string = 'MS-03'

PRIVATE @Таблица array
PRIVATE @Объект object
PRIVATE @Список array(entity)

SET @Массив = ['MS-04','MS-05','MS-07']
--SET @Массив = [@Код01, @Код02, @Код03]

USE 'MS_TEST'
  
  SELECT Ссылка
    INTO @Список
    FROM Справочник.Номенклатура
   WHERE Код IN (@Массив) OR Код = @Код03 OR Код IN (@Массив)

  SELECT Ссылка, Код, Наименование
    INTO @Таблица
    FROM Справочник.Номенклатура
   WHERE Ссылка IN (@Список)
   ORDER BY Код DESC

END

RETURN @Список --@Таблица