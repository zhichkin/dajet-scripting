
DEFINE ЭлементСправочника
(
  Ссылка          entity,
  Код             string,
  Наименование    string,
  ПометкаУдаления boolean
)

DEFINE Справочник.Справочник1.Запись
(
  Ссылка       entity,
  Наименование string,
  Список       object OF Справочник.Справочник1
)

DEFINE Справочник.Справочник1
(
  Ссылка          entity,
  Владелец        entity,
  Код             string,
  Наименование    string,
  ПометкаУдаления boolean,
  Тест            string
)

DECLARE @Код       string  = '000000001'
DECLARE @Запись    object --OF Справочник.Справочник1.Запись
DECLARE @Список    array  --OF Справочник.Справочник1
DECLARE @PG_Запись object OF ЭлементСправочника

USE 'MS_TEST'
  
  SELECT Ссылка, Наименование
    INTO @Запись
    FROM Справочник.Справочник1
   WHERE Код = @Код

  SELECT Ссылка, Владелец
       , Код, Наименование
       , ПометкаУдаления
    INTO @Список
    FROM Справочник.Справочник1
   WHERE Ссылка = @Запись.Ссылка

   USE 'MS_UNF'
     SELECT TOP 1 Ссылка, Наименование
       INTO @PG_Запись
       FROM Справочник.Номенклатура
      --WHERE 1 / 0 = 0
   END

END

PRINT 'Конец скрипта'