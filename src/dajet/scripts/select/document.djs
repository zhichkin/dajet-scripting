
DECLARE @Номер string  = 'MS-001'
DECLARE @Документ object
DECLARE @ТабличнаяЧасть array

USE 'MS_TEST'

  SELECT Ссылка, Дата, Номер
    INTO @Документ
    FROM Документ.Документ1
   WHERE Номер = @Номер

   SELECT НомерСтроки, Реквизит1
     INTO @ТабличнаяЧасть
     FROM Документ.Документ1.ТабличнаяЧасть1
    WHERE Ссылка = @Документ.Ссылка
    ORDER BY НомерСтроки DESC
END

SET @Документ.Товары = @ТабличнаяЧасть

RETURN @Документ