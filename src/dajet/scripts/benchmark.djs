
DECLARE @Код string = '000000001'

DECLARE @Таблица array
DECLARE @Объект  object

USE 'MS_TEST'
  
  SELECT TOP 1 Ссылка, Код, Наименование, ПометкаУдаления
    INTO @Объект
    FROM Справочник.Номенклатура
   WHERE Код = @Код

  SELECT TOP 3 Период, Цена, Дата = '2026-01-01T00:00:00'
    INTO @Таблица
    FROM РегистрСведений.ЦеныНоменклатуры
   WHERE Номенклатура = @Объект.Ссылка
   ORDER BY Период DESC

END

SET @Объект.Таблица = @Таблица

RETURN JSON(@Объект)