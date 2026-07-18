
DECLARE @Ссылка       entity = {53:b188f299-2c11-1b8f-11f1-64e06c69cb27}
DECLARE @Код          string = 'MS-07'
DECLARE @Наименование string = 'SKU-07'

USE 'MS_TEST'

   INSERT Справочник.Номенклатура
   SELECT Ссылка       = @Ссылка
        , Код          = @Код
        , Наименование = @Наименование

END