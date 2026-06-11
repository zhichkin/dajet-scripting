
DECLARE @Таблица array
DECLARE @Код     string  = '000000007'

USE 'MS_TEST'

  WITH Документ AS
  (
    SELECT Ссылка FROM Документ.Документ1 WHERE Номер = 'MS-001'
  ),
  ТабличнаяЧасть AS
  (
    SELECT Реквизит1 FROM Документ.Документ1.ТабличнаяЧасть1 AS ТЧ
    INNER JOIN Документ AS Шапка ON Шапка.Ссылка = ТЧ.Ссылка
  )
  SELECT Реквизит1
    INTO @Таблица
    FROM ТабличнаяЧасть

END

RETURN @Таблица