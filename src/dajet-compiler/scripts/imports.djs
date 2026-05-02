
--IMPORT '/mnt/c/GitHub/dajet-scripting/src/dajet-compiler/bin/Debug/net10.0/scripts/imports/'

IMPORT 'C:\GitHub\dajet-scripting\src\dajet-compiler\bin\Debug\net10.0\scripts\imports\'

DEFINE ОбменДанными.Сообщение
(
   Булево          boolean,
   ЦелоеЧисло      integer,
   ДесятичноеЧисло decimal,
   ДатаВремя       datetime,
   Строка          string,
   ДвоичныеДанные  binary,
   Идентификатор   uuid,
   Ссылка          entity,
   СоставнойТип    union(string,entity),
   Объект          object OF ОбменДанными.ОбъектДанных,
   Массив          array  OF ОбменДанными.ЭлементМассива
)

DEFINE ОбменДанными.ОбъектДанных
(
   Свойство string
)

DEFINE ОбменДанными.ЭлементМассива
(
   Свойство decimal
)