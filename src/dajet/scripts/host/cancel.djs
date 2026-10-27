
-- Test script cancellation

DECLARE @input string

PRINT '[START]'

--SLEEP 10

TRY
   SLEEP 10
CATCH
   PRINT ERROR_MESSAGE()
FINALLY
   PRINT '[FINALLY]'
END

PRINT '[END] ' + @input

RETURN '[SUCCESS]'