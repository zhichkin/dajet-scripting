
DECLARE @Таблица array
DECLARE @Код     string  = '000000007'

USE 'MS_UNF'

  WITH Выборка AS
  (
    SELECT TOP 5 Ссылка, ЭтоГруппа, Код, Наименование FROM Справочник.Номенклатура WHERE ЭтоГруппа = FALSE
  )
  SELECT Ссылка, ЭтоГруппа, Код, Наименование
    INTO @Таблица
    FROM Выборка

END

RETURN @Таблица