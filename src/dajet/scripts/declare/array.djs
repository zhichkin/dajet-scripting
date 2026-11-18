
DECLARE @result object

DECLARE @МассивПустой array

PRIVATE @array_object array = []
PRIVATE @array_string array(string) = []
PRIVATE @array_values array(string) = ['a','b','c']

SET @result.МассивОбъект = @array_object
SET @result.МассивСтрока = @array_string
SET @result.МассивБукафф = @array_values
SET @result.МассивПустой = @МассивПустой

RETURN @result