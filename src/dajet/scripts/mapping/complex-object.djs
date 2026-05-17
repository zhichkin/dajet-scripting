
DEFINE ЗаписьДанных
(
  Ссылка          entity,
  Код             string,
  Наименование    string,
  ПометкаУдаления boolean
)

DEFINE ОбъектДанных
(
  Ссылка          entity,
  Код             string,
  Наименование    string,
  ПометкаУдаления boolean,
  Таблица         array OF ЗаписьДанных
)

DECLARE @Код string = '000000001'

--DECLARE @Таблица array  -- should be declared first
--DECLARE @Данные  object -- to make assignment work

DECLARE @Таблица array  OF ЗаписьДанных
DECLARE @Данные  object OF ОбъектДанных

USE 'MS_TEST'
  
  SELECT Ссылка, Код, Наименование, ПометкаУдаления
    INTO @Данные
    FROM Справочник.Справочник1
   WHERE Код = @Код

  SELECT Ссылка, Код, Наименование, ПометкаУдаления
    INTO @Таблица
    FROM Справочник.Справочник1
   WHERE Код = @Код

END

SET @Данные.Таблица = @Таблица