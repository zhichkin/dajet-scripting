
# SINGLETON 'select.product.by.code'

DECLARE @КодТовара string
DECLARE @БазаДанных string

PRIVATE @Таблица array

USE @БазаДанных

  SELECT Ссылка, Код, Наименование
    INTO @Таблица
    FROM Справочник.Номенклатура
   WHERE Код = @КодТовара

END

RETURN @Таблица